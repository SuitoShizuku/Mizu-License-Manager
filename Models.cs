using System.Text.Json;

namespace MizuLicenseManager;
public sealed class NamedEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}
public sealed class ExtraKey
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}
public sealed class License
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public string Email { get; set; } = "";
    public Guid? EmailId { get; set; }
    public List<Guid> DeviceIds { get; set; } = [];
    public bool Favorite { get; set; }
    public List<ExtraKey> ExtraKeys { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public string Star => Favorite ? "★" : "☆";
    public string AppPath { get; set; } = "";
    public string Developer { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool Active { get; set; } = true;
    public bool Temporary { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string StatusText => Temporary ? "一時的有効" : Active ? "現在有効" : "現在無効";
    public List<string> Devices { get; set; } = [];
    public List<string> Apps { get; set; } = [];
    public string Website { get; set; } = "";
    public List<string> ExtraUrls { get; set; } = [];
    public DateTime? Expires { get; set; }
    public DateTime Updated { get; set; } = DateTime.Now;
    [System.Text.Json.Serialization.JsonIgnore]
    public string Summary => $"{Developer}  ·  {StatusText}";
}
public sealed class Database
{
    public int Version { get; set; } = 1;
    public List<License> Licenses { get; set; } = [];
    public List<string> Emails { get; set; } = [];
    public List<NamedEntry> EmailEntries { get; set; } = [];
    public List<NamedEntry> DeviceEntries { get; set; } = [];
    public Dictionary<string, string> TagColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Devices { get; set; } = [];
    public string Webhook { get; set; } = "";
    public bool EncryptBackup { get; set; }
    public string BackupPassphrase { get; set; } = "";
    public string Theme { get; set; } = "System";
}
public static class DataJson
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!;
    public static byte[] Backup(Database db) => JsonSerializer.SerializeToUtf8Bytes(new Database
    { Licenses = db.Licenses, Emails = db.Emails, Devices = db.Devices, EmailEntries = db.EmailEntries, DeviceEntries = db.DeviceEntries, TagColors = db.TagColors, Webhook = db.Webhook }, Options);
    public static void Normalize(Database db)
    {
        NamedEntry Ensure(List<NamedEntry> entries, string name)
        { var entry = entries.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); if (entry == null) { entry = new NamedEntry { Name = name }; entries.Add(entry); } return entry; }
        if (db.EmailEntries.Count == 0) foreach (var value in db.Emails.Where(x => !string.IsNullOrWhiteSpace(x))) Ensure(db.EmailEntries, value);
        if (db.DeviceEntries.Count == 0) foreach (var value in db.Devices.Where(x => !string.IsNullOrWhiteSpace(x))) Ensure(db.DeviceEntries, value);
        foreach (var license in db.Licenses)
        {
            if (license.EmailId == null && license.Email.Length > 0) license.EmailId = Ensure(db.EmailEntries, license.Email).Id;
            if (license.EmailId is Guid id) license.Email = db.EmailEntries.FirstOrDefault(x => x.Id == id)?.Name ?? license.Email;
            if (license.DeviceIds.Count == 0) license.DeviceIds = license.Devices.Select(x => Ensure(db.DeviceEntries, x).Id).ToList();
            license.Devices = license.DeviceIds.Select(id => db.DeviceEntries.FirstOrDefault(x => x.Id == id)?.Name).Where(x => x != null).Cast<string>().ToList();
        }
        db.Emails = db.EmailEntries.Select(x => x.Name).ToList(); db.Devices = db.DeviceEntries.Select(x => x.Name).ToList();
        db.TagColors = new Dictionary<string, string>(db.TagColors, StringComparer.OrdinalIgnoreCase);
    }
    public static Database Parse(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("Version", out _) || !document.RootElement.TryGetProperty("Licenses", out _)) throw new InvalidDataException("Mizuのバックアップではありません。");
        var db = JsonSerializer.Deserialize<Database>(bytes, Options) ?? throw new InvalidDataException();
        if (db.Version != 1 || db.Licenses == null || db.Emails == null || db.Devices == null ||
            db.Emails.Any(x => x == null) || db.Devices.Any(x => x == null) ||
            db.Licenses.Any(x => x == null || string.IsNullOrWhiteSpace(x.Name) || x.Key == null || x.Email == null || x.AppPath == null || x.Developer == null || x.Notes == null || x.Website == null || x.Devices == null || x.Devices.Any(d => d == null)) ||
            db.Licenses.Any(x => x.Apps == null || x.Apps.Any(string.IsNullOrWhiteSpace)) ||
            db.Licenses.Select(x => x.Id).Distinct().Count() != db.Licenses.Count) throw new InvalidDataException("対応していないJSONです。");
        if (db.EmailEntries == null || db.DeviceEntries == null || db.TagColors == null ||
            db.EmailEntries.Concat(db.DeviceEntries).Any(x => x == null || string.IsNullOrWhiteSpace(x.Name)) ||
            db.EmailEntries.Select(x => x.Id).Distinct().Count() != db.EmailEntries.Count || db.DeviceEntries.Select(x => x.Id).Distinct().Count() != db.DeviceEntries.Count ||
            db.Licenses.Any(x => x.DeviceIds == null || x.ExtraKeys == null || x.ExtraKeys.Any(k => k == null || k.Name == null || k.Value == null))) throw new InvalidDataException();
        if (db.Licenses.Any(x => x.ExtraUrls == null || x.ExtraUrls.Any(url => url == null))) throw new InvalidDataException("追加URLが不正です。");
        if (db.TagColors.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value == null || !System.Text.RegularExpressions.Regex.IsMatch(x.Value, "^#[0-9A-Fa-f]{6}$"))) throw new InvalidDataException("タグ色が不正です。");
        Normalize(db); return db;
    }
}
