using Avalonia.Interactivity;

namespace FFDecsaSharp.Gui.Views;

public partial class ConfirmDialog : ShadUI.Window
{
    public ConfirmDialog() => InitializeComponent();

    public ConfirmDialog(string message, string yesText, string noText) : this()
    {
        MessageText.Text = message;
        YesButton.Content = yesText;
        NoButton.Content = noText;
        YesButton.Click += (_, _) => Close(true);
        NoButton.Click += (_, _) => Close(false);
    }
}
