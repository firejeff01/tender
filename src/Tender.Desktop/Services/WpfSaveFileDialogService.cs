using Microsoft.Win32;

namespace Tender.Desktop.Services;

public sealed class WpfSaveFileDialogService : ISaveFileDialogService
{
    public string? ShowSaveAsXlsx(string suggestedFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 活頁簿 (*.xlsx)|*.xlsx",
            DefaultExt = ".xlsx",
            FileName = suggestedFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
