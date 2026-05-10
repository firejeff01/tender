using System.Diagnostics;

namespace Tender.Desktop.Services;

public sealed class ProcessStartBrowserLauncher : IBrowserLauncher
{
    public void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // 只接受 http/https，避免 UseShellExecute=true 把 file://、javascript:、
        // \\unc\path、cmd: 等其他 scheme 交給 OS handler 開啟。
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
}
