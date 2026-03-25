using System.Windows;
using System.IO;
using System.Linq;
using AnimeSorterWin.Data;
using AnimeSorterWin.Services;
using AnimeSorterWin.Services.Api;
using AnimeSorterWin.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeSorterWin;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    // 给 MainWindow 构造函数做兜底注入用，避免 OnStartup 时机导致 DataContext 为空。
    public static ServiceProvider? Services { get; private set; }

    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // SQLite：缓存识别结果，避免重复 MD5 与重复 API 调用。
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnimeSorterWin");
        Directory.CreateDirectory(appFolder);
        // 由于项目早期只保存了 RecognitionCacheEntity，后续新增了候选集表 RecognitionCandidatesCacheEntity。
        // 对于已经存在的旧数据库文件，EnsureCreated 不会自动补全新表，因此使用带“SchemaVersion”的新库文件。
        const int SchemaVersion = 2;
        var dbPath = Path.Combine(appFolder, $"anime-sorter.v{SchemaVersion}.db");
        var connectionString = $"Data Source={dbPath};Cache=Shared";

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // HTTP：IHttpClientFactory 管理连接池。
        services.AddHttpClient("anime_api", client =>
        {
            // 避免单次请求长时间无响应导致“看起来卡住”
            client.Timeout = TimeSpan.FromSeconds(25);
        });

        // API 基础配置（运行时请把 Url 改成你的真实接口）。
        services.AddSingleton(new AnimeApiSettings());

        services.AddSingleton<ProcessingPipelineService>();

        services.AddSingleton<MainViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // 确保数据库表存在（首次运行会自动创建）。
        var dbFactory = _serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using (var db = dbFactory.CreateDbContext())
        {
            db.Database.EnsureCreated();
        }

        // 用 Dispatcher 延后设置 DataContext：避免 StartupUri 创建窗口尚未就绪导致绑定失效。
        Dispatcher.BeginInvoke(() =>
        {
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow is not null)
            {
                mainWindow.DataContext = _serviceProvider!.GetRequiredService<MainViewModel>();
                return;
            }

            // 兜底：如果 MainWindow 还没成为 Current.MainWindow，则遍历当前打开的窗口。
            foreach (var w in System.Windows.Application.Current.Windows.OfType<MainWindow>())
            {
                w.DataContext = _serviceProvider!.GetRequiredService<MainViewModel>();
                break;
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _serviceProvider?.Dispose();
        }
        finally
        {
            base.OnExit(e);
        }
    }
}

