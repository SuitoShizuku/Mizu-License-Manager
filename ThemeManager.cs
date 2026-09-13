using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace MizuLicenseManager;
public static class ThemeManager
{
    public static string Mode { get; private set; } = "System";
    private static bool subscribed;
    public static void Apply(string mode)
    {
        Mode = mode is "Light" or "Dark" ? mode : "System";
        if (!subscribed) { SystemEvents.UserPreferenceChanged += OnPreferenceChanged; subscribed = true; }
        var dark = Mode == "Dark" || (Mode == "System" && IsSystemDark());
        var resources = Application.Current.Resources;
        void Brush(string name, string light, string night) => resources[name] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? night : light));
        Brush("PageBrush", "#F3F6FA", "#141922"); Brush("SurfaceBrush", "#FFFFFF", "#202735");
        Brush("TextBrush", "#1B2638", "#E6EDF7"); Brush("MutedBrush", "#53647C", "#AFBED3");
        Brush("BorderBrush", "#CBD5E1", "#46536A"); Brush("ChipBrush", "#E5EEFF", "#2D4264");
        Brush("AccentBrush", "#2360BE", "#3478D4");
        resources[SystemColors.WindowBrushKey] = resources["SurfaceBrush"];
        resources[SystemColors.WindowTextBrushKey] = resources["TextBrush"];
        resources[SystemColors.ControlBrushKey] = resources["SurfaceBrush"];
        resources[SystemColors.ControlTextBrushKey] = resources["TextBrush"];
        resources[SystemColors.HighlightBrushKey] = resources["AccentBrush"];
    }
    private static bool IsSystemDark()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
        catch { return false; }
    }
    private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    { if (Mode == "System") Application.Current?.Dispatcher.BeginInvoke(() => Apply("System")); }
    public static void Attach(Window window)
    {
        window.SetResourceReference(Window.BackgroundProperty, "PageBrush");
        window.SetResourceReference(Window.ForegroundProperty, "TextBrush");
    }
}
