using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MizuLicenseManager;
public static class WindowIcons
{
    public static Uri Resource(string tone) => new($"pack://application:,,,/Mizu-License-Manager;component/Assets/Mizu-{tone}.ico");
    public static System.Drawing.Icon Load(string tone, int size)
    {
        using var stream = Application.GetResourceStream(Resource(tone)).Stream;
        using var icon = new System.Drawing.Icon(stream, size, size);
        return (System.Drawing.Icon)icon.Clone();
    }
    public static void Attach(Window window)
    {
        window.Icon = BitmapFrame.Create(Resource("Black"));
        System.Drawing.Icon? small = null, large = null;
        void Apply()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            small ??= Load("Black", 32); large ??= Load("White", 256);
            SendMessage(handle, 0x80, IntPtr.Zero, small.Handle); // ICON_SMALL: caption
            SendMessage(handle, 0x80, new IntPtr(1), large.Handle); // ICON_BIG: taskbar / Alt+Tab
        }
        window.SourceInitialized += (_, _) => Apply();
        window.Loaded += (_, _) => Apply();
        window.Closed += (_, _) => { small?.Dispose(); large?.Dispose(); };
    }
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
