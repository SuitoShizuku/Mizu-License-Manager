using System.Windows;
using System.Windows.Controls;

namespace MizuLicenseManager;
public sealed class ExtraKeyRow : Grid
{
    private readonly TextBox label = new(), visible = new() { Visibility = Visibility.Collapsed };
    private readonly PasswordBox secret = new();
    private readonly Button reveal = new() { Content = "表示", Padding = new Thickness(7), Width = 58 };
    public ExtraKey Value => new() { Name = label.Text, Value = secret.Password };
    public ExtraKeyRow(ExtraKey value, Action changed, Action remove, Action<string> copy)
    {
        Margin = new Thickness(0, 5, 0, 0);
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        label.Text = value.Name; label.ToolTip = "追加ライセンス名"; Children.Add(label);
        label.MinWidth = 0; visible.MinWidth = 0; secret.MinWidth = 0;
        var values = new Grid(); Grid.SetColumn(values, 1); Children.Add(values); values.Children.Add(secret); values.Children.Add(visible); secret.Password = value.Value; visible.Text = value.Value;
        var actions = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(actions, 2); Children.Add(actions); actions.Children.Add(reveal);
        reveal.Click += (_, _) => { var show = visible.Visibility != Visibility.Visible; visible.Visibility = show ? Visibility.Visible : Visibility.Collapsed; secret.Visibility = show ? Visibility.Collapsed : Visibility.Visible; reveal.Content = show ? "非表示" : "表示"; };
        var copyValue = new Button { Content = "コピー", Padding = new Thickness(7) }; copyValue.Click += (_, _) => copy(secret.Password); actions.Children.Add(copyValue);
        var delete = new Button { Content = "×", Padding = new Thickness(7), ToolTip = "この行を削除" }; delete.Click += (_, _) => remove(); actions.Children.Add(delete);
        var menu = new ContextMenu(); var copyName = new MenuItem { Header = "名前をコピー" }; copyName.Click += (_, _) => copy(label.Text); menu.Items.Add(copyName); label.ContextMenu = menu;
        label.TextChanged += (_, _) => changed();
        secret.PasswordChanged += (_, _) => { if (visible.Text != secret.Password) visible.Text = secret.Password; changed(); };
        visible.TextChanged += (_, _) => { if (secret.Password != visible.Text) secret.Password = visible.Text; changed(); };
    }
    public void HideValue() { visible.Visibility = Visibility.Collapsed; secret.Visibility = Visibility.Visible; reveal.Content = "表示"; }
}
