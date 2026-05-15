using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tender.Core.Models;
using Tender.Storage.Repositories;

namespace Tender.Desktop.ViewModels;

public partial class KeywordsManageViewModel : ObservableObject
{
    private readonly IKeywordsRepository _repo;

    public ObservableCollection<EditableGroup> Groups { get; } = new();

    [ObservableProperty]
    private EditableGroup? _selectedGroup;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>
    /// 比對欄位選項：Value 是存到 JSON 的鍵 (tenderName/agencyName/any)，
    /// Display 是 UI 顯示的中文名稱（對應主畫面欄位用語）。
    /// </summary>
    public IReadOnlyList<TargetFieldOption> TargetFieldOptions { get; } = new[]
    {
        new TargetFieldOption("tenderName", "標案名稱"),
        new TargetFieldOption("agencyName", "機關名稱"),
        new TargetFieldOption("any", "標案名稱＋機關名稱"),
    };

    /// <summary>給 XAML 顏色色盤用的預設色清單（直接綁定）。</summary>
    public IReadOnlyList<string> PresetColors { get; } = KeywordGroupColors.Palette;

    public KeywordsManageViewModel(IKeywordsRepository repo)
    {
        _repo = repo;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            var set = await _repo.LoadAsync(ct);
            Groups.Clear();
            int idx = 0;
            foreach (var g in set.Groups)
            {
                var color = string.IsNullOrWhiteSpace(g.Color)
                    ? KeywordGroupColors.GetByIndex(idx)
                    : g.Color;
                var eg = new EditableGroup(g.Name, color);
                foreach (var k in g.Items)
                    eg.Items.Add(new EditableKeyword(k.Keyword, k.TargetField, k.Enabled));
                AttachGroupListeners(eg);
                Groups.Add(eg);
                idx++;
            }
            if (Groups.Count > 0) SelectedGroup = Groups[0];
            HasUnsavedChanges = false;
            StatusMessage = $"已載入 {Groups.Count} 個群組";
        }
        catch (Exception ex)
        {
            StatusMessage = $"載入失敗：{ex.Message}";
        }
    }

    private void AttachGroupListeners(EditableGroup eg)
    {
        eg.PropertyChanged += OnDirty;
        eg.Items.CollectionChanged += (_, _) => HasUnsavedChanges = true;
        foreach (var ek in eg.Items)
            ek.PropertyChanged += OnDirty;
    }

    private void OnDirty(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HasUnsavedChanges) && e.PropertyName != nameof(StatusMessage))
            HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddGroup()
    {
        // 自動指派下一個未用色（若全部用完則循環）
        var usedColors = new HashSet<string>(Groups.Select(g => g.Color), StringComparer.OrdinalIgnoreCase);
        var nextColor = KeywordGroupColors.Palette.FirstOrDefault(c => !usedColors.Contains(c))
                        ?? KeywordGroupColors.GetByIndex(Groups.Count);
        var eg = new EditableGroup("新群組", nextColor);
        AttachGroupListeners(eg);
        Groups.Add(eg);
        SelectedGroup = eg;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void DeleteGroup()
    {
        if (SelectedGroup == null) return;
        Groups.Remove(SelectedGroup);
        SelectedGroup = Groups.FirstOrDefault();
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddKeyword()
    {
        if (SelectedGroup == null) return;
        var ek = new EditableKeyword("新關鍵字", "tenderName", true);
        ek.PropertyChanged += OnDirty;
        SelectedGroup.Items.Add(ek);
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void DeleteKeyword(EditableKeyword? keyword)
    {
        if (SelectedGroup == null || keyword == null) return;
        SelectedGroup.Items.Remove(keyword);
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void MoveGroupUp(EditableGroup? group)
    {
        if (group == null) return;
        var idx = Groups.IndexOf(group);
        if (idx <= 0) return;
        Groups.Move(idx, idx - 1);
        SelectedGroup = group;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void MoveGroupDown(EditableGroup? group)
    {
        if (group == null) return;
        var idx = Groups.IndexOf(group);
        if (idx < 0 || idx >= Groups.Count - 1) return;
        Groups.Move(idx, idx + 1);
        SelectedGroup = group;
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void MoveKeywordUp(EditableKeyword? keyword)
    {
        if (keyword == null || SelectedGroup == null) return;
        var idx = SelectedGroup.Items.IndexOf(keyword);
        if (idx <= 0) return;
        SelectedGroup.Items.Move(idx, idx - 1);
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void MoveKeywordDown(EditableKeyword? keyword)
    {
        if (keyword == null || SelectedGroup == null) return;
        var idx = SelectedGroup.Items.IndexOf(keyword);
        if (idx < 0 || idx >= SelectedGroup.Items.Count - 1) return;
        SelectedGroup.Items.Move(idx, idx + 1);
        HasUnsavedChanges = true;
    }

    /// <summary>給 drag-drop helper 用：把群組從 fromIndex 移到 toIndex。</summary>
    public void MoveGroup(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Groups.Count) return;
        if (toIndex < 0 || toIndex >= Groups.Count) return;
        if (fromIndex == toIndex) return;
        var moved = Groups[fromIndex];
        Groups.Move(fromIndex, toIndex);
        SelectedGroup = moved;
        HasUnsavedChanges = true;
    }

    /// <summary>給 drag-drop helper 用：把目前群組中的 keyword 從 fromIndex 移到 toIndex。</summary>
    public void MoveKeyword(int fromIndex, int toIndex)
    {
        if (SelectedGroup == null) return;
        var items = SelectedGroup.Items;
        if (fromIndex < 0 || fromIndex >= items.Count) return;
        if (toIndex < 0 || toIndex >= items.Count) return;
        if (fromIndex == toIndex) return;
        items.Move(fromIndex, toIndex);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// 把內嵌的「出廠預設」keywords 合併進當前清單：
    /// 已存在的群組（同名）只補上「使用者沒有的關鍵字」（同 Keyword + TargetField 視為相同），
    /// 不存在的群組整組附加在最後。完全不會覆蓋使用者現有的順序、顏色、targetField、enabled 設定。
    /// 用於升級新版後把新增的 ESG 等項目補進來，不必砍 keywords.json。
    /// </summary>
    [RelayCommand]
    private void MergeEmbeddedDefaults()
    {
        var defaultSet = _repo.GetEmbeddedDefault();
        int newGroupCount = 0;
        int newItemCount = 0;

        foreach (var dg in defaultSet.Groups)
        {
            var existing = Groups.FirstOrDefault(g => g.Name == dg.Name);
            if (existing is null)
            {
                var color = string.IsNullOrWhiteSpace(dg.Color)
                    ? KeywordGroupColors.GetByIndex(Groups.Count)
                    : dg.Color;
                var eg = new EditableGroup(dg.Name, color);
                foreach (var k in dg.Items)
                {
                    var ek = new EditableKeyword(k.Keyword, k.TargetField, k.Enabled);
                    eg.Items.Add(ek);
                    newItemCount++;
                }
                AttachGroupListeners(eg);
                Groups.Add(eg);
                newGroupCount++;
            }
            else
            {
                foreach (var k in dg.Items)
                {
                    var dup = existing.Items.Any(x =>
                        x.Keyword == k.Keyword && x.TargetField == k.TargetField);
                    if (dup) continue;
                    var ek = new EditableKeyword(k.Keyword, k.TargetField, k.Enabled);
                    ek.PropertyChanged += OnDirty;
                    existing.Items.Add(ek);
                    newItemCount++;
                }
            }
        }

        if (newGroupCount == 0 && newItemCount == 0)
        {
            StatusMessage = "目前已是最新預設，沒有需要補的群組或項目";
            return;
        }

        HasUnsavedChanges = true;
        StatusMessage = $"已合併新版預設：新群組 {newGroupCount}、新項目 {newItemCount}（記得按「💾 儲存」）";
    }

    [RelayCommand]
    private void SetGroupColor((EditableGroup group, string color) arg)
    {
        if (arg.group == null || string.IsNullOrWhiteSpace(arg.color)) return;
        arg.group.Color = arg.color;
        // HasUnsavedChanges 由 OnDirty 透過 PropertyChanged 自動觸發
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            var set = new KeywordSet
            {
                Groups = Groups.Select(g => new KeywordGroup
                {
                    Name = g.Name,
                    Color = g.Color,
                    Items = g.Items.Select(k => new KeywordItem
                    {
                        Keyword = k.Keyword,
                        TargetField = k.TargetField,
                        Enabled = k.Enabled,
                    }).ToList().AsReadOnly(),
                }).ToList().AsReadOnly(),
            };
            await _repo.SaveAsync(set, ct);
            HasUnsavedChanges = false;
            StatusMessage = $"儲存成功（{Groups.Count} 群組 / {Groups.Sum(g => g.Items.Count)} 關鍵字）";
        }
        catch (Exception ex)
        {
            StatusMessage = $"儲存失敗：{ex.Message}";
        }
    }
}

public sealed record TargetFieldOption(string Value, string Display);

public partial class EditableGroup : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _color;

    public ObservableCollection<EditableKeyword> Items { get; } = new();

    public EditableGroup(string name, string color)
    {
        _name = name;
        _color = color;
    }
}

public partial class EditableKeyword : ObservableObject
{
    [ObservableProperty]
    private string _keyword;

    [ObservableProperty]
    private string _targetField;

    [ObservableProperty]
    private bool _enabled;

    public EditableKeyword(string keyword, string targetField, bool enabled)
    {
        _keyword = keyword;
        _targetField = targetField;
        _enabled = enabled;
    }
}
