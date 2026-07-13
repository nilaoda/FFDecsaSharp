using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace FFDecsaSharp.Gui.Services;

/// <summary>
/// Adds a localized native-style edit menu to every text box that does not provide its own menu.
/// </summary>
internal static class TextBoxContextMenuService
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        Control.LoadedEvent.AddClassHandler<TextBox>((textBox, _) => Attach(textBox));
    }

    private static void Attach(TextBox textBox)
    {
        if (textBox.ContextMenu is not null)
        {
            return;
        }

        KeyModifiers shortcutModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        MenuItem cut = CreateMenuItem(textBox, L.TextBoxMenu_Cut, new KeyGesture(Key.X, shortcutModifier), textBox.Cut);
        MenuItem copy = CreateMenuItem(textBox, L.TextBoxMenu_Copy, new KeyGesture(Key.C, shortcutModifier), textBox.Copy);
        MenuItem paste = CreateMenuItem(textBox, L.TextBoxMenu_Paste, new KeyGesture(Key.V, shortcutModifier), textBox.Paste);
        MenuItem selectAll = CreateMenuItem(textBox, L.TextBoxMenu_SelectAll, new KeyGesture(Key.A, shortcutModifier), textBox.SelectAll);
        var menu = new ContextMenu
        {
            Items = { cut, copy, paste, selectAll },
        };

        menu.Opening += async (_, _) =>
        {
            FocusTextBox(textBox);
            RefreshHeaders(cut, copy, paste, selectAll);

            bool hasSelection = textBox.SelectionStart != textBox.SelectionEnd;
            bool hasText = !string.IsNullOrEmpty(textBox.Text);
            bool canEdit = textBox.IsEnabled && !textBox.IsReadOnly;
            cut.IsEnabled = canEdit && hasSelection;
            copy.IsEnabled = textBox.IsEnabled && hasSelection;
            paste.IsEnabled = false;
            selectAll.IsEnabled = textBox.IsEnabled && hasText;
            if (!canEdit)
            {
                return;
            }

            var clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            try
            {
                paste.IsEnabled = !string.IsNullOrEmpty(await clipboard.TryGetTextAsync());
            }
            catch
            {
                paste.IsEnabled = false;
            }
        };

        textBox.ContextMenu = menu;
    }

    private static MenuItem CreateMenuItem(TextBox textBox, string header, KeyGesture inputGesture, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGesture = inputGesture,
        };
        item.Click += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            FocusTextBox(textBox);
            action();
        });
        return item;
    }

    private static void FocusTextBox(TextBox textBox)
    {
        if (textBox.IsEnabled)
        {
            textBox.Focus(NavigationMethod.Pointer, KeyModifiers.None);
        }
    }

    private static void RefreshHeaders(MenuItem cut, MenuItem copy, MenuItem paste, MenuItem selectAll)
    {
        cut.Header = L.TextBoxMenu_Cut;
        copy.Header = L.TextBoxMenu_Copy;
        paste.Header = L.TextBoxMenu_Paste;
        selectAll.Header = L.TextBoxMenu_SelectAll;
    }
}
