using System.Diagnostics;

namespace MizuLicenseManager;
public static class OpenTarget
{
    public static string WebAddress(string value)
    {
        var text = value.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host))
            throw new ArgumentException("http:// または https:// で始まるURLを入力してください。");
        return uri.AbsoluteUri;
    }
    public static void Website(string value) => Process.Start(new ProcessStartInfo(WebAddress(value)) { UseShellExecute = true });
    public static void LocalPath(string value)
    {
        var path = value.Trim();
        if (path.Length == 0 || (!File.Exists(path) && !Directory.Exists(path))) throw new ArgumentException("ファイルまたはフォルダが見つかりません。パスを確認してください。");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
