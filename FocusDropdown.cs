using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MizuLicenseManager;
public static class FocusDropdown
{
    public static void Attach(ComboBox input)
    {
        input.GotKeyboardFocus += (_, e) =>
        {
            // Selecting a popup item returns focus to the editor; do not reopen it then.
            if (BelongsTo(input, e.OldFocus as DependencyObject)) return;
            input.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            { if (input.IsKeyboardFocusWithin && input.IsEnabled) input.IsDropDownOpen = true; }));
        };
    }
    private static bool BelongsTo(ComboBox input, DependencyObject? node)
    {
        while (node != null)
        {
            if (node == input || node is ComboBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) == input) return true;
            node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }
}
