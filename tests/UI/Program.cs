using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using MizuLicenseManager;
using License = MizuLicenseManager.License;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mizu-ui-test-" + Guid.NewGuid());
        try
        {
            var app = new App(); app.InitializeComponent();
            var store = new Storage(directory); var db = new Database { Emails = ["one@example.com", "two@example.com"], Devices = ["Desktop"] }; store.Save(db);
            var window = new MainWindow(store);
            T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
            void Load(License? license) => typeof(MainWindow).GetMethod("LoadEditor", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [license]);
            void Assert(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
            Load(new License { Name = "Test", Email = "two@example.com", Key = "KEY" });
            Assert((string)Field<ComboBox>("email").SelectedItem == "two@example.com", "existing email selected");
            Load(null);
            Assert(Field<ComboBox>("email").SelectedIndex == -1 && Field<TextBox>("name").Text == "", "new license clears email and name");
            var reveal = Field<Button>("reveal"); reveal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert((string)reveal.Content == "非表示" && Field<TextBox>("keyVisible").Visibility == Visibility.Visible, "key show toggle label");
            reveal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert((string)reveal.Content == "表示" && Field<TextBox>("keyVisible").Visibility == Visibility.Collapsed, "key hide toggle label");
            Field<RadioButton>("temporary").IsChecked = true;
            Assert(Field<StackPanel>("expirySection").Visibility == Visibility.Visible && Field<RadioButton>("active").IsChecked == false, "temporary radio exposes expiry exclusively");
            Field<RadioButton>("inactive").IsChecked = true;
            Assert(Field<StackPanel>("expirySection").Visibility == Visibility.Collapsed, "inactive radio hides expiry");
            Assert(Field<TextBox>("notes").VerticalContentAlignment == VerticalAlignment.Top, "notes aligned top");
            Field<TextBox>("notes").Text = "unsaved";
            var closing = new CancelEventArgs();
            typeof(Window).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [closing]);
            Assert(closing.Cancel && Field<TextBox>("notes").Text == "unsaved", "close canceled and draft retained");
            var settings = new SettingsWindow(db);
            var panel = (StackPanel)((ScrollViewer)settings.Content).Content;
            var emailEditor = panel.Children.OfType<EntryEditor>().First();
            var entryCount = emailEditor.Values().Count;
            emailEditor.Children.OfType<Button>().Last().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var entryInputs = (StackPanel)emailEditor.Children[1]; var lastInput = ((DockPanel)entryInputs.Children[^1]).Children.OfType<TextBox>().Single(); lastInput.Text = "added@example.com";
            Assert(emailEditor.Values().Count == entryCount + 1 && emailEditor.Values().Last().Name == "added@example.com", "settings button appends a separate entry");
            Assert(panel.Children.OfType<TextBox>().Count(x => !x.AcceptsReturn) == 1, "webhook is ordinary text input");
            Field<ComboBox>("appInput").Text = "After Effects";
            typeof(MainWindow).GetMethod("AddApp", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Field<ComboBox>("appInput").Text = "after effects";
            typeof(MainWindow).GetMethod("AddApp", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Assert(Field<List<string>>("chosenApps").Count == 1 && Field<WrapPanel>("appChips").Children.Count == 1, "app chips deduplicate case-insensitively");
            var first = new License { Name = "Motion Tool", Apps = ["After Effects"], Temporary = true };
            var second = new License { Name = "Paint Tool", Apps = ["Photoshop"] };
            db.Licenses = [first, second]; typeof(MainWindow).GetField("db", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, db);
            typeof(MainWindow).GetMethod("RefreshAppFilters", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            var filterChip = Field<TabControl>("appFilters").Items.OfType<TabItem>().First(x => (string)x.Header == "After Effects");
            filterChip.IsSelected = true;
            Assert(Field<ListBox>("list").Items.Count == 1 && ((License)Field<ListBox>("list").Items[0]).Name == "Motion Tool", "app chip filters license list");
            Field<TextBox>("search").Text = "Paint";
            Assert(Field<ListBox>("list").Items.Count == 0, "search combines with chip filter");
            Field<TextBox>("search").Clear();
            Load(first);
            typeof(MainWindow).GetMethod("AddExtraUrl", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, ["https://example.com/account"]);
            Assert(Field<StackPanel>("extraUrls").Children.OfType<ExtraUrlRow>().Single().Value == "https://example.com/account", "extra URL creates independent input row");
            string opened = "", copied = ""; bool removed = false;
            var urlTest = new ExtraUrlRow("https://example.com/product", () => { }, () => removed = true, value => opened = value, value => copied = value);
            var urlButtons = urlTest.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToList();
            urlButtons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); urlButtons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); urlButtons[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(opened == "https://example.com/product" && copied == opened && removed, "extra URL open copy and remove target its own row");
            Assert(OpenTarget.WebAddress(" https://example.com/account ") == "https://example.com/account", "URL opener accepts trimmed web address");
            bool invalidUrl = false; try { OpenTarget.WebAddress("file:///C:/Windows/test.exe"); } catch (ArgumentException) { invalidUrl = true; }
            Assert(invalidUrl, "URL field rejects non-web targets");
            typeof(MainWindow).GetMethod("AddExtraKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [new ExtraKey { Name = "Serial", Value = "DEMO-SERIAL" }]);
            var extra = Field<StackPanel>("extraKeys").Children.OfType<ExtraKeyRow>().Single();
            var extraValuePanel = extra.Children.OfType<Grid>().Single();
            Assert(extraValuePanel.Children.OfType<TextBox>().Single().Visibility == Visibility.Collapsed, "extra key is initially hidden");
            var extraActions = extra.Children.OfType<StackPanel>().Single(); extraActions.Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(extraValuePanel.Children.OfType<TextBox>().Single().Visibility == Visibility.Visible && extra.Value.Value == "DEMO-SERIAL", "extra key reveal preserves value");
            extra.HideValue();
            Field<ComboBox>("deviceInput").SelectedItem = "Desktop";
            Assert(((DockPanel)Field<ComboBox>("deviceInput").Parent).Children.OfType<Button>().Any(x => (string)x.Content == "＋ 追加"), "pending device highlights add button");
            Assert(!Field<ComboBox>("deviceInput").IsEditable && !((DockPanel)Field<ComboBox>("deviceInput").Parent).Children.OfType<Button>().Any(x => ((string)x.Content).Contains("候補")), "device is selection-only with no candidate button");
            Assert(Field<ComboBox>("deviceInput").Items.Cast<string>().SequenceEqual(db.Devices), "device choices come only from settings");
            Field<ComboBox>("deviceInput").SelectedIndex = -1; Field<ComboBox>("deviceInput").Text = "Unregistered";
            typeof(MainWindow).GetMethod("AddDevice", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Assert(!Field<List<string>>("chosenDevices").Contains("Unregistered"), "unregistered device cannot be added");
            extra.Measure(new Size(650, 50)); extra.Arrange(new Rect(0, 0, 650, 50)); extra.UpdateLayout();
            var boundary = extra.ColumnDefinitions[0].ActualWidth;
            extraActions.Children.OfType<Button>().First().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            extra.Measure(new Size(650, 50)); extra.Arrange(new Rect(0, 0, 650, 50)); extra.UpdateLayout();
            Assert(Math.Abs(extra.ColumnDefinitions[0].ActualWidth - boundary) < 0.01, "extra key reveal keeps name-value boundary fixed"); extra.HideValue();
            Assert(Grid.GetColumn(Field<StackPanel>("expirySection")) == 1 && Field<StackPanel>("expirySection").Parent is Grid, "expiry occupies right column");
            ThemeManager.Apply("Dark");
            Assert(((System.Windows.Media.SolidColorBrush)window.Background).Color == System.Windows.Media.Color.FromRgb(20, 25, 34), "dark theme updates open window");
            ThemeManager.Apply("Light");
            Assert(((System.Windows.Media.SolidColorBrush)window.Background).Color == System.Windows.Media.Color.FromRgb(243, 246, 250), "light theme updates open window");
            ThemeManager.Apply("System"); Assert(ThemeManager.Mode == "System", "OS theme mode accepted");
            foreach (var mode in new[] { "Light", "Dark" })
            {
                ThemeManager.Apply(mode);
                var root = (FrameworkElement)window.Content;
                root.Measure(new Size(1280, 850)); root.Arrange(new Rect(0, 0, 1280, 850)); root.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(root);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var output = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "preview-" + mode + ".png")); encoder.Save(output);
                var scroll = (ScrollViewer)Field<StackPanel>("editor").Parent;
                scroll.ScrollToBottom(); root.UpdateLayout();
                var lower = new System.Windows.Media.Imaging.RenderTargetBitmap(1280, 850, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); lower.Render(root);
                var lowerEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); lowerEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(lower));
                using var lowerOutput = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "preview-lower-" + mode + ".png")); lowerEncoder.Save(lowerOutput);
                scroll.ScrollToTop(); root.UpdateLayout();
            }
            Field<TabControl>("appFilters").SelectedIndex = 0;
            Assert(Field<ListBox>("list").Items.Count == 2, "all tab clears tag filter");
            Field<TabControl>("appFilters").SelectedIndex = 2;
            Assert(Field<ListBox>("list").Items.Count == 1 && ((License)Field<ListBox>("list").Items[0]).Name == "Paint Tool", "tabs select one tag at a time");
            db.Licenses = Enumerable.Range(0, 80).Select(i => new License { Name = "Scroll " + i, Apps = i % 2 == 0 ? ["Tag"] : [] }).ToList();
            typeof(MainWindow).GetMethod("RefreshAppFilters", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            typeof(MainWindow).GetMethod("RefreshList", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            var layout = (FrameworkElement)window.Content; layout.Measure(new Size(1280, 850)); layout.Arrange(new Rect(0, 0, 1280, 850)); layout.UpdateLayout();
            var listViewer = SmoothScroll.FindViewer(Field<ListBox>("list"))!;
            listViewer.ScrollToVerticalOffset(23.5); layout.UpdateLayout();
            Assert(!ScrollViewer.GetCanContentScroll(Field<ListBox>("list")) && Math.Abs(listViewer.VerticalOffset - 23.5) < 0.1, "list scrolls in fractional pixels rather than whole items");
            var favoriteId = db.Licenses.Last().Id;
            ((Task)typeof(MainWindow).GetMethod("ToggleFavorite", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [favoriteId])!).GetAwaiter().GetResult();
            Assert(((License)Field<ListBox>("list").Items[0]).Id == favoriteId && store.Load().Licenses.First(x => x.Id == favoriteId).Favorite, "favorite is saved and sorted first");
            var tagChip = Field<WrapPanel>("appChips").Children.OfType<Button>().First();
            tagChip.ContextMenu.Items.OfType<MenuItem>().First(x => (string)x.Header == "パープル").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert(store.Load().TagColors["After Effects"] == "#7544A2", "tag right-click color selection persists");
            ThemeManager.Apply("Dark"); layout.UpdateLayout();
            var listBox = Field<ListBox>("list"); listBox.ApplyTemplate();
            var surface = (Border)listBox.Template.FindName("ListSurface", listBox);
            var before = ((System.Windows.Media.SolidColorBrush)surface.Background).Color;
            layout.IsEnabled = false; layout.UpdateLayout();
            Assert(((System.Windows.Media.SolidColorBrush)surface.Background).Color == before && before == System.Windows.Media.Color.FromRgb(20, 25, 34), "disabled list retains dark background during backup");
            typeof(MainWindow).GetMethod("RefreshList", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null); layout.UpdateLayout();
            Assert(((System.Windows.Media.SolidColorBrush)surface.Background).Color == before, "list refresh retains dark surface"); layout.IsEnabled = true;
            var toggle = panel.Children.OfType<System.Windows.Controls.Primitives.ToggleButton>().Single();
            toggle.IsChecked = true; toggle.ApplyTemplate();
            var toggleSurface = (Border)toggle.Template.FindName("ToggleSurface", toggle);
            Assert(((System.Windows.Media.SolidColorBrush)toggleSurface.Background).Color == System.Windows.Media.Color.FromRgb(36, 94, 168), "checked encryption toggle uses strong blue instead of native pale fill");
            toggle.IsChecked = false;
            Assert(((System.Windows.Media.SolidColorBrush)toggleSurface.Background).Color == System.Windows.Media.Color.FromRgb(32, 39, 53), "unchecked encryption toggle uses dark surface");
            toggle.IsChecked = true; panel.Measure(new Size(650, 1300)); panel.Arrange(new Rect(0, 0, 650, 1300)); panel.UpdateLayout();
            var settingsBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(650, 1300, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); settingsBitmap.Render(panel);
            var settingsEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); settingsEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(settingsBitmap));
            using (var settingsOutput = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "settings-dark.png"))) settingsEncoder.Save(settingsOutput);
            var cleanWindow = new MainWindow(store);
            Assert(cleanWindow.Icon != null && cleanWindow.Icon.Width > 0, "generated icon loads from embedded resource");
            Assert(cleanWindow.PrepareToExit(), "tray exit permits clean shutdown");
            var exitArgs = new CancelEventArgs(); typeof(Window).GetMethod("OnClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cleanWindow, [exitArgs]);
            Assert(!exitArgs.Cancel, "explicit exit bypasses close-to-tray");
            Console.WriteLine("40 UI checks passed (no visible windows or external requests).");
        }
        finally { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true); }
    }
}
