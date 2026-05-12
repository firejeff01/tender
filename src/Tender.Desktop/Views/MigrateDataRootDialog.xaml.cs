using System.Windows;
using Tender.Desktop.Services;

namespace Tender.Desktop.Views;

public partial class MigrateDataRootDialog : Window
{
    public MigrateChoice Result { get; private set; } = MigrateChoice.Cancel;

    public MigrateDataRootDialog()
    {
        InitializeComponent();
    }

    public void Initialize(string oldRoot, string newRoot)
    {
        OldRootText.Text = oldRoot;
        NewRootText.Text = newRoot;
    }

    private void Migrate_Click(object sender, RoutedEventArgs e)
    {
        Result = MigrateChoice.Migrate;
        DialogResult = true;
        Close();
    }

    private void Keep_Click(object sender, RoutedEventArgs e)
    {
        Result = MigrateChoice.Keep;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = MigrateChoice.Cancel;
        DialogResult = false;
        Close();
    }
}
