// 因專案啟用了 UseWindowsForms（為了 NotifyIcon），WinForms 與 WPF 多處型別同名。
// 統一在此處 alias 為 WPF 端，避免每個檔案都要寫 fully-qualified name。
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using Brushes = System.Windows.Media.Brushes;
global using FontFamily = System.Windows.Media.FontFamily;
global using Color = System.Windows.Media.Color;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using UserControl = System.Windows.Controls.UserControl;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
