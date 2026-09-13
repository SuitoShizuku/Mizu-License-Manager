using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net.Mail;
using System.Windows.Controls.Primitives;

namespace MizuLicenseManager;
public sealed class SettingsWindow : Window
{
    public List<string> Emails { get; private set; } = [];
    public List<string> Devices { get; private set; } = [];
    public List<NamedEntry> EmailEntries { get; private set; } = [];
    public List<NamedEntry> DeviceEntries { get; private set; } = [];
    public string Webhook { get; private set; } = "";
    public bool EncryptBackup { get; private set; }
    public string BackupPassphrase { get; private set; } = "";
    public string Theme { get; private set; } = "System";
    public SettingsWindow(Database db)
    {
        Title = "設定"; Width = 650; Height = 720; MinWidth = 530; MinHeight = 550; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ThemeManager.Attach(this);
        WindowIcons.Attach(this);
        var panel = new StackPanel { Margin = new Thickness(25) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "設定", FontSize = 26, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "テーマ", Margin = new Thickness(0, 16, 0, 6) });
        var theme = new ComboBox { ItemsSource = new[] { "ライト", "ダーク", "OSに依存" }, SelectedIndex = db.Theme == "Light" ? 0 : db.Theme == "Dark" ? 1 : 2 }; panel.Children.Add(theme);
        theme.SelectionChanged += (_, _) => ThemeManager.Apply(theme.SelectedIndex == 0 ? "Light" : theme.SelectedIndex == 1 ? "Dark" : "System");
        Closed += (_, _) => { if (DialogResult != true) ThemeManager.Apply(db.Theme); };
        var normalized = DataJson.Clone(db); DataJson.Normalize(normalized);
        var emails = new EntryEditor("メールアドレス", "＋ 新規メールアドレス", normalized.EmailEntries); panel.Children.Add(emails);
        var devices = new EntryEditor("対象デバイス", "＋ 新規デバイス", normalized.DeviceEntries); panel.Children.Add(devices);
        panel.Children.Add(new TextBlock { Text = "Discord Webhook URL（空欄で自動送信を停止）", Margin = new Thickness(0, 20, 0, 8) });
        var webhook = new TextBox { Text = db.Webhook }; panel.Children.Add(webhook);
        var copyUrl = new Button { Content = "URLをコピー", HorizontalAlignment = HorizontalAlignment.Right }; copyUrl.Click += (_, _) => { try { if (webhook.Text.Length > 0) Clipboard.SetText(webhook.Text); } catch { MessageBox.Show(this, "コピーできませんでした。"); } }; panel.Children.Add(copyUrl);
        var encryption = new ToggleButton { IsChecked = db.EncryptBackup, Padding = new Thickness(16, 10, 16, 10), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 12) }; panel.Children.Add(encryption);
        encryption.SetResourceReference(StyleProperty, "EncryptionToggleStyle");
        var secrets = new StackPanel(); panel.Children.Add(secrets);
        secrets.Children.Add(new TextBlock { Text = "暗号化パスフレーズ", Margin = new Thickness(0, 4, 0, 6) });
        var passphrase = new PasswordBox { Password = db.BackupPassphrase }; secrets.Children.Add(passphrase);
        secrets.Children.Add(new TextBlock { Text = "パスフレーズ（確認）", Margin = new Thickness(0, 10, 0, 6) });
        var confirm = new PasswordBox { Password = db.BackupPassphrase }; secrets.Children.Add(confirm);
        var explanation = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 10, 0, 15) }; panel.Children.Add(explanation);
        explanation.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        void UpdateEncryption()
        {
            var enabled = encryption.IsChecked == true;
            encryption.Content = enabled ? "● 暗号化 ON" : "○ 暗号化 OFF";
            if (enabled) encryption.Background = new SolidColorBrush(Color.FromRgb(36, 94, 168)); else encryption.SetResourceReference(BackgroundProperty, "SurfaceBrush");
            if (enabled) encryption.Foreground = Brushes.White; else encryption.SetResourceReference(ForegroundProperty, "TextBrush");
            secrets.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            explanation.Text = enabled
                ? "全件バックアップをパスフレーズで暗号化し、.mlicenseとして送信します。読み込みにも暗号化時のパスフレーズが必要です。パスフレーズはPC内で暗号化保存され、送信されません。変更前のファイルには以前のパスフレーズが必要です。"
                : "保存・削除・復元のたびに全件を平文の.jsonで送信します。ライセンスキー・メール・メモを送信先の閲覧者が読めます。Webhook URLとパスフレーズはバックアップに含みません。";
        }
        encryption.Checked += (_, _) => UpdateEncryption(); encryption.Unchecked += (_, _) => UpdateEncryption(); UpdateEncryption();
        var save = new Button { Content = "設定を保存", HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(save);
        save.Click += (_, _) =>
        {
            var addressList = emails.Values().Select(x => x.Name).ToList();
            if (normalized.EmailEntries.Any(old => !emails.Values().Any(x => x.Id == old.Id)) || normalized.DeviceEntries.Any(old => !devices.Values().Any(x => x.Id == old.Id))) { MessageBox.Show(this, "登録済みの項目は空欄にできません。名前を入力してください。"); return; }
            if (addressList.Distinct(StringComparer.OrdinalIgnoreCase).Count() != addressList.Count || devices.Values().Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != devices.Values().Count) { MessageBox.Show(this, "同じ名前の項目が重複しています。"); return; }
            if (addressList.Any(x => !MailAddress.TryCreate(x, out var address) || address.Address != x)) { MessageBox.Show(this, "メールアドレスの形式を確認してください。"); return; }
            var url = webhook.Text.Trim(); if (url.Length > 0 && !DiscordBackup.ValidUrl(url)) { MessageBox.Show(this, "https://discord.com/api/webhooks/ で始まるWebhook URLを入力してください。"); return; }
            if (encryption.IsChecked == true && (string.IsNullOrWhiteSpace(passphrase.Password) || passphrase.Password != confirm.Password)) { MessageBox.Show(this, "空欄ではないパスフレーズを入力し、確認欄と一致させてください。"); return; }
            Theme = theme.SelectedIndex == 0 ? "Light" : theme.SelectedIndex == 1 ? "Dark" : "System";
            EmailEntries = emails.Values(); DeviceEntries = devices.Values();
            Emails = addressList; Devices = DeviceEntries.Select(x => x.Name).ToList(); Webhook = url; EncryptBackup = encryption.IsChecked == true; BackupPassphrase = EncryptBackup ? passphrase.Password : db.BackupPassphrase; DialogResult = true;
        };
    }
    private static List<string> Split(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
