using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace MizuLicenseManager;
public sealed class DiscordBackup
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly HttpClient client;
    public DiscordBackup(HttpClient? client = null) { this.client = client ?? Client; }
    public static bool ValidUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Host == "discord.com" && uri.IsDefaultPort && uri.UserInfo == "" &&
        uri.Query == "" && uri.Fragment == "" && Regex.IsMatch(uri.AbsolutePath, @"^/api(?:/v\d+)?/webhooks/\d+/[A-Za-z0-9_-]+$");
    public async Task SendAsync(Database db)
    {
        if (!ValidUrl(db.Webhook)) throw new InvalidOperationException("Discord Webhook URLが正しくありません。");
        var bytes = DataJson.Backup(db);
        if (db.EncryptBackup) bytes = await Task.Run(() => BackupEncryption.Encrypt(bytes, db.BackupPassphrase));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(db.EncryptBackup ? "application/octet-stream" : "application/json");
        form.Add(file, "files[0]", $"mizu-licenses-{DateTime.Now:yyyyMMdd-HHmmss}.{(db.EncryptBackup ? "mlicense" : "json")}");
        form.Add(new StringContent("{\"allowed_mentions\":{\"parse\":[]}}"), "payload_json");
        using var response = await client.PostAsync(db.Webhook + "?wait=true", form);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Discord送信失敗 (HTTP {(int)response.StatusCode})。設定を確認して再送してください。");
    }
}
