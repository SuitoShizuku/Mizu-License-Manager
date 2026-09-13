using System.Windows;
using System.Windows.Controls;

namespace MizuLicenseManager;
public sealed class EntryEditor : StackPanel
{
    private readonly List<(Guid Id, TextBox Input)> rows = [];
    private readonly StackPanel inputs = new();
    public EntryEditor(string title, string buttonText, IEnumerable<NamedEntry> entries)
    {
        Children.Add(new TextBlock { Text = title, Margin = new Thickness(0, 20, 0, 8) });
        Children.Add(inputs);
        foreach (var entry in entries) Add(entry.Id, entry.Name);
        var add = new Button { Content = buttonText, HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += (_, _) => { Add(Guid.NewGuid(), ""); rows[^1].Input.Focus(); }; Children.Add(add);
    }
    private void Add(Guid id, string name)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var number = new TextBlock { Text = (rows.Count + 1).ToString(), Width = 28, VerticalAlignment = VerticalAlignment.Center }; DockPanel.SetDock(number, Dock.Left); row.Children.Add(number);
        var box = new TextBox { Text = name, MinWidth = 160 };
        var copy = new Button { Content = "コピー" }; copy.Click += (_, _) => { try { if (box.Text.Length > 0) Clipboard.SetText(box.Text); } catch { MessageBox.Show("コピーできませんでした。"); } }; DockPanel.SetDock(copy, Dock.Right); row.Children.Add(copy); row.Children.Add(box);
        rows.Add((id, box)); inputs.Children.Add(row);
    }
    public List<NamedEntry> Values() => rows.Where(x => !string.IsNullOrWhiteSpace(x.Input.Text)).Select(x => new NamedEntry { Id = x.Id, Name = x.Input.Text.Trim() }).ToList();
}
