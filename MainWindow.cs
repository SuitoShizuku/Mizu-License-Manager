using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace MizuLicenseManager;
public sealed class MainWindow : Window
{
    private readonly Storage storage;
    private readonly DiscordBackup discord = new();
    private Database db;
    private Guid? selected;
    private bool dirty, loading, busy;
    private bool allowExit;
    private readonly ListBox list = new() { BorderThickness = new Thickness(0) };
    private readonly TextBox search = new();
    private readonly ComboBox filter = new() { ItemsSource = new[] { "すべて", "現在有効", "現在無効", "一時的有効" }, SelectedIndex = 0 };
    private readonly StackPanel editor = new();
    private readonly TextBlock status = new() { Text = "ローカル保存の準備ができました", Foreground = Brushes.SlateGray, Margin = new Thickness(20, 10, 20, 14), TextWrapping = TextWrapping.Wrap };
    private readonly TextBox name = new(), keyVisible = new(), appPath = new() { IsReadOnly = true }, notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 100, VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, website = new();
    private readonly PasswordBox key = new();
    private readonly ComboBox email = new() { IsSynchronizedWithCurrentItem = false }, developer = new() { IsEditable = true, IsTextSearchEnabled = true }, deviceInput = new() { IsEditable = false, IsTextSearchEnabled = true, IsSynchronizedWithCurrentItem = false };
    private readonly RadioButton active = new() { Content = "現在有効", GroupName = "LicenseStatus", Margin = new Thickness(5, 12, 12, 12) };
    private readonly RadioButton inactive = new() { Content = "現在無効", GroupName = "LicenseStatus", Margin = new Thickness(5, 12, 12, 12) };
    private readonly RadioButton temporary = new() { Content = "一時的有効", GroupName = "LicenseStatus", Margin = new Thickness(5, 12, 12, 12) };
    private readonly StackPanel expirySection = new();
    private Button reveal = null!;
    private readonly DatePicker expires = new();
    private readonly WrapPanel deviceTags = new();
    private readonly List<string> chosenDevices = [];
    private readonly ComboBox appInput = new() { IsEditable = true, IsTextSearchEnabled = true };
    private readonly WrapPanel appChips = new();
    private readonly TabControl appFilters = new();
    private bool updatingTabs;
    private readonly List<string> chosenApps = [];
    private readonly HashSet<string> selectedApps = new(StringComparer.OrdinalIgnoreCase);
    private readonly StackPanel extraKeys = new();
    private readonly StackPanel extraUrls = new();
    private readonly TextBlock updated = new();
    private readonly DockPanel root = new();

    public MainWindow(Storage? storage = null)
    {
        this.storage = storage ?? new Storage();
        db = this.storage.Load(); Title = "Mizu License Manager"; Width = 1280; Height = 850; MinWidth = 1150; MinHeight = 650;
        ThemeManager.Apply(db.Theme); ThemeManager.Attach(this); FontFamily = new FontFamily("Yu Gothic UI"); FontSize = 14;
        WindowIcons.Attach(this);
        root.SetResourceReference(Panel.BackgroundProperty, "PageBrush");
        status.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        SmoothScroll.Attach(list);
        appFilters.SelectionChanged += (_, _) =>
        {
            if (updatingTabs) return;
            selectedApps.Clear();
            if (appFilters.SelectedItem is TabItem { Tag: string tag }) selectedApps.Add(tag);
            RefreshList();
        };
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = root;
        var header = new DockPanel { Margin = new Thickness(22, 16, 22, 12) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(Button("JSON書き出し", Export)); actions.Children.Add(Button("バックアップ読込", Import));
        actions.Children.Add(Button("バックアップ再送", async () => await Backup())); actions.Children.Add(Button("設定", Settings));
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        header.Children.Add(new TextBlock { Text = "MIZU  /  License Manager", FontSize = 23, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var grid = new Grid { Margin = new Thickness(20, 0, 20, 0) }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64, GridUnitType.Star) }); root.Children.Add(grid);
        var sidebar = new DockPanel { Margin = new Thickness(0, 0, 20, 0) }; grid.Children.Add(sidebar);
        var controls = new StackPanel(); controls.Children.Add(new TextBlock { Text = "ライセンス一覧", FontSize = 18, Margin = new Thickness(0, 0, 0, 12) });
        search.ToolTip = "名称・開発元・メール・デバイス・タグ・メモを検索"; controls.Children.Add(new TextBlock { Text = "検索" }); controls.Children.Add(search); controls.Children.Add(filter);
        controls.Children.Add(new TextBlock { Text = "タグで絞り込み", Margin = new Thickness(0, 12, 0, 4) });
        controls.Children.Add(new ScrollViewer { Content = appFilters, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        controls.Children.Add(Button("＋ ライセンスを追加", () => { if (ConfirmDiscard()) { loading = true; list.SelectedItem = null; loading = false; LoadEditor(null); } })); DockPanel.SetDock(controls, Dock.Top); sidebar.Children.Add(controls); sidebar.Children.Add(list);
        var template = new DataTemplate(typeof(License)); var stack = new FrameworkElementFactory(typeof(StackPanel)); stack.SetValue(StackPanel.MarginProperty, new Thickness(8));
        var heading = new FrameworkElementFactory(typeof(DockPanel));
        var star = new FrameworkElementFactory(typeof(Button)); star.SetBinding(System.Windows.Controls.Button.ContentProperty, new System.Windows.Data.Binding("Star")); star.SetValue(DockPanel.DockProperty, Dock.Right); star.SetValue(System.Windows.Controls.Button.PaddingProperty, new Thickness(6, 0, 6, 0)); star.SetValue(System.Windows.Controls.Button.FontSizeProperty, 22d); star.SetValue(System.Windows.Controls.Button.ToolTipProperty, "お気に入りを切り替え"); star.SetValue(System.Windows.Controls.Button.ForegroundProperty, Brushes.DarkGoldenrod);
        star.AddHandler(System.Windows.Controls.Button.ClickEvent, new RoutedEventHandler(async (sender, e) => { e.Handled = true; if (sender is Button { DataContext: License license }) await ToggleFavorite(license.Id); })); heading.AppendChild(star);
        var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name")); title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold); title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); heading.AppendChild(title); stack.AppendChild(heading);
        title.SetValue(TextBlock.FontSizeProperty, 18d);
        var sub = new FrameworkElementFactory(typeof(TextBlock)); sub.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Summary")); sub.SetValue(TextBlock.ForegroundProperty, Brushes.SlateGray); sub.SetValue(TextBlock.FontSizeProperty, 12.0); stack.AppendChild(sub); template.VisualTree = stack; list.ItemTemplate = template;
        sub.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        var badges = new FrameworkElementFactory(typeof(ItemsControl)); badges.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding("Apps"));
        badges.SetValue(ItemsControl.ItemsPanelProperty, new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel))));
        var badge = new FrameworkElementFactory(typeof(Border)); badge.SetValue(Border.CornerRadiusProperty, new CornerRadius(10)); badge.SetValue(Border.PaddingProperty, new Thickness(8, 3, 8, 3)); badge.SetValue(Border.MarginProperty, new Thickness(0, 6, 5, 0)); badge.SetResourceReference(Border.BackgroundProperty, "ChipBrush");
        var badgeText = new FrameworkElementFactory(typeof(TextBlock)); badgeText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding()); badgeText.SetValue(TextBlock.FontSizeProperty, 12.0); badge.AppendChild(badgeText);
        badge.AddHandler(FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) => { if (sender is Border { DataContext: string tag } border) StyleTag(border, tag); }));
        badges.SetValue(ItemsControl.ItemTemplateProperty, new DataTemplate { VisualTree = badge }); stack.AppendChild(badges);
        var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(24) }; Grid.SetColumn(card, 1); grid.Children.Add(card);
        card.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        var content = new DockPanel(); card.Child = content;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        footer.Children.Add(Button("削除", Delete)); var save = Button("変更を保存", async () => await SaveLicense()); save.Background = new SolidColorBrush(Color.FromRgb(35, 96, 190)); save.Foreground = Brushes.White; footer.Children.Add(save); DockPanel.SetDock(footer, Dock.Bottom); content.Children.Add(footer);
        content.Children.Add(new ScrollViewer { Content = editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        editor.Children.Add(new TextBlock { Text = "ライセンスの詳細", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        Field("正式名称 *", name, () => name.Text);
        var secret = new DockPanel(); reveal = Button("表示", () => { var show = keyVisible.Visibility != Visibility.Visible; keyVisible.Visibility = show ? Visibility.Visible : Visibility.Collapsed; key.Visibility = show ? Visibility.Collapsed : Visibility.Visible; reveal.Content = show ? "非表示" : "表示"; }); DockPanel.SetDock(reveal, Dock.Right); secret.Children.Add(reveal); var keys = new Grid(); keys.Children.Add(key); keys.Children.Add(keyVisible); secret.Children.Add(keys);
        var keyCaption = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 5) };
        keyCaption.Children.Add(new TextBlock { Text = "ライセンスキー · 表示ボタンで確認", VerticalAlignment = VerticalAlignment.Center });
        var plus = Button("＋", () => { AddExtraKey(new ExtraKey()); MarkDirty(); }); plus.Padding = new Thickness(6, 0, 6, 0); plus.ToolTip = "追加ライセンスキーを追加"; keyCaption.Children.Add(plus); editor.Children.Add(keyCaption);
        var primaryKey = new DockPanel(); var copyKey = Button("コピー", () => Copy(key.Password)); DockPanel.SetDock(copyKey, Dock.Right); primaryKey.Children.Add(copyKey); primaryKey.Children.Add(secret); editor.Children.Add(primaryKey); editor.Children.Add(extraKeys);
        Field("登録メールアドレス · 設定から候補を登録", email, () => email.SelectedItem as string ?? "");
        var pathRow = new DockPanel(); var clear = Button("クリア", () => appPath.Clear()); DockPanel.SetDock(clear, Dock.Right); pathRow.Children.Add(clear);
        var openPath = Button("開く", () => Open(() => OpenTarget.LocalPath(appPath.Text))); DockPanel.SetDock(openPath, Dock.Right); pathRow.Children.Add(openPath); pathRow.Children.Add(appPath);
        appPath.ToolTip = "クリックしてファイルまたはフォルダを選択";
        appPath.PreviewMouseLeftButtonDown += (_, e) => { var menu = new ContextMenu(); var f = new MenuItem { Header = "ファイルを選択…" }; f.Click += (_, _) => { var d = new OpenFileDialog(); if (d.ShowDialog(this) == true) appPath.Text = d.FileName; }; var d = new MenuItem { Header = "フォルダを選択…" }; d.Click += (_, _) => { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) appPath.Text = dialog.FolderName; }; menu.Items.Add(f); menu.Items.Add(d); menu.PlacementTarget = appPath; menu.IsOpen = true; e.Handled = true; };
        Field("アプリのファイル・フォルダパス", pathRow, () => appPath.Text);
        Field("開発元 · 入力または候補から選択", developer, () => developer.Text);
        var appsBox = new StackPanel(); var appsRow = new DockPanel(); var addApp = Button("追加", AddApp); DockPanel.SetDock(addApp, Dock.Right); appsRow.Children.Add(addApp); appsRow.Children.Add(appInput); appsBox.Children.Add(appsRow); appsBox.Children.Add(appChips);
        appInput.ToolTip = "例: After Effects / Photoshop / Blender";
        appInput.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { AddApp(); e.Handled = true; } };
        Field("タグ", appsBox, () => string.Join(Environment.NewLine, chosenApps));
        var deviceBox = new StackPanel(); var deviceRow = new DockPanel(); var add = Button("追加", AddDevice); DockPanel.SetDock(add, Dock.Right); deviceRow.Children.Add(add); deviceRow.Children.Add(deviceInput); deviceBox.Children.Add(deviceRow); deviceBox.Children.Add(deviceTags);
        EnhanceInput(appInput, addApp); EnhanceInput(deviceInput, add);
        foreach (var input in new[] { deviceInput, appInput, developer }) FocusDropdown.Attach(input);
        Field("対象デバイス · 複数追加できます", deviceBox, () => string.Join(Environment.NewLine, chosenDevices));
        var states = new StackPanel { Orientation = Orientation.Horizontal }; states.Children.Add(active); states.Children.Add(inactive); states.Children.Add(temporary);
        var stateRow = new Grid(); stateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); stateRow.ColumnDefinitions.Add(new ColumnDefinition());
        var stateSection = new StackPanel(); Field("状態", states, () => temporary.IsChecked == true ? "一時的有効" : active.IsChecked == true ? "現在有効" : "現在無効", stateSection); stateRow.Children.Add(stateSection);
        expirySection.Margin = new Thickness(14, 0, 0, 0); Grid.SetColumn(expirySection, 1); stateRow.Children.Add(expirySection);
        Field("有効期限（任意）", expires, () => expires.SelectedDate?.ToString("yyyy-MM-dd") ?? "", expirySection); editor.Children.Add(stateRow);
        var urlCaption = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 5) };
        urlCaption.Children.Add(new TextBlock { Text = "製品・管理ページURL（任意）", VerticalAlignment = VerticalAlignment.Center });
        var plusUrl = Button("＋", () => { AddExtraUrl(""); MarkDirty(); }); plusUrl.Padding = new Thickness(6, 0, 6, 0); plusUrl.ToolTip = "URLを追加"; urlCaption.Children.Add(plusUrl); editor.Children.Add(urlCaption);
        var urlRow = new DockPanel(); var copyUrl = Button("コピー", () => Copy(website.Text)); DockPanel.SetDock(copyUrl, Dock.Right); urlRow.Children.Add(copyUrl);
        var openUrl = Button("開く", () => OpenWebsite(website.Text)); DockPanel.SetDock(openUrl, Dock.Right); urlRow.Children.Add(openUrl); urlRow.Children.Add(website); editor.Children.Add(urlRow); editor.Children.Add(extraUrls);
        Field("メモ", notes, () => notes.Text); Field("最終更新", updated, () => updated.Text);
        foreach (var box in new[] { name, appPath, notes, website }) box.TextChanged += (_, _) => MarkDirty();
        key.PasswordChanged += (_, _) => { if (keyVisible.Text != key.Password) keyVisible.Text = key.Password; MarkDirty(); };
        keyVisible.TextChanged += (_, _) => { if (key.Password != keyVisible.Text) key.Password = keyVisible.Text; MarkDirty(); };
        developer.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => MarkDirty()));
        email.SelectionChanged += (_, _) => MarkDirty();
        foreach (var radio in new[] { active, inactive, temporary }) radio.Checked += (_, _) => { UpdateExpiry(); MarkDirty(); };
        expires.SelectedDateChanged += (_, _) => MarkDirty();
        search.TextChanged += (_, _) => RefreshList(); filter.SelectionChanged += (_, _) => RefreshList();
        list.SelectionChanged += (_, _) => { if (loading) return; var target = list.SelectedItem as License; if (target?.Id == selected) return; if (!ConfirmDiscard()) { loading = true; list.SelectedItem = db.Licenses.FirstOrDefault(x => x.Id == selected); loading = false; return; } LoadEditor(target); };
        Deactivated += (_, _) => HideKey();
        Closing += (_, e) => { if (!allowExit) { e.Cancel = true; HideKey(); Hide(); } };
        RefreshAppFilters(); RefreshList(); LoadEditor(null);
    }
    private static Button Button(string text, Action action) { var b = new Button { Content = text }; b.Click += (_, _) => action(); return b; }
    private void Field(string label, UIElement control, Func<string> value, StackPanel? parent = null)
    {
        parent ??= editor;
        var caption = new TextBlock { Text = label, Margin = new Thickness(0, 12, 0, 5) }; caption.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); parent.Children.Add(caption);
        var row = new DockPanel(); var copy = Button("コピー", () => Copy(value())); copy.ToolTip = label + "をコピー"; DockPanel.SetDock(copy, Dock.Right); row.Children.Add(copy); row.Children.Add(control); parent.Children.Add(row);
    }
    private void Copy(string text) { try { if (text.Length == 0) { status.Text = "コピーする値がありません"; return; } Clipboard.SetText(text); status.Text = "クリップボードにコピーしました"; } catch { status.Text = "クリップボードが使用中です。もう一度お試しください。"; } }
    private void MarkDirty() { if (!loading) { dirty = true; status.Text = "未保存の変更があります"; } }
    private bool ConfirmDiscard() => !dirty || MessageBox.Show(this, "未保存の変更を破棄しますか？", "変更の確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    public bool PrepareToExit()
    {
        if (busy) { Show(); Activate(); MessageBox.Show(this, "保存・送信処理が終わってから終了してください。"); return false; }
        if (dirty) { Show(); Activate(); if (!ConfirmDiscard()) return false; }
        allowExit = true; return true;
    }
    private void HideKey() { keyVisible.Visibility = Visibility.Collapsed; key.Visibility = Visibility.Visible; reveal.Content = "表示"; foreach (var row in extraKeys.Children.OfType<ExtraKeyRow>()) row.HideValue(); }
    private void UpdateExpiry() => expirySection.Visibility = temporary.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    private void RefreshList()
    {
        loading = true; var q = search.Text.Trim();
        list.ItemsSource = db.Licenses.Where(x => filter.SelectedIndex switch { 1 => x.Active && !x.Temporary, 2 => !x.Active && !x.Temporary, 3 => x.Temporary, _ => true })
            .Where(x => selectedApps.Count == 0 || x.Apps.Any(selectedApps.Contains))
            .Where(x => string.Join(" ", x.Name, x.Developer, x.Email, x.Notes, string.Join(" ", x.Devices), string.Join(" ", x.Apps)).Contains(q, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Favorite).ThenBy(x => x.Name).ToList();
        list.SelectedItem = db.Licenses.FirstOrDefault(x => x.Id == selected); loading = false;
    }
    private void LoadEditor(License? item)
    {
        loading = true; selected = item?.Id; var x = item ?? new License();
        name.Text = x.Name; key.Password = x.Key; HideKey(); email.SelectedIndex = -1; email.ItemsSource = db.Emails.Append(x.Email).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Order().ToList(); email.SelectedItem = string.IsNullOrEmpty(x.Email) ? null : x.Email; if (item == null) { email.SelectedIndex = -1; email.Text = ""; }
        developer.ItemsSource = db.Licenses.Select(l => l.Developer).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Order().ToList(); developer.Text = x.Developer;
        deviceInput.ItemsSource = db.Devices.ToList(); deviceInput.SelectedIndex = -1;
        appInput.ItemsSource = db.Licenses.SelectMany(l => l.Apps).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList(); appInput.Text = "";
        chosenApps.Clear(); chosenApps.AddRange(x.Apps); RenderApps();
        extraKeys.Children.Clear(); foreach (var entry in x.ExtraKeys) AddExtraKey(entry);
        extraUrls.Children.Clear(); foreach (var url in x.ExtraUrls) AddExtraUrl(url);
        appPath.Text = x.AppPath; notes.Text = x.Notes; website.Text = x.Website; active.IsChecked = x.Active && !x.Temporary; inactive.IsChecked = !x.Active && !x.Temporary; temporary.IsChecked = x.Temporary; expires.SelectedDate = x.Expires; UpdateExpiry();
        updated.Text = item == null ? "未保存" : x.Updated.ToString("yyyy/MM/dd HH:mm"); chosenDevices.Clear(); chosenDevices.AddRange(x.Devices); RenderDevices(); loading = false; dirty = false;
    }
    private void RenderDevices() { deviceTags.Children.Clear(); foreach (var device in chosenDevices.ToList()) deviceTags.Children.Add(Button(device + "  ×", () => { chosenDevices.Remove(device); RenderDevices(); MarkDirty(); })); }
    private static Button Chip(string text, Action action) { var chip = Button(text, action); chip.SetResourceReference(StyleProperty, "ChipStyle"); return chip; }
    private void AddApp()
    {
        var text = appInput.Text.Trim(); if (text.Length == 0) return;
        if (!chosenApps.Contains(text, StringComparer.OrdinalIgnoreCase)) { chosenApps.Add(text); RenderApps(); MarkDirty(); }
        appInput.Text = "";
    }
    private void RenderApps() { appChips.Children.Clear(); foreach (var app in chosenApps.ToList()) { var chip = Chip(app + "  ×", () => { chosenApps.Remove(app); RenderApps(); MarkDirty(); }); StyleTag(chip, app); appChips.Children.Add(chip); } }
    private void AddExtraKey(ExtraKey value)
    {
        ExtraKeyRow? row = null; row = new ExtraKeyRow(value, MarkDirty, () => { extraKeys.Children.Remove(row!); MarkDirty(); }, Copy); extraKeys.Children.Add(row);
    }
    private void AddExtraUrl(string value)
    {
        ExtraUrlRow? row = null; row = new ExtraUrlRow(value, MarkDirty, () => { extraUrls.Children.Remove(row!); MarkDirty(); }, OpenWebsite, Copy); extraUrls.Children.Add(row);
    }
    private void OpenWebsite(string url) => Open(() => OpenTarget.Website(url));
    private void Open(Action action)
    {
        try { action(); }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "開けませんでした"); }
        catch { MessageBox.Show(this, "開けませんでした。対象と既定のアプリ設定を確認してください。", "開く"); }
    }
    private void EnhanceInput(ComboBox input, Button add)
    {
        void Update()
        {
            var pending = !string.IsNullOrWhiteSpace(input.Text);
            add.SetResourceReference(BackgroundProperty, pending ? "AccentBrush" : "SurfaceBrush");
            if (pending) add.Foreground = Brushes.White; else add.SetResourceReference(ForegroundProperty, "TextBrush");
            add.FontWeight = pending ? FontWeights.Bold : FontWeights.Normal; add.Content = pending ? "＋ 追加" : "追加";
        }
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ComboBox.TextProperty, typeof(ComboBox)).AddValueChanged(input, (_, _) => { Update(); if (!loading) MarkDirty(); });
        input.SelectionChanged += (_, _) => Update();
        Update();
    }
    private void AddDevice()
    {
        if (deviceInput.SelectedItem is not string value || !db.Devices.Contains(value)) return;
        if (!chosenDevices.Contains(value, StringComparer.OrdinalIgnoreCase)) { chosenDevices.Add(value); RenderDevices(); MarkDirty(); }
        deviceInput.SelectedIndex = -1;
    }
    private async Task ToggleFavorite(Guid id)
    {
        if (busy) return;
        var next = DataJson.Clone(db); var item = next.Licenses.First(x => x.Id == id); item.Favorite = !item.Favorite;
        if (Persist(next)) { RefreshList(); await Backup(); }
    }
    private void StyleTag(FrameworkElement element, string tag)
    {
        var background = element is Border ? Border.BackgroundProperty : Control.BackgroundProperty;
        var foreground = element is Border ? TextBlock.ForegroundProperty : Control.ForegroundProperty;
        if (db.TagColors.TryGetValue(tag, out var color) && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
        { element.SetValue(background, new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))); element.SetValue(foreground, Brushes.White); }
        else { element.SetResourceReference(background, "ChipBrush"); element.SetResourceReference(foreground, "TextBrush"); }
        var menu = new ContextMenu();
        foreach (var (label, value) in new[] { ("標準に戻す", ""), ("ブルー", "#285DA8"), ("グリーン", "#226A50"), ("パープル", "#7544A2"), ("ピンク", "#A33570"), ("オレンジ", "#975018"), ("レッド", "#A8323A"), ("グレー", "#535D70") })
        {
            var choice = new MenuItem { Header = label }; if (value.Length > 0) choice.Icon = new Border { Width = 14, Height = 14, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)), CornerRadius = new CornerRadius(3) };
            choice.Click += async (_, _) => { if (busy) return; var next = DataJson.Clone(db); if (value.Length == 0) next.TagColors.Remove(tag); else next.TagColors[tag] = value; if (Persist(next)) { RenderApps(); RefreshList(); await Backup(); } }; menu.Items.Add(choice);
        }
        element.ContextMenu = menu;
    }
    private void RefreshAppFilters()
    {
        var apps = db.Licenses.SelectMany(x => x.Apps).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
        selectedApps.IntersectWith(apps); updatingTabs = true; appFilters.Items.Clear();
        var allTab = new TabItem { Header = "すべて" }; allTab.SetResourceReference(BackgroundProperty, "SurfaceBrush"); allTab.SetResourceReference(ForegroundProperty, "TextBrush"); appFilters.Items.Add(allTab);
        foreach (var app in apps)
        {
            var tab = new TabItem { Header = app, Tag = app }; StyleTag(tab, app); appFilters.Items.Add(tab);
        }
        appFilters.SelectedItem = appFilters.Items.OfType<TabItem>().FirstOrDefault(tab => tab.Tag is string tag && selectedApps.Contains(tag)) ?? appFilters.Items[0];
        updatingTabs = false;
    }
    private async Task SaveLicense()
    {
        if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show(this, "正式名称を入力してください。"); name.Focus(); return; }
        var next = DataJson.Clone(db); var item = new License { Id = selected ?? Guid.NewGuid(), Name = name.Text.Trim(), Key = key.Password, Email = email.SelectedItem as string ?? "", AppPath = appPath.Text, Developer = developer.Text.Trim(), Notes = notes.Text, Active = inactive.IsChecked != true, Temporary = temporary.IsChecked == true, Devices = [.. chosenDevices], Website = website.Text.Trim(), Expires = temporary.IsChecked == true ? expires.SelectedDate : null, Updated = DateTime.Now };
        item.Apps = [.. chosenApps]; item.ExtraKeys = extraKeys.Children.OfType<ExtraKeyRow>().Select(row => row.Value).ToList(); item.Favorite = db.Licenses.FirstOrDefault(x => x.Id == selected)?.Favorite ?? false;
        item.ExtraUrls = extraUrls.Children.OfType<ExtraUrlRow>().Select(row => row.Value).ToList();
        next.Licenses.RemoveAll(x => x.Id == item.Id); next.Licenses.Add(item); next.Devices = next.Devices.Concat(item.Devices).Distinct().ToList();
        if (!Persist(next)) return; selected = item.Id; RefreshList(); LoadEditor(item); await Backup();
    }
    private bool Persist(Database next) { try { storage.Save(next); db = next; RefreshAppFilters(); status.Text = "PCに保存しました"; return true; } catch { MessageBox.Show(this, "保存できませんでした。空き容量と保存先へのアクセスを確認してください。入力は保持されています。", "保存エラー"); return false; } }
    private async Task Backup()
    {
        if (busy) return;
        if (string.IsNullOrWhiteSpace(db.Webhook)) { status.Text = "PCに保存済み · Discord未設定（設定画面で登録できます）"; return; }
        busy = true; root.IsEnabled = false; status.Text = "PCに保存済み · Discordへ全件バックアップ中…";
        try { await discord.SendAsync(db); status.Text = $"PC保存・Discordバックアップ完了  {DateTime.Now:HH:mm:ss}  /  {db.Licenses.Count}件"; }
        catch (Exception ex) { status.Text = ex is InvalidOperationException ? "PCに保存済み · " + ex.Message : "PCに保存済み · Discord送信に失敗しました。ネットワークを確認して再送してください。"; }
        finally { busy = false; root.IsEnabled = true; }
    }
    private async void Delete()
    {
        if (selected == null) return;
        if (MessageBox.Show(this, "このライセンスを削除しますか？", "削除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var next = DataJson.Clone(db); next.Licenses.RemoveAll(x => x.Id == selected); if (!Persist(next)) return; LoadEditor(null); RefreshList(); await Backup();
    }
    private async void Settings()
    {
        var dialog = new SettingsWindow(db) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var next = DataJson.Clone(db); next.Emails = dialog.Emails; next.Devices = dialog.Devices; next.Webhook = dialog.Webhook; next.EncryptBackup = dialog.EncryptBackup; next.BackupPassphrase = dialog.BackupPassphrase;
        next.EmailEntries = dialog.EmailEntries; next.DeviceEntries = dialog.DeviceEntries;
        var oldEmail = email.SelectedItem as string;
        var editedEmailId = db.EmailEntries.FirstOrDefault(x => x.Name == oldEmail)?.Id;
        var renamedDevices = chosenDevices.Select(value => { var id = db.DeviceEntries.FirstOrDefault(x => x.Name == value)?.Id; return next.DeviceEntries.FirstOrDefault(x => x.Id == id)?.Name ?? value; }).ToList();
        next.Theme = dialog.Theme;
        if (!Persist(next)) { ThemeManager.Apply(db.Theme); return; } ThemeManager.Apply(db.Theme);
        oldEmail = db.EmailEntries.FirstOrDefault(x => x.Id == editedEmailId)?.Name ?? oldEmail;
        var wasDirty = dirty; loading = true; chosenDevices.Clear(); chosenDevices.AddRange(renamedDevices); RenderDevices(); RefreshList(); loading = true;
        email.ItemsSource = db.Emails.Append(oldEmail ?? "").Where(x => x.Length > 0).Distinct().ToList(); email.SelectedItem = oldEmail;
        deviceInput.ItemsSource = db.Devices.ToList(); deviceInput.SelectedIndex = -1; loading = false; dirty = wasDirty; await Backup();
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "JSONバックアップ|*.json", FileName = $"mizu-licenses-{DateTime.Now:yyyyMMdd}.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllBytes(dialog.FileName, DataJson.Backup(db)); status.Text = "保存済みデータをJSONに書き出しました（ライセンスキーを含みます）"; } catch { MessageBox.Show(this, "書き出しに失敗しました。"); }
    }
    private async void Import()
    {
        if (!ConfirmDiscard()) return;
        var dialog = new OpenFileDialog { Filter = "Mizuバックアップ|*.json;*.mlicense|暗号化バックアップ|*.mlicense|JSONバックアップ|*.json" }; if (dialog.ShowDialog(this) != true) return;
        Database next;
        try
        {
            var bytes = File.ReadAllBytes(dialog.FileName);
            if (Path.GetExtension(dialog.FileName).Equals(".mlicense", StringComparison.OrdinalIgnoreCase))
            {
                var prompt = new PassphraseWindow { Owner = this }; if (prompt.ShowDialog() != true) return;
                var passphrase = prompt.Passphrase;
                busy = true; root.IsEnabled = false;
                try { bytes = await Task.Run(() => BackupEncryption.Decrypt(bytes, passphrase)); }
                finally { busy = false; root.IsEnabled = true; }
            }
            next = DataJson.Parse(bytes);
        }
        catch (System.Security.Cryptography.CryptographicException) { MessageBox.Show(this, "パスフレーズが違うか、ファイルが破損・変更されています。保存データは変更していません。"); return; }
        catch { MessageBox.Show(this, "対応するバックアップを読み込めませんでした。保存データは変更していません。"); return; }
        if (MessageBox.Show(this, $"現在の全ライセンス・メール・デバイス候補を、バックアップ内の{next.Licenses.Count}件に置き換えますか？", "バックアップ復元", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        next.Webhook = db.Webhook; next.EncryptBackup = db.EncryptBackup; next.BackupPassphrase = db.BackupPassphrase;
        next.Theme = db.Theme;
        if (!Persist(next)) return; LoadEditor(null); RefreshList(); await Backup();
    }
}
