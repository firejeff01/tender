using System.Windows;
using Tender.Desktop.Views;

namespace Tender.Desktop.Services;

public sealed class WpfMigrateDataRootDialog : IMigrateDataRootDialog
{
    public MigrateChoice Ask(string oldRoot, string newRoot)
    {
        var dlg = new MigrateDataRootDialog
        {
            Owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow,
        };
        dlg.Initialize(oldRoot, newRoot);
        dlg.ShowDialog();
        return dlg.Result;
    }
}
