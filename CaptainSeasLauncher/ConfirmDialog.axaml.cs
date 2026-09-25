using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CaptainSeasLauncher;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string message, string confirmText = "Ja, verwijderen") : this()
    {
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Close(true);
}
