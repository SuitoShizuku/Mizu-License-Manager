using MizuLicenseManager;
using System.Net;
using System.Text;

var directory = Path.Combine(Path.GetTempPath(), "mizu-tests-" + Guid.NewGuid());
int checks = 0;
void Assert(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
try
{
    var store = new Storage(directory);
    Assert(store.Load().Licenses.Count == 0, "empty vault");
    var db = new Database { Emails = ["test@example.com"], Devices = ["Desktop", "Laptop"], Webhook = "https://discord.com/api/webhooks/123/test-token" };
    db.Licenses.Add(new License { Name = "日本語アプリ", Key = "SECRET-TEST-LICENSE", Devices = ["Desktop", "Laptop"], Notes = "複数行\nメモ" });
    store.Save(db);
    var bytes = File.ReadAllBytes(Path.Combine(directory, "vault.dat"));
    Assert(!Encoding.UTF8.GetString(bytes).Contains("SECRET-TEST-LICENSE"), "vault encrypted");
    var loaded = store.Load();
    Assert(loaded.Licenses[0].Key == db.Licenses[0].Key && loaded.Licenses[0].Devices.Count == 2 && loaded.Webhook == db.Webhook, "DPAPI round trip");
    db.Licenses.Add(new License { Name = "Second", Active = false }); store.Save(db);
    Assert(File.Exists(Path.Combine(directory, "vault.dat.bak")) && store.Load().Licenses.Count == 2, "atomic replacement and previous backup");
    var backup = DataJson.Backup(db);
    Assert(!Encoding.UTF8.GetString(backup).Contains("test-token"), "webhook secret excluded");
    Assert(DataJson.Parse(backup).Licenses.Count == 2 && DataJson.Parse(backup).Licenses[0].Key == "SECRET-TEST-LICENSE", "all records exported and restorable");
    bool rejected = false; try { DataJson.Parse(Encoding.UTF8.GetBytes("{}")); } catch { rejected = true; }
    Assert(rejected, "unrelated JSON rejected");
    var invalid = DataJson.Clone(db); invalid.Licenses[0].Key = null!;
    rejected = false; try { DataJson.Parse(DataJson.Backup(invalid)); } catch { rejected = true; }
    Assert(rejected, "null license fields rejected before restore");
    Assert(DiscordBackup.ValidUrl(db.Webhook) && !DiscordBackup.ValidUrl("https://discord.com.evil.test/api/webhooks/1/token") && !DiscordBackup.ValidUrl("http://discord.com/api/webhooks/1/token"), "webhook destination validation");
    var handler = new CaptureHandler(); using var client = new HttpClient(handler); var service = new DiscordBackup(client);
    await service.SendAsync(db);
    Assert(handler.Uri!.Query == "?wait=true" && handler.Body.Contains("application/json") && handler.Body.Contains("SECRET-TEST-LICENSE") && handler.Body.Contains("Second"), "multipart single JSON upload with confirmation");
    handler.Code = HttpStatusCode.TooManyRequests;
    rejected = false; try { await service.SendAsync(db); } catch (InvalidOperationException) { rejected = true; }
    Assert(rejected && store.Load().Licenses.Count == 2, "Discord failure preserves local data");
    var phrase = "テスト用 passphrase 123";
    var encrypted = BackupEncryption.Encrypt(backup, phrase);
    Assert(BackupEncryption.Decrypt(encrypted, phrase).SequenceEqual(backup), "encrypted backup round trip");
    Assert(!encrypted.SequenceEqual(BackupEncryption.Encrypt(backup, phrase)), "random salt and nonce per backup");
    rejected = false; try { BackupEncryption.Decrypt(encrypted, "wrong"); } catch (System.Security.Cryptography.CryptographicException) { rejected = true; }
    Assert(rejected, "wrong passphrase rejected");
    var tampered = (byte[])encrypted.Clone(); tampered[^1] ^= 1;
    rejected = false; try { BackupEncryption.Decrypt(tampered, phrase); } catch (System.Security.Cryptography.CryptographicException) { rejected = true; }
    Assert(rejected, "modified ciphertext rejected");
    rejected = false; try { BackupEncryption.Decrypt([1, 2, 3], phrase); } catch (InvalidDataException) { rejected = true; }
    Assert(rejected, "truncated encrypted file rejected");
    rejected = false; try { BackupEncryption.Encrypt(backup, " "); } catch (InvalidOperationException) { rejected = true; }
    Assert(rejected, "empty encryption passphrase rejected");
    db.EncryptBackup = true; db.BackupPassphrase = phrase; handler.Code = HttpStatusCode.OK;
    await service.SendAsync(db);
    Assert(handler.Body.Contains(".mlicense") && handler.Body.Contains("application/octet-stream") && !handler.Body.Contains("SECRET-TEST-LICENSE") && !handler.Body.Contains(phrase), "encrypted webhook attachment without plaintext");
    Assert(DataJson.Parse(DataJson.Backup(db)).BackupPassphrase == "", "passphrase excluded from backups");
    store.Save(db); Assert(store.Load().EncryptBackup && store.Load().BackupPassphrase == phrase, "encryption settings preserved in local vault");
    var oldRecord = DataJson.Parse(backup).Licenses[1]; Assert(!oldRecord.Active && !oldRecord.Temporary && oldRecord.StatusText == "現在無効", "legacy status remains compatible");
    db.Licenses[0].Temporary = true; db.Licenses[0].Expires = new DateTime(2027, 1, 1);
    Assert(DataJson.Parse(DataJson.Backup(db)).Licenses[0].StatusText == "一時的有効", "temporary status round trip");
    db.Licenses[0].Apps = ["After Effects", "Photoshop"]; db.Theme = "Dark"; store.Save(db);
    Assert(store.Load().Theme == "Dark" && store.Load().Licenses[0].Apps.Count == 2, "theme and app tags persist locally");
    Assert(DataJson.Parse(DataJson.Backup(db)).Licenses[0].Apps.Contains("After Effects"), "app tags included in backup restore");
    db.Licenses[0].Email = "old@example.com"; db.Licenses[0].EmailId = null;
    DataJson.Normalize(db); var mailId = db.Licenses[0].EmailId; var deviceId = db.Licenses[0].DeviceIds[0];
    db.EmailEntries.First(x => x.Id == mailId).Name = "renamed@example.com";
    db.DeviceEntries.First(x => x.Id == deviceId).Name = "Renamed desktop";
    db.EmailEntries.Reverse(); db.DeviceEntries.Reverse(); DataJson.Normalize(db);
    Assert(db.Licenses[0].Email == "renamed@example.com" && db.Licenses[0].Devices[0] == "Renamed desktop", "stable references survive rename and reordering");
    db.Licenses[0].Favorite = true; db.Licenses[0].ExtraKeys = [new ExtraKey { Name = "Serial", Value = "EXTRA-SECRET" }, new ExtraKey { Name = "Activation", Value = "SECOND-SECRET" }]; db.TagColors["After Effects"] = "#7544A2";
    store.Save(db); var restored = DataJson.Parse(BackupEncryption.Decrypt(BackupEncryption.Encrypt(DataJson.Backup(db), phrase), phrase));
    Assert(restored.Licenses[0].Favorite && restored.Licenses[0].ExtraKeys.Count == 2 && restored.Licenses[0].ExtraKeys[1].Value == "SECOND-SECRET" && restored.TagColors["After Effects"] == "#7544A2", "favorites extra keys and tag colors survive encrypted restore");
    Assert(restored.Licenses[0].EmailId == mailId && restored.Licenses[0].DeviceIds[0] == deviceId, "stable reference IDs survive backup restore");
    db.Licenses[0].ExtraUrls = ["https://example.com/product", "https://example.com/account"];
    var urlsRestored = DataJson.Parse(BackupEncryption.Decrypt(BackupEncryption.Encrypt(DataJson.Backup(db), phrase), phrase));
    Assert(urlsRestored.Licenses[0].ExtraUrls.SequenceEqual(db.Licenses[0].ExtraUrls), "additional URLs survive encrypted backup restore");
    bytes[bytes.Length / 2] ^= 0xFF; File.WriteAllBytes(Path.Combine(directory, "vault.dat"), bytes);
    rejected = false; try { store.Load(); } catch { rejected = true; }
    Assert(rejected, "corrupt vault fails closed");
    Console.WriteLine($"{checks} checks passed.");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

sealed class CaptureHandler : HttpMessageHandler
{
    public string Body = ""; public Uri? Uri; public HttpStatusCode Code = HttpStatusCode.OK;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    { Uri = request.RequestUri; Body = await request.Content!.ReadAsStringAsync(token); return new HttpResponseMessage(Code) { Content = new StringContent("{}") }; }
}
