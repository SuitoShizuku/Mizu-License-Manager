using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MizuLicenseManager;
public sealed class SmoothScroll : Animatable
{
    private ScrollViewer? viewer;
    private double target;
    private bool animating;
    private static readonly DependencyProperty OffsetProperty = DependencyProperty.Register("Offset", typeof(double), typeof(SmoothScroll), new PropertyMetadata(0d, (d, e) => ((SmoothScroll)d).viewer?.ScrollToVerticalOffset((double)e.NewValue)));
    protected override Freezable CreateInstanceCore() => new SmoothScroll();
    public static void Attach(ListBox list)
    {
        // Physical scrolling keeps the thumb and content in pixel coordinates, including variable-height cards.
        ScrollViewer.SetCanContentScroll(list, false);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        var motion = new SmoothScroll();
        list.PreviewMouseWheel += (_, e) =>
        {
            motion.viewer ??= FindViewer(list);
            if (motion.viewer == null || SystemParameters.WheelScrollLines == 0) return;
            var viewer = motion.viewer;
            var distance = SystemParameters.WheelScrollLines < 0 ? viewer.ViewportHeight : SystemParameters.WheelScrollLines * 16d;
            var destination = Math.Clamp((motion.animating ? motion.target : viewer.VerticalOffset) - e.Delta / 120d * distance, 0, viewer.ScrollableHeight);
            var start = viewer.VerticalOffset;
            motion.BeginAnimation(OffsetProperty, null); motion.SetValue(OffsetProperty, start);
            motion.target = destination;
            if (!SystemParameters.ClientAreaAnimation) { viewer.ScrollToVerticalOffset(destination); motion.animating = false; }
            else
            {
                motion.animating = true;
                var animation = new DoubleAnimation(start, destination, TimeSpan.FromMilliseconds(140)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                animation.Completed += (_, _) => motion.animating = false;
                motion.BeginAnimation(OffsetProperty, animation);
            }
            e.Handled = true;
        };
        void Stop()
        {
            if (motion.viewer == null) return;
            var current = motion.viewer.VerticalOffset;
            motion.BeginAnimation(OffsetProperty, null); motion.SetValue(OffsetProperty, current); motion.animating = false;
        }
        list.PreviewMouseDown += (_, _) => Stop();
        list.PreviewKeyDown += (_, _) => Stop();
        list.Unloaded += (_, _) => { Stop(); motion.viewer = null; };
    }
    public static ScrollViewer? FindViewer(DependencyObject node)
    {
        if (node is ScrollViewer viewer) return viewer;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        { var found = FindViewer(VisualTreeHelper.GetChild(node, i)); if (found != null) return found; }
        return null;
    }
}
