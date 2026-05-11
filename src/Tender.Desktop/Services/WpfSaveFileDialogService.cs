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

    public string? ShowSaveAsXlsm(string suggestedFileName)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel 啟用巨集的活頁簿 (*.xlsm)|*.xlsm",
            DefaultExt = ".xlsm",
            FileName = suggestedFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
