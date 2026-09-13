using System.Windows;
using System.Threading;

namespace MizuLicenseManager;
public partial class App : Application
{
    private Mutex? instance;
    private System.Windows.Forms.NotifyIcon? tray;
    private System.Drawing.Icon? appIcon;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, "Local\\MizuLicenseManager", out var first);
        if (!first) { MessageBox.Show("Mizu License Manager は既に起動しています。"); Shutdown(); return; }
        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new MainWindow(); MainWindow = window;
            appIcon = WindowIcons.Load("White", 32);
            tray = new System.Windows.Forms.NotifyIcon { Text = "Mizu License Manager", Icon = appIcon, Visible = true };
            void Restore() => Dispatcher.Invoke(() => { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); });
            tray.MouseClick += (_, args) => { if (args.Button == System.Windows.Forms.MouseButtons.Left) Restore(); };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("ウィンドウを表示", null, (_, _) => Restore()); tray.ContextMenuStrip = menu;
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("終了", null, (_, _) => Dispatcher.Invoke(() => { if (window.PrepareToExit()) Shutdown(); }));
            window.Show();
        }
        catch { MessageBox.Show("保存データを開けませんでした。データを上書きせず終了します。READMEの復旧手順を確認してください。", "起動エラー"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { tray?.Dispose(); appIcon?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
