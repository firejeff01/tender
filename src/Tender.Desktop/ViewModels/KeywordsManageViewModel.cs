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

    public IReadOnlyList<string> TargetFieldOptions { get; } = new[]
    {
        "tenderName", "agencyName", "any",
    };

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
            foreach (var g in set.Groups)
            {
                var eg = new EditableGroup(g.Name);
                foreach (var k in g.Items)
                    eg.Items.Add(new EditableKeyword(k.Keyword, k.TargetField, k.Enabled));
                eg.PropertyChanged += OnDirty;
                eg.Items.CollectionChanged += (_, _) => HasUnsavedChanges = true;
                foreach (var ek in eg.Items)
                    ek.PropertyChanged += OnDirty;
                Groups.Add(eg);
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

    private void OnDirty(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HasUnsavedChanges) && e.PropertyName != nameof(StatusMessage))
            HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddGroup()
    {
        var eg = new EditableGroup("新群組");
        eg.PropertyChanged += OnDirty;
        eg.Items.CollectionChanged += (_, _) => HasUnsavedChanges = true;
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
    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            var set = new KeywordSet
            {
                Groups = Groups.Select(g => new KeywordGroup
                {
                    Name = g.Name,
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

public partial class EditableGroup : ObservableObject
{
    [ObservableProperty]
    private string _name;

    public ObservableCollection<EditableKeyword> Items { get; } = new();

    public EditableGroup(string name) { _name = name; }
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
