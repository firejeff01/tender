using Xunit;

namespace Tender.Desktop.UiTests.Infrastructure;

/// <summary>
/// 整個 UiTests assembly 的測試共用同一個 <see cref="AppFixture"/> instance。
/// xUnit 對 Collection Fixture 的 dispose 會在最後一個測試跑完後執行，
/// 所以 app 跨整個 assembly 只啟動/結束一次。
///
/// 用法：每個 test class 都標上 <c>[Collection("UiTests")]</c>。
/// </summary>
[CollectionDefinition("UiTests")]
public sealed class UiTestsCollection : ICollectionFixture<AppFixture>
{
    // 不需要程式碼；介面就是註冊。
}
