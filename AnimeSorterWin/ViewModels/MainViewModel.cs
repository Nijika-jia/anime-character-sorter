using System.Windows.Forms;
using System.IO;
using AnimeSorterWin.Models;
using AnimeSorterWin.Services;
using AnimeSorterWin.Services.Api;
using AnimeSorterWin.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimeSorterWin.Data;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using Microsoft.Win32;

namespace AnimeSorterWin.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ProcessingPipelineService _pipelineService;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    private CancellationTokenSource? _cts;

    public MainViewModel(ProcessingPipelineService pipelineService, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _pipelineService = pipelineService;
        _dbContextFactory = dbContextFactory;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? inputRoot;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string? outputRoot;

    // 并发数滑块（限制 API 并发/吞吐的关键参数）
    [ObservableProperty]
    private int apiConcurrency = 3;

    // 默认无鉴权接口容易触发频率限制：按你确认的预设
    [ObservableProperty]
    private double apiMaxRps = 2;

    [ObservableProperty]
    private FileOperationMode fileOperationMode = FileOperationMode.Copy;

    /// <summary>
    /// 输出目录归类方式（决定最终子文件夹结构）。
    /// </summary>
    [ObservableProperty]
    private OutputOrganizationMode outputOrganizationMode = OutputOrganizationMode.SeriesThenCharacter;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool isRunning;

    [ObservableProperty]
    private long scannedCount;

    [ObservableProperty]
    private long cacheHitsCount;

    [ObservableProperty]
    private long apiSuccessCount;

    [ObservableProperty]
    private long apiFailuresCount;

    [ObservableProperty]
    private long throttled429Count;

    [ObservableProperty]
    private string statusText = "等待开始";

    private bool CanStart() =>
        !IsRunning
        && !string.IsNullOrWhiteSpace(InputRoot)
        && !string.IsNullOrWhiteSpace(OutputRoot)
        && Directory.Exists(InputRoot!)
        && Directory.Exists(OutputRoot!);

    [RelayCommand]
    private void BrowseInput()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "选择输入目录（包含大量动漫图片）",
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrWhiteSpace(InputRoot))
            dlg.SelectedPath = InputRoot!;

        if (dlg.ShowDialog() == DialogResult.OK)
            InputRoot = dlg.SelectedPath;
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "选择输出目录（将按 Series/Character 归类）",
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrWhiteSpace(OutputRoot))
            dlg.SelectedPath = OutputRoot!;

        if (dlg.ShowDialog() == DialogResult.OK)
            OutputRoot = dlg.SelectedPath;
    }

    // 不使用 CanExecute：避免 WPF 在命令不可执行时静默拦截点击导致“没反应”
    [RelayCommand]
    private async Task StartAsync()
    {
        if (!CanStart())
        {
            StatusText = "请先选择有效的输入目录与输出目录";
            return;
        }

        // 防止重复启动
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        IsRunning = true;
        StatusText = "正在扫描并处理...";

        // 清空统计
        ScannedCount = 0;
        CacheHitsCount = 0;
        ApiSuccessCount = 0;
        ApiFailuresCount = 0;
        Throttled429Count = 0;

        var progress = new Progress<PipelineProgressUpdate>(p =>
        {
            ScannedCount = p.Scanned;
            CacheHitsCount = p.CacheHits;
            ApiSuccessCount = p.ApiSuccess;
            ApiFailuresCount = p.ApiFailures;
            Throttled429Count = p.Throttled429;

            // 让“正在扫描并识别”不再是静态文本，便于你判断是否卡在枚举/MD5/API。
            StatusText = $"正在扫描并识别... 已扫描 {p.Scanned} 张，429={p.Throttled429}";
        });

        try
        {
            var request = new PipelineRequest
            {
                InputRoot = InputRoot!,
                OutputRoot = OutputRoot!,
                ApiMaxDegreeOfParallelism = ApiConcurrency,
                ApiMaxRps = ApiMaxRps,
                FileOperationMode = FileOperationMode,
                ApiRequestMode = ApiRequestMode.MultipartFormData,
                OutputOrganizationMode = OutputOrganizationMode
            };

            StatusText = "正在扫描并识别（将保存所有候选以供确认）...";

            var pendingItems = await _pipelineService.ScanAndRecognizeCandidatesAsync(request, progress, _cts.Token);

            StatusText = "识别完成，正在打开确认窗口...";
            var confirmVm = new ConfirmWindowViewModel(_dbContextFactory, pendingItems);
            var confirmWindow = new ConfirmWindow(confirmVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            var ok = confirmWindow.ShowDialog();
            if (ok != true)
            {
                StatusText = "已取消确认";
                return;
            }

            StatusText = "正在整理文件...";
            await _pipelineService.OrganizeConfirmedFilesAsync(request, pendingItems, confirmVm.OutputOrganizationMode, _cts.Token);
            StatusText = "处理完成";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已停止";
        }
        catch (Exception ex)
        {
            StatusText = $"发生错误：{ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    // 导入确认数据：直接在主界面选择文件，然后进入确认窗口。
    [RelayCommand]
    private async Task ImportAsync()
    {
        if (IsRunning)
            return;

        StatusText = "导入确认数据：正在弹出文件选择窗口...";

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入确认数据",
            Filter = "AnimeSorter确认数据 (*.animesortercache.json)|*.animesortercache.json"
        };

        if (dlg.ShowDialog() != true)
            return;

        // 弹窗一定要先弹出来：导入数据本身不依赖 OutputRoot（只是在最后整理文件时需要）。
        if (string.IsNullOrWhiteSpace(OutputRoot) || !Directory.Exists(OutputRoot!))
        {
            StatusText = "请先选择有效的输出目录（用于整理文件）。";
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsRunning = true;

        try
        {
            StatusText = "正在导入确认数据...";

            var confirmVm = new ConfirmWindowViewModel(_dbContextFactory, new List<PendingImageItem>());
            await confirmVm.ImportFromFileAsync(dlg.FileName, _cts.Token);

            if (confirmVm.Items.Count == 0)
            {
                StatusText = "导入的数据为空。";
                return;
            }

            var confirmWindow = new ConfirmWindow(confirmVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var ok = confirmWindow.ShowDialog();
            if (ok != true)
            {
                StatusText = "已取消确认";
                return;
            }

            // 组织文件（不再扫描/API；使用导入的 PendingImageItem）
            var request = new PipelineRequest
            {
                InputRoot = InputRoot ?? string.Empty,
                OutputRoot = OutputRoot!,
                ApiMaxDegreeOfParallelism = ApiConcurrency,
                ApiMaxRps = ApiMaxRps,
                FileOperationMode = FileOperationMode,
                ApiRequestMode = ApiRequestMode.MultipartFormData,
                OutputOrganizationMode = OutputOrganizationMode
            };

            StatusText = "正在整理文件...";
            await _pipelineService.OrganizeConfirmedFilesAsync(request, confirmVm.Items, confirmVm.OutputOrganizationMode, _cts.Token);
            StatusText = "处理完成";
        }
        catch (OperationCanceledException)
        {
            StatusText = "已停止";
        }
        catch (Exception ex)
        {
            StatusText = $"发生错误：{ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        try
        {
            _cts?.Cancel();
            StatusText = "正在停止...";
        }
        catch
        {
            // 忽略
        }
    }

    private bool CanStop() => IsRunning;
}

