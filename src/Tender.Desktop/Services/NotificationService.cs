using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Tender.Desktop.Services;

public interface INotificationService
{
    void ShowInfo(string title, string message);
    void Dispose();
}

/// <summary>
/// System tray icon 通知。長駐顯示 tray icon，可發 balloon tip。
/// </summary>
public sealed class NotificationService : INotificationService, IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public NotificationService()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "政府電子採購網標案查詢",
            Visible = true,
        };

        // 雙擊 tray icon → 把主視窗叫到前面
        _notifyIcon.DoubleClick += (_, _) =>
        {
            var win = System.Windows.Application.Current.MainWindow;
            if (win == null) return;
            if (win.WindowState == System.Windows.WindowState.Minimized)
                win.WindowState = System.Windows.WindowState.Normal;
            win.Activate();
        };
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var resource = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("AppIcon.ico", StringComparison.OrdinalIgnoreCase));
            if (resource != null)
            {
                using var stream = asm.GetManifestResourceStream(resource);
                if (stream != null) return new Icon(stream);
            }
            // fallback：用 exe 圖示
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                return Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
        }
        catch { }
        return SystemIcons.Application;
    }

    public void ShowInfo(string title, string message)
    {
        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(5000);
        }
        catch { /* ignore */ }
    }

    public void Dispose()
    {
        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        catch { }
    }
}
