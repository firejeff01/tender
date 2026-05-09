using System.Diagnostics;

namespace Tender.Desktop.Services;

public sealed class ProcessStartBrowserLauncher : IBrowserLauncher
{
    public void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
