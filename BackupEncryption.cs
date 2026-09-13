using System.Security.Cryptography;
using System.Text;

namespace MizuLicenseManager;
public static class BackupEncryption
{
    // Versioned binary format: magic, random salt (16), nonce (12), tag (16), ciphertext.
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("MLICENSE\x01");
    private const int Iterations = 600_000;
    public static byte[] Encrypt(byte[] plaintext, string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase)) throw new InvalidOperationException("暗号化用のパスフレーズを設定してください。");
        var result = new byte[Header.Length + 44 + plaintext.Length];
        Header.CopyTo(result, 0);
        var salt = result.AsSpan(Header.Length, 16); RandomNumberGenerator.Fill(salt);
        var nonce = result.AsSpan(Header.Length + 16, 12); RandomNumberGenerator.Fill(nonce);
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, result.AsSpan(Header.Length + 44), result.AsSpan(Header.Length + 28, 16), Header);
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public static byte[] Decrypt(byte[] file, string passphrase)
    {
        if (file.Length < Header.Length + 44 || !file.AsSpan(0, Header.Length).SequenceEqual(Header)) throw new InvalidDataException("対応する.mlicenseファイルではありません。");
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, file.AsSpan(Header.Length, 16), Iterations, HashAlgorithmName.SHA256, 32);
        var plaintext = new byte[file.Length - Header.Length - 44];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(file.AsSpan(Header.Length + 16, 12), file.AsSpan(Header.Length + 44), file.AsSpan(Header.Length + 28, 16), plaintext, Header);
            return plaintext;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
