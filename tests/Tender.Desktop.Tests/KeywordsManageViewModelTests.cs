using FluentAssertions;
using Moq;
using Tender.Core.Models;
using Tender.Desktop.ViewModels;
using Tender.Storage.Repositories;
using Xunit;

namespace Tender.Desktop.Tests;

/// <summary>
/// 2026-05-14「篩選類別設定」改動的單元測試 cover：
/// - 拖拉/上下排序的邊界
/// - AddGroup 自動配色
/// - MergeEmbeddedDefaults 合併語意
/// - HasUnsavedChanges dirty flag 觸發路徑
///
/// IKeywordsRepository 用 Moq；ViewModel 純記憶體跑得起來。
/// </summary>
public sealed class KeywordsManageViewModelTests
{
    private static KeywordSet MakeSet(params (string name, string color, string[] keywords)[] groups) =>
        new()
        {
            Groups = groups.Select(g => new KeywordGroup
            {
                Name = g.name,
                Color = g.color,
                Items = g.keywords.Select(k => new KeywordItem
                {
                    Keyword = k,
                    TargetField = "tenderName",
                    Enabled = true,
                }).ToList().AsReadOnly(),
            }).ToList().AsReadOnly(),
        };

    private static (KeywordsManageViewModel vm, Mock<IKeywordsRepository> repo) Build(
        KeywordSet? initial = null,
        KeywordSet? embeddedDefault = null)
    {
        var repo = new Mock<IKeywordsRepository>();
        repo.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial ?? new KeywordSet { Groups = Array.Empty<KeywordGroup>() });
        repo.Setup(r => r.GetEmbeddedDefault())
            .Returns(embeddedDefault ?? new KeywordSet { Groups = Array.Empty<KeywordGroup>() });
        repo.Setup(r => r.SaveAsync(It.IsAny<KeywordSet>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var vm = new KeywordsManageViewModel(repo.Object);
        return (vm, repo);
    }

    private static async Task<KeywordsManageViewModel> BuildLoaded(KeywordSet initial, KeywordSet? embedded = null)
    {
        var (vm, _) = Build(initial, embedded);
        await vm.LoadCommand.ExecuteAsync(null);
        return vm;
    }

    // ============================================================
    // Reorder：上下按鈕
    // ============================================================

    [Fact]
    public async Task MoveGroupUp_FromMiddle_SwapsWithAbove()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }), ("B", "#222", new[] { "b" }), ("C", "#333", new[] { "c" }));
        var vm = await BuildLoaded(set);

        vm.MoveGroupUpCommand.Execute(vm.Groups[1]);

        vm.Groups.Select(g => g.Name).Should().Equal("B", "A", "C");
        vm.SelectedGroup!.Name.Should().Be("B");
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task MoveGroupUp_AtTop_IsNoop()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }), ("B", "#222", new[] { "b" }));
        var vm = await BuildLoaded(set);

        vm.MoveGroupUpCommand.Execute(vm.Groups[0]);

        vm.Groups.Select(g => g.Name).Should().Equal("A", "B");
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task MoveGroupDown_AtBottom_IsNoop()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }), ("B", "#222", new[] { "b" }));
        var vm = await BuildLoaded(set);

        vm.MoveGroupDownCommand.Execute(vm.Groups[1]);

        vm.Groups.Select(g => g.Name).Should().Equal("A", "B");
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task MoveKeywordUp_FromMiddle_SwapsWithAbove()
    {
        var set = MakeSet(("A", "#111", new[] { "a1", "a2", "a3" }));
        var vm = await BuildLoaded(set);
        var middle = vm.SelectedGroup!.Items[1];

        vm.MoveKeywordUpCommand.Execute(middle);

        vm.SelectedGroup.Items.Select(k => k.Keyword).Should().Equal("a2", "a1", "a3");
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task MoveKeywordDown_AtBottom_IsNoop()
    {
        var set = MakeSet(("A", "#111", new[] { "a1", "a2" }));
        var vm = await BuildLoaded(set);

        vm.MoveKeywordDownCommand.Execute(vm.SelectedGroup!.Items[1]);

        vm.SelectedGroup.Items.Select(k => k.Keyword).Should().Equal("a1", "a2");
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    // ============================================================
    // Reorder：drag-drop 進入點 MoveGroup(from, to) / MoveKeyword(from, to)
    // ============================================================

    [Fact]
    public async Task MoveGroup_ValidIndices_Reorders()
    {
        var set = MakeSet(("A", "#1", new[] { "a" }), ("B", "#2", new[] { "b" }), ("C", "#3", new[] { "c" }));
        var vm = await BuildLoaded(set);

        vm.MoveGroup(0, 2);

        vm.Groups.Select(g => g.Name).Should().Equal("B", "C", "A");
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, -1)]
    [InlineData(5, 1)]
    [InlineData(0, 99)]
    [InlineData(1, 1)] // same index
    public async Task MoveGroup_InvalidOrSameIndex_IsNoop(int from, int to)
    {
        var set = MakeSet(("A", "#1", new[] { "a" }), ("B", "#2", new[] { "b" }));
        var vm = await BuildLoaded(set);

        vm.MoveGroup(from, to);

        vm.Groups.Select(g => g.Name).Should().Equal("A", "B");
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task MoveKeyword_ValidIndices_Reorders()
    {
        var set = MakeSet(("A", "#1", new[] { "a1", "a2", "a3" }));
        var vm = await BuildLoaded(set);

        vm.MoveKeyword(0, 2);

        vm.SelectedGroup!.Items.Select(k => k.Keyword).Should().Equal("a2", "a3", "a1");
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task MoveKeyword_NoSelectedGroup_IsNoop()
    {
        var set = MakeSet(("A", "#1", new[] { "a1", "a2" }));
        var vm = await BuildLoaded(set);
        vm.SelectedGroup = null;

        vm.MoveKeyword(0, 1);

        vm.HasUnsavedChanges.Should().BeFalse();
    }

    // ============================================================
    // AddGroup 自動配色
    // ============================================================

    [Fact]
    public async Task AddGroup_PicksFirstUnusedPaletteColor()
    {
        // 既有群組已用前 3 色 → 下一個應為第 4 色
        var set = MakeSet(
            ("A", KeywordGroupColors.Palette[0], new[] { "a" }),
            ("B", KeywordGroupColors.Palette[1], new[] { "b" }),
            ("C", KeywordGroupColors.Palette[2], new[] { "c" }));
        var vm = await BuildLoaded(set);

        vm.AddGroupCommand.Execute(null);

        vm.Groups.Should().HaveCount(4);
        vm.Groups[3].Color.Should().Be(KeywordGroupColors.Palette[3]);
    }

    [Fact]
    public async Task AddGroup_AllPaletteColorsUsed_WrapsViaGetByIndex()
    {
        var groups = Enumerable.Range(0, KeywordGroupColors.Palette.Count)
            .Select(i => ($"G{i}", KeywordGroupColors.Palette[i], new[] { "k" }))
            .ToArray();
        var set = MakeSet(groups);
        var vm = await BuildLoaded(set);

        vm.AddGroupCommand.Execute(null);

        vm.Groups.Last().Color.Should().Be(KeywordGroupColors.GetByIndex(KeywordGroupColors.Palette.Count));
    }

    // ============================================================
    // MergeEmbeddedDefaults
    // ============================================================

    [Fact]
    public async Task MergeEmbeddedDefaults_EmptyCurrent_AddsAllDefaultGroups()
    {
        var embedded = MakeSet(("ESG/碳管理", "#7A8B5C", new[] { "ESG", "碳排" }));
        var vm = await BuildLoaded(initial: new KeywordSet { Groups = Array.Empty<KeywordGroup>() }, embedded);

        vm.MergeEmbeddedDefaultsCommand.Execute(null);

        vm.Groups.Should().HaveCount(1);
        vm.Groups[0].Name.Should().Be("ESG/碳管理");
        vm.Groups[0].Color.Should().Be("#7A8B5C");
        vm.Groups[0].Items.Select(k => k.Keyword).Should().Equal("ESG", "碳排");
        vm.HasUnsavedChanges.Should().BeTrue();
        vm.StatusMessage.Should().Contain("新群組 1").And.Contain("新項目 2");
    }

    [Fact]
    public async Task MergeEmbeddedDefaults_SameNamedGroup_AppendsOnlyMissingItems()
    {
        // 使用者既有：ESG 群組（自訂色 #FF0000）含 "ESG"
        var initial = MakeSet(("ESG/碳管理", "#FF0000", new[] { "ESG" }));
        // 新版預設：ESG 群組（不同顏色）含 "ESG"+"碳排"
        var embedded = MakeSet(("ESG/碳管理", "#7A8B5C", new[] { "ESG", "碳排" }));
        var vm = await BuildLoaded(initial, embedded);

        vm.MergeEmbeddedDefaultsCommand.Execute(null);

        vm.Groups.Should().HaveCount(1);
        vm.Groups[0].Color.Should().Be("#FF0000", "既有自訂色不應被覆寫");
        vm.Groups[0].Items.Select(k => k.Keyword).Should().Equal("ESG", "碳排");
        vm.StatusMessage.Should().Contain("新群組 0").And.Contain("新項目 1");
    }

    [Fact]
    public async Task MergeEmbeddedDefaults_AlreadyUpToDate_StatusReflectsNoop()
    {
        var same = MakeSet(("A", "#111", new[] { "a" }));
        var vm = await BuildLoaded(initial: same, embedded: same);

        vm.MergeEmbeddedDefaultsCommand.Execute(null);

        vm.HasUnsavedChanges.Should().BeFalse();
        vm.StatusMessage.Should().Contain("已是最新");
    }

    [Fact]
    public async Task MergeEmbeddedDefaults_SameKeywordDifferentTargetField_AddedAsSeparate()
    {
        // 使用者既有：地區/基隆 (any)
        var initial = new KeywordSet
        {
            Groups = new[]
            {
                new KeywordGroup
                {
                    Name = "地區",
                    Color = "#6B8E6B",
                    Items = new[] { new KeywordItem { Keyword = "基隆", TargetField = "any", Enabled = true } },
                }
            }
        };
        // 新版預設：地區/基隆 (tenderName) — 同 keyword 但 targetField 不同 → 應算新項目
        var embedded = new KeywordSet
        {
            Groups = new[]
            {
                new KeywordGroup
                {
                    Name = "地區",
                    Color = "#6B8E6B",
                    Items = new[] { new KeywordItem { Keyword = "基隆", TargetField = "tenderName", Enabled = true } },
                }
            }
        };
        var vm = await BuildLoaded(initial, embedded);

        vm.MergeEmbeddedDefaultsCommand.Execute(null);

        vm.Groups[0].Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeEmbeddedDefaults_ExistingItemOrderPreserved()
    {
        // 使用者把預設順序顛倒過：先 b 後 a
        var initial = MakeSet(("A", "#1", new[] { "b", "a" }));
        // 預設順序：a, b, c
        var embedded = MakeSet(("A", "#1", new[] { "a", "b", "c" }));
        var vm = await BuildLoaded(initial, embedded);

        vm.MergeEmbeddedDefaultsCommand.Execute(null);

        // 既有的 b、a 順序不變；c append 在後
        vm.Groups[0].Items.Select(k => k.Keyword).Should().Equal("b", "a", "c");
    }

    // ============================================================
    // HasUnsavedChanges：觸發路徑
    // ============================================================

    [Fact]
    public async Task HasUnsavedChanges_AfterLoad_IsFalse()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }));
        var vm = await BuildLoaded(set);

        vm.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task HasUnsavedChanges_OnColorChange_BecomesTrue()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }));
        var vm = await BuildLoaded(set);

        vm.Groups[0].Color = "#999999";

        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task HasUnsavedChanges_OnNameChange_BecomesTrue()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }));
        var vm = await BuildLoaded(set);

        vm.Groups[0].Name = "B";

        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task HasUnsavedChanges_OnKeywordEnabledToggle_BecomesTrue()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }));
        var vm = await BuildLoaded(set);

        vm.SelectedGroup!.Items[0].Enabled = false;

        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task HasUnsavedChanges_AddGroup_BecomesTrue()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }));
        var vm = await BuildLoaded(set);

        vm.AddGroupCommand.Execute(null);

        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task HasUnsavedChanges_AfterSave_BecomesFalse()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }));
        var vm = await BuildLoaded(set);
        vm.AddGroupCommand.Execute(null);
        vm.HasUnsavedChanges.Should().BeTrue();

        await vm.SaveCommand.ExecuteAsync(null);

        vm.HasUnsavedChanges.Should().BeFalse();
    }

    // ============================================================
    // Load：backfill 缺色的 group
    // ============================================================

    [Fact]
    public async Task Load_GroupWithoutColor_GetsBackfilledFromPalette()
    {
        var set = new KeywordSet
        {
            Groups = new[]
            {
                new KeywordGroup
                {
                    Name = "缺色組",
                    // Color 留空字串
                    Items = new[] { new KeywordItem { Keyword = "k", TargetField = "tenderName", Enabled = true } },
                }
            }
        };
        var (vm, _) = Build(set);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Groups[0].Color.Should().Be(KeywordGroupColors.GetByIndex(0));
    }

    // ============================================================
    // TargetFieldOptions 中文化
    // ============================================================

    [Fact]
    public void TargetFieldOptions_HasChineseDisplay()
    {
        var (vm, _) = Build();

        vm.TargetFieldOptions.Should().Contain(o => o.Value == "tenderName" && o.Display == "標案名稱");
        vm.TargetFieldOptions.Should().Contain(o => o.Value == "agencyName" && o.Display == "機關名稱");
        vm.TargetFieldOptions.Should().Contain(o => o.Value == "any" && o.Display == "標案名稱＋機關名稱");
    }

    // ============================================================
    // Save：寫回的 KeywordSet 順序與 Color 對齊使用者操作
    // ============================================================

    [Fact]
    public async Task Save_PersistsOrderAndColorAfterReorder()
    {
        var set = MakeSet(("A", "#111", new[] { "a" }), ("B", "#222", new[] { "b" }));
        var (vm, repo) = Build(set);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.MoveGroup(0, 1);
        vm.Groups[0].Color = "#999999"; // 原 B
        await vm.SaveCommand.ExecuteAsync(null);

        repo.Verify(r => r.SaveAsync(It.Is<KeywordSet>(s =>
            s.Groups.Count == 2 &&
            s.Groups[0].Name == "B" && s.Groups[0].Color == "#999999" &&
            s.Groups[1].Name == "A" && s.Groups[1].Color == "#111"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
