using System.Windows;
using System.Windows.Controls;

namespace MizuLicenseManager;
public sealed class PassphraseWindow : Window
{
    private readonly PasswordBox passphrase = new();
    public string Passphrase => passphrase.Password;
    public PassphraseWindow()
    {
        Title = "暗号化バックアップの読み込み"; Width = 460; SizeToContent = SizeToContent.Height;
        ThemeManager.Attach(this);
        WindowIcons.Attach(this);
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "このファイルを暗号化したパスフレーズ", Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(passphrase);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "キャンセル", IsCancel = true }; buttons.Children.Add(cancel);
        var ok = new Button { Content = "読み込む", IsDefault = true }; buttons.Children.Add(ok); panel.Children.Add(buttons);
        ok.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(Passphrase)) DialogResult = true; };
        Loaded += (_, _) => passphrase.Focus();
    }
}
