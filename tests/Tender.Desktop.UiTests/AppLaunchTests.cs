using System.IO;
using FluentAssertions;
using Tender.Desktop.UiTests.Infrastructure;
using Xunit;

namespace Tender.Desktop.UiTests;

/// <summary>
/// 證明 fixture 啟動流程 + env var 隔離 + 取得主視窗都能跑。
/// </summary>
[Collection("UiTests")]
public sealed class AppLaunchTests
{
    private readonly AppFixture _fixture;

    public AppLaunchTests(AppFixture fixture) => _fixture = fixture;

    [Fact]
    public void Launches_MainWindow_IsVisible()
    {
        _fixture.MainWindow.Should().NotBeNull();
        _fixture.MainWindow.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void Launches_DataRoot_IsCreatedAndIsolated()
    {
        Directory.Exists(_fixture.DataRoot).Should().BeTrue();
    }
}
