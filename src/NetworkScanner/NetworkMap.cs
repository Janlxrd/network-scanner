using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NetworkScanner.Core;

namespace NetworkScanner;

public sealed class NetworkMap : FrameworkElement
{
    const double RingSpacing = 130;
    const double ChainSpacing = 70;
    const double NodeRadius = 18;
    const double MinScale = 0.3;
    const double MaxScale = 3;
    const int LabelLength = 14;

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(NetworkMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(DeviceItem), typeof(NetworkMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty UpstreamRoutersProperty = DependencyProperty.Register(
        nameof(UpstreamRouters), typeof(IReadOnlyList<string>), typeof(NetworkMap),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    static readonly Typeface TextFace = new("Segoe UI");
    static readonly Typeface IconFace = new(DeviceIcons.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    readonly List<(DeviceItem Item, Point World)> hitNodes = new();
    readonly ToolTip tip = new() { Placement = PlacementMode.Mouse };
    Brush text = Brushes.Black, secondary = Brushes.Gray, accent = Brushes.Blue, online = Brushes.Green, panel = Brushes.White;
    Pen nodePen = new(), linePen = new(), dashedPen = new(), selectedPen = new();
    double scale = 1;
    Vector pan;
    bool userMoved;
    Point? dragFrom;
    DeviceItem? hovered;

    public NetworkMap()
    {
        ClipToBounds = true;
        ToolTip = tip;
        ToolTipService.SetIsEnabled(this, false);
        Loaded += (_, _) => ThemeSwitcher.Changed += InvalidateVisual;
        Unloaded += (_, _) => ThemeSwitcher.Changed -= InvalidateVisual;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DeviceItem? SelectedItem
    {
        get => (DeviceItem?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public IReadOnlyList<string>? UpstreamRouters
    {
        get => (IReadOnlyList<string>?)GetValue(UpstreamRoutersProperty);
        set => SetValue(UpstreamRoutersProperty, value);
    }

    static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var map = (NetworkMap)d;
        if (e.OldValue is INotifyCollectionChanged oldList) oldList.CollectionChanged -= map.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newList) newList.CollectionChanged += map.OnCollectionChanged;
    }

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    void LoadBrushes()
    {
        text = Res("TextBrush");
        secondary = Res("SecondaryTextBrush");
        accent = Res("AccentBrush");
        online = Res("OnlineBrush");
        panel = Res("PanelBrush");
        var line = Res("MapLineBrush");
        nodePen = new Pen(secondary, 1.2);
        linePen = new Pen(line, 1.2);
        dashedPen = new Pen(line, 1.2) { DashStyle = new DashStyle(new double[] { 4, 4 }, 0) };
        selectedPen = new Pen(accent, 2);
    }

    protected override void OnRender(DrawingContext dc)
    {
        LoadBrushes();
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        hitNodes.Clear();

        var items = ItemsSource?.OfType<DeviceItem>().ToList() ?? new List<DeviceItem>();
        var router = items.FirstOrDefault(i => i.Kind == DeviceKind.Router);
        var devices = items.Where(i => i != router).OrderBy(i => i.IpSort).ToList();
        var places = MapLayout.Place(devices.Count, RingSpacing);
        var outer = devices.Count == 0 ? 0 : RingSpacing * ((devices.Count - 1) / MapLayout.RingCapacity + 1);
        var upstream = UpstreamRouters ?? Array.Empty<string>();
        var chain = upstream.Select((ip, i) => (Label: ip, Y: -(outer + ChainSpacing * (i + 1)))).ToList();
        var internetY = -(outer + ChainSpacing * (upstream.Count + 1));

        var radius = outer + NodeRadius + 40;
        var top = internetY - NodeRadius - 10;
        if (!userMoved) FitView(new Rect(-radius, top, 2 * radius, radius - top));

        Point S(double x, double y) => new(RenderSize.Width / 2 + pan.X + x * scale, RenderSize.Height / 2 + pan.Y + y * scale);
        var origin = S(0, 0);

        dc.DrawLine(linePen, origin, S(0, internetY));
        for (var i = 0; i < devices.Count; i++)
            dc.DrawLine(devices[i].Online ? linePen : dashedPen, origin, S(places[i].X, places[i].Y));

        var internet = S(0, internetY);
        dc.DrawEllipse(panel, nodePen, internet, NodeRadius * scale, NodeRadius * scale);
        Glyph(dc, DeviceIcons.Internet, internet, text, 16);
        Label(dc, "Internet", internet, secondary);
        foreach (var hop in chain)
        {
            var p = S(0, hop.Y);
            Box(dc, p, 0.8, panel, nodePen);
            Glyph(dc, DeviceIcons.Glyph(DeviceKind.Router), p, secondary, 13);
            Label(dc, hop.Label, p, secondary);
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var p = S(places[i].X, places[i].Y);
            DrawDevice(dc, devices[i], p, 1);
            hitNodes.Add((devices[i], p));
        }

        if (router is not null)
        {
            DrawDevice(dc, router, origin, 1.2);
            hitNodes.Add((router, origin));
        }
        else
        {
            Box(dc, origin, 1.2, panel, nodePen);
            Glyph(dc, DeviceIcons.Glyph(DeviceKind.Router), origin, text, 18);
            Label(dc, "Router", origin, text);
        }
    }

    void DrawDevice(DrawingContext dc, DeviceItem d, Point p, double size)
    {
        Box(dc, p, size, panel, null);
        if (!d.Online) dc.PushOpacity(0.45);
        Box(dc, p, size, d.TileFill, d.Kind == DeviceKind.Router ? new Pen(accent, 1.5) : null);
        Glyph(dc, d.Glyph, p, d.TileFore, 16 * size);
        if (d.Online)
        {
            var half = NodeRadius * size * scale;
            dc.DrawEllipse(online, new Pen(panel, 1.5 * scale), new Point(p.X + half - 2 * scale, p.Y - half + 2 * scale), 4 * scale, 4 * scale);
        }
        var name = d.Name == "Unknown" ? "." + d.Ip.Split('.')[^1] : d.Name;
        Label(dc, name.Length > LabelLength ? name[..(LabelLength - 1)] + "…" : name, p, text, size);
        if (!d.Online) dc.Pop();
        if (d == SelectedItem)
        {
            var r = (NodeRadius * size + 5) * scale;
            dc.DrawRoundedRectangle(null, selectedPen, new Rect(p.X - r, p.Y - r, 2 * r, 2 * r), 9 * scale, 9 * scale);
        }
    }

    void Box(DrawingContext dc, Point p, double size, Brush fill, Pen? pen)
    {
        var half = NodeRadius * size * scale;
        dc.DrawRoundedRectangle(fill, pen, new Rect(p.X - half, p.Y - half, 2 * half, 2 * half), 7 * scale, 7 * scale);
    }

    void Glyph(DrawingContext dc, string glyph, Point p, Brush brush, double size)
    {
        var ft = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, IconFace,
            Math.Max(6, size * scale), brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y - ft.Height / 2));
    }

    void Label(DrawingContext dc, string value, Point p, Brush brush, double size = 1)
    {
        var ft = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            TextFace, Math.Max(9, 11 * scale), brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y + (NodeRadius * size + 5) * scale));
    }

    void FitView(Rect content)
    {
        if (RenderSize.Width <= 0 || RenderSize.Height <= 0) return;
        var fit = Math.Min((RenderSize.Width - 20) / content.Width, (RenderSize.Height - 20) / content.Height);
        scale = Math.Clamp(Math.Min(1, fit), MinScale, MaxScale);
        pan = new Vector(-(content.X + content.Width / 2) * scale, -(content.Y + content.Height / 2) * scale);
    }

    DeviceItem? HitTest(Point p)
    {
        var r = NodeRadius * 1.2 * scale;
        return hitNodes.LastOrDefault(n => Math.Abs(n.World.X - p.X) <= r && Math.Abs(n.World.Y - p.Y) <= r).Item;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var s = e.GetPosition(this);
        var next = Math.Clamp(scale * (e.Delta > 0 ? 1.1 : 1 / 1.1), MinScale, MaxScale);
        var center = new Vector(RenderSize.Width / 2, RenderSize.Height / 2);
        var world = ((Vector)s - center - pan) / scale;
        pan = (Vector)s - center - world * next;
        scale = next;
        userMoved = true;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (HitTest(p) is { } node)
        {
            SelectedItem = node;
        }
        else if (e.ClickCount == 2)
        {
            userMoved = false;
            InvalidateVisual();
        }
        else
        {
            dragFrom = p;
            CaptureMouse();
        }
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        dragFrom = null;
        ReleaseMouseCapture();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        if (dragFrom is { } from)
        {
            pan += p - from;
            dragFrom = p;
            userMoved = true;
            InvalidateVisual();
            return;
        }

        var node = HitTest(p);
        Cursor = node is null ? Cursors.Arrow : Cursors.Hand;
        if (node == hovered) return;
        hovered = node;
        tip.IsOpen = false;
        if (node is null) return;
        tip.Content = $"{node.Name}  {node.Ip}";
        tip.IsOpen = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        hovered = null;
        tip.IsOpen = false;
    }

    static Brush Res(string key) => (Brush)Application.Current.FindResource(key);
}
