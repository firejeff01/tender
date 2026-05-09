namespace Tender.Desktop.Services;

/// <summary>以使用者預設瀏覽器開啟 URL。</summary>
public interface IBrowserLauncher
{
    void Open(string url);
}
