using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using AnimeSorterWin.Data;
using AnimeSorterWin.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.EntityFrameworkCore;
using AnimeSorterWin.Data.Entities;

namespace AnimeSorterWin.ViewModels;

public sealed partial class ConfirmWindowViewModel : ObservableObject
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private RecognitionCandidatesData? _focusedCandidates;

    public ObservableCollection<PendingImageItem> Items { get; }

    [ObservableProperty]
    private PendingImageItem? focusedItem;

    [ObservableProperty]
    private CandidateFilterMode filterMode = CandidateFilterMode.WorkCharacter;

    [ObservableProperty]
    private List<CandidateDropdownOption> candidateDropdownOptions = new();

    [ObservableProperty]
    private CandidateDropdownOption? selectedDropdownOption;

    [ObservableProperty]
    private string workInput = string.Empty;

    [ObservableProperty]
    private string characterInput = string.Empty;

    // 用于判断是否已完成第一次加载，避免在初始化阶段触发联动。
    private bool _isInitialized;

    public ConfirmWindowViewModel(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IEnumerable<PendingImageItem> items)
    {
        _dbContextFactory = dbContextFactory;
        Items = new ObservableCollection<PendingImageItem>(items);
        if (Items.Count > 0)
        {
            FocusedItem = Items[0];
            WorkInput = Items[0].SelectedWork;
            CharacterInput = Items[0].SelectedCharacter;
        }
    }

    public async Task LoadCandidatesForFocusedItemAsync(CancellationToken ct)
    {
        if (FocusedItem is null)
            return;

        // 先尝试从导入的候选缓存字典读取（用于导出/导入续审）
        if (_candidatesOverride.TryGetValue(FocusedItem.Md5, out var overrideData))
        {
            _focusedCandidates = overrideData;
            BuildDropdownOptionsAndSyncInputs();
            _isInitialized = true;
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var entity = await db.RecognitionCandidatesCaches.FirstOrDefaultAsync(x => x.Md5 == FocusedItem.Md5, ct).ConfigureAwait(false);
        if (entity is null)
        {
            _focusedCandidates = null;
            CandidateDropdownOptions = new List<CandidateDropdownOption>();
            SelectedDropdownOption = null;
            return;
        }

        _focusedCandidates = JsonSerializer.Deserialize<RecognitionCandidatesData>(entity.CandidatesJson);
        if (_focusedCandidates is null)
        {
            CandidateDropdownOptions = new List<CandidateDropdownOption>();
            SelectedDropdownOption = null;
            return;
        }

        BuildDropdownOptionsAndSyncInputs();
        _isInitialized = true;
    }

    partial void OnFilterModeChanged(CandidateFilterMode value)
    {
        if (!_isInitialized)
            return;

        BuildDropdownOptionsAndSyncInputs();
    }

    partial void OnSelectedDropdownOptionChanged(CandidateDropdownOption? value)
    {
        if (!_isInitialized)
            return;
        if (value is null)
            return;
        if (FocusedItem is null)
            return;

        // 下拉框只修改当前模式对应的输入框，另一项保留用户/默认输入（符合“候选筛选仅用于候选选择”）。
        switch (FilterMode)
        {
            case CandidateFilterMode.WorkOnly:
                WorkInput = value.Work;
                break;
            case CandidateFilterMode.CharacterOnly:
                CharacterInput = value.Character;
                break;
            case CandidateFilterMode.WorkCharacter:
            default:
                WorkInput = value.Work;
                CharacterInput = value.Character;
                break;
        }
    }

    private void BuildDropdownOptionsAndSyncInputs()
    {
        if (_focusedCandidates is null || _focusedCandidates.Characters.Count == 0)
        {
            CandidateDropdownOptions = new List<CandidateDropdownOption>();
            SelectedDropdownOption = null;
            return;
        }

        var candidates = _focusedCandidates.Characters;

        var options = new List<CandidateDropdownOption>();
        switch (FilterMode)
        {
            case CandidateFilterMode.WorkOnly:
                {
                    var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in candidates)
                    {
                        if (distinct.Add(c.Work))
                            options.Add(new CandidateDropdownOption(c.Work, c.Work, string.Empty));
                    }
                    break;
                }
            case CandidateFilterMode.CharacterOnly:
                {
                    var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in candidates)
                    {
                        if (distinct.Add(c.Character))
                            options.Add(new CandidateDropdownOption(c.Character, string.Empty, c.Character));
                    }
                    break;
                }
            case CandidateFilterMode.WorkCharacter:
            default:
                {
                    var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in candidates)
                    {
                        var key = $"{c.Work}::{c.Character}";
                        if (distinct.Add(key))
                            options.Add(new CandidateDropdownOption($"{c.Work} - {c.Character}", c.Work, c.Character));
                    }
                    break;
                }
        }

        CandidateDropdownOptions = options;

        // 根据当前输入同步下拉框选中项
        CandidateDropdownOption? match = null;
        switch (FilterMode)
        {
            case CandidateFilterMode.WorkOnly:
                match = options.FirstOrDefault(o => string.Equals(o.Work, WorkInput, StringComparison.OrdinalIgnoreCase));
                break;
            case CandidateFilterMode.CharacterOnly:
                match = options.FirstOrDefault(o => string.Equals(o.Character, CharacterInput, StringComparison.OrdinalIgnoreCase));
                break;
            case CandidateFilterMode.WorkCharacter:
            default:
                match = options.FirstOrDefault(o =>
                    string.Equals(o.Work, WorkInput, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(o.Character, CharacterInput, StringComparison.OrdinalIgnoreCase));
                break;
        }

        // 如果当前输入无法匹配选项，就不强制选中，允许手动输入。
        if (match is not null)
            SelectedDropdownOption = match;
        else
            SelectedDropdownOption = null;
    }

    // 用于“输出目录结构”的映射：与候选筛选使用同一套分类模式按钮。
    public OutputOrganizationMode OutputOrganizationMode => FilterMode switch
    {
        CandidateFilterMode.WorkOnly => OutputOrganizationMode.SeriesOnly,
        CandidateFilterMode.CharacterOnly => OutputOrganizationMode.CharacterOnly,
        CandidateFilterMode.WorkCharacter => OutputOrganizationMode.SeriesThenCharacter,
        _ => OutputOrganizationMode.SeriesThenCharacter
    };

    // 导出/导入续审：把候选集缓存放到内存字典里。
    private readonly Dictionary<string, RecognitionCandidatesData> _candidatesOverride = new(StringComparer.OrdinalIgnoreCase);

    private sealed class ConfirmExportBundle
    {
        public required CandidateFilterMode FilterMode { get; init; }
        public required List<ConfirmExportItem> Items { get; init; }
        public int Version { get; init; } = 1;
    }

    private sealed class ConfirmExportItem
    {
        public required string FilePath { get; init; }
        public required string Md5 { get; init; }

        public required string DefaultWork { get; init; }
        public required string DefaultCharacter { get; init; }

        public required string SelectedWork { get; init; }
        public required string SelectedCharacter { get; init; }

        public required PendingItemStatus Status { get; init; }

        public required double BoxX1 { get; init; }
        public required double BoxY1 { get; init; }
        public required double BoxX2 { get; init; }
        public required double BoxY2 { get; init; }
        public required string BoxId { get; init; }
        public required bool NotConfident { get; init; }

        public required RecognitionCandidatesData CandidatesData { get; init; }
    }

    /// <summary>
    /// 导出“确认续审文件”：包含每张图的候选集、box，以及已选/已确认状态。
    /// </summary>
    public async Task ExportToFileAsync(string filePath, CancellationToken ct)
    {
        var exportItems = new List<ConfirmExportItem>(Items.Count);

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        foreach (var item in Items)
        {
            ct.ThrowIfCancellationRequested();

            RecognitionCandidatesData? candidatesData = null;
            if (_candidatesOverride.TryGetValue(item.Md5, out var overrideData))
                candidatesData = overrideData;
            else
            {
                var entity = await db.RecognitionCandidatesCaches.FirstOrDefaultAsync(x => x.Md5 == item.Md5, ct).ConfigureAwait(false);
                if (entity is not null)
                    candidatesData = JsonSerializer.Deserialize<RecognitionCandidatesData>(entity.CandidatesJson);
            }

            candidatesData ??= new RecognitionCandidatesData(
                Box: [item.BoxX1, item.BoxY1, item.BoxX2, item.BoxY2],
                BoxId: item.BoxId,
                NotConfident: item.NotConfident,
                Characters: [new CandidateWorkCharacter(item.DefaultWork, item.DefaultCharacter)]
            );

            exportItems.Add(new ConfirmExportItem
            {
                FilePath = item.FilePath,
                Md5 = item.Md5,
                DefaultWork = item.DefaultWork,
                DefaultCharacter = item.DefaultCharacter,
                SelectedWork = item.SelectedWork,
                SelectedCharacter = item.SelectedCharacter,
                Status = item.Status,
                BoxX1 = item.BoxX1,
                BoxY1 = item.BoxY1,
                BoxX2 = item.BoxX2,
                BoxY2 = item.BoxY2,
                BoxId = item.BoxId,
                NotConfident = item.NotConfident,
                CandidatesData = candidatesData
            });
        }

        var bundle = new ConfirmExportBundle
        {
            FilterMode = FilterMode,
            Items = exportItems
        };

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 导入“确认续审文件”，刷新当前 Items 与候选集。
    /// </summary>
    public async Task ImportFromFileAsync(string filePath, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

        var bundle = JsonSerializer.Deserialize<ConfirmExportBundle>(json);
        if (bundle is null || bundle.Items is null)
            return;

        // 重置当前状态
        _candidatesOverride.Clear();
        Items.Clear();

        FilterMode = bundle.FilterMode;

        foreach (var ei in bundle.Items)
        {
            ct.ThrowIfCancellationRequested();

            Items.Add(new PendingImageItem
            {
                FilePath = ei.FilePath,
                Md5 = ei.Md5,
                DefaultWork = ei.DefaultWork,
                DefaultCharacter = ei.DefaultCharacter,
                SelectedWork = ei.SelectedWork,
                SelectedCharacter = ei.SelectedCharacter,
                Status = ei.Status,
                BoxX1 = ei.BoxX1,
                BoxY1 = ei.BoxY1,
                BoxX2 = ei.BoxX2,
                BoxY2 = ei.BoxY2,
                BoxId = ei.BoxId,
                NotConfident = ei.NotConfident
            });

            _candidatesOverride[ei.Md5] = ei.CandidatesData;
        }

        if (Items.Count > 0)
        {
            FocusedItem = Items[0];
            WorkInput = Items[0].SelectedWork;
            CharacterInput = Items[0].SelectedCharacter;
        }

        _focusedCandidates = null;
        CandidateDropdownOptions = new List<CandidateDropdownOption>();
        SelectedDropdownOption = null;
        _isInitialized = false;
    }

    public void FocusedItemChanged(PendingImageItem? item)
    {
        FocusedItem = item;
        if (item is null)
            return;

        // 保持当前行的已选 work/character 显示在输入框
        WorkInput = item.SelectedWork;
        CharacterInput = item.SelectedCharacter;

        // 由于 FocusedItem 变化，需要重新加载 candidates 并同步 dropdown
        _focusedCandidates = null;
        CandidateDropdownOptions = new List<CandidateDropdownOption>();
        SelectedDropdownOption = null;
        _isInitialized = false;
    }

    public void ConfirmSelectedItems(IEnumerable<PendingImageItem> selected)
    {
        foreach (var item in selected)
        {
            if (string.IsNullOrWhiteSpace(WorkInput))
                item.SelectedWork = item.DefaultWork;
            else
                item.SelectedWork = WorkInput.Trim();

            if (string.IsNullOrWhiteSpace(CharacterInput))
                item.SelectedCharacter = item.DefaultCharacter;
            else
                item.SelectedCharacter = CharacterInput.Trim();

            item.Status = PendingItemStatus.Confirmed;
        }
    }

    public void SkipSelectedItems(IEnumerable<PendingImageItem> selected)
    {
        foreach (var item in selected)
            item.Status = PendingItemStatus.Skipped;
    }

    public void ConfirmAll()
    {
        foreach (var item in Items)
            item.ResetToDefault();
    }
}

