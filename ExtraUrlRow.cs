using System.Windows;
using System.Windows.Controls;

namespace MizuLicenseManager;
public sealed class ExtraUrlRow : DockPanel
{
    private readonly TextBox input = new();
    public string Value => input.Text.Trim();
    public ExtraUrlRow(string value, Action changed, Action remove, Action<string> open, Action<string> copy)
    {
        Margin = new Thickness(0, 5, 0, 0);
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; SetDock(actions, Dock.Right); Children.Add(actions);
        foreach (var (label, action) in new (string, Action)[] { ("開く", () => open(Value)), ("コピー", () => copy(input.Text)), ("×", remove) })
        { var button = new Button { Content = label }; button.Click += (_, _) => action(); actions.Children.Add(button); }
        input.Text = value; input.ToolTip = "追加の製品・管理ページURL"; input.TextChanged += (_, _) => changed(); Children.Add(input);
    }
}
