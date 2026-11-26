using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Globalization;

namespace CodeAnalyzer.UI;

public sealed class GraphView : Control
{
    private readonly IGraphLayout layout;
    private IReadOnlyList<GraphNode> nodes = [];
    private IReadOnlyList<Dependency> edges = [];
    private readonly Dictionary<string, Point> positions = [];
    private readonly Dictionary<string, Point> originalPositions = [];
    private string? originalSelection;
    private double scale = 1;
    private double minimumScale = 0.08;
    private Vector offset = new(30, 30);
    private Point lastPointer;
    private string? dragging;
    private bool panning;
    private bool fitWhenVisible;
    private string? selected;
    private double nodeWidth = 220;
    private double nodeHeight = 66;

    public event Action<string>? NodeSelected;
    public event Action<string>? NodeOpened;

    public bool ShowMetrics
    {
        get;
        set
        {
            if(field == value) return;
            field = value;
            SetGraph(nodes, edges, originalSelection);
        }
    } = true;

    public GraphView() : this(new LayeredGraphLayout()) { }

    public GraphView(IGraphLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        this.layout = layout;
        ClipToBounds = true;
        Focusable = true;
        PointerPressed += Pressed;
        PointerMoved += Moved;
        PointerReleased += Released;
        PointerCaptureLost += (_, _) => { dragging = null; panning = false; };
        PointerWheelChanged += Wheel;
        SizeChanged += (_, _) =>
        {
            if(fitWhenVisible) Fit();
            else InvalidateVisual();
        };
    }

    public void SetGraph(IReadOnlyList<GraphNode> newNodes, IReadOnlyList<Dependency> newEdges, string? selectedId = null)
    {
        var hasMetrics = ShowMetrics && newNodes.Any(n => n.Metrics.Length > 0);
        nodeWidth = hasMetrics ? 300 : 220;
        nodeHeight = hasMetrics ? 72 + newNodes.Max(n => n.Metrics.Split('\n').Length) * 18 : 66;
        var newPositions = layout.Calculate(newNodes, newEdges, new Size(nodeWidth, nodeHeight));
        nodes = newNodes;
        edges = newEdges;
        selected = selectedId;
        originalSelection = selectedId;
        originalPositions.Clear();
        positions.Clear();
        foreach(var node in nodes)
        {
            positions.Add(node.Id, newPositions[node.Id]);
            originalPositions.Add(node.Id, newPositions[node.Id]);
        }
        dragging = null;
        panning = false;
        Fit();
    }

    public void Reset()
    {
        dragging = null;
        panning = false;
        positions.Clear();
        foreach(var (id, point) in originalPositions) positions.Add(id, point);
        selected = originalSelection;
        ToolTip.SetTip(this, null);
        Fit();
        if(originalSelection is not null) NodeSelected?.Invoke(originalSelection);
    }

    public void Fit()
    {
        if(positions.Count == 0) { fitWhenVisible = false; InvalidateVisual(); return; }
        if(Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            fitWhenVisible = true;
            InvalidateVisual();
            return;
        }
        fitWhenVisible = false;
        var minX = positions.Values.Min(p => p.X);
        var minY = positions.Values.Min(p => p.Y);
        var width = positions.Values.Max(p => p.X) + nodeWidth - minX;
        var height = positions.Values.Max(p => p.Y) + nodeHeight - minY;
        scale = Math.Min(Math.Min(Math.Max(100, Bounds.Width - 60) / width,
            Math.Max(100, Bounds.Height - 60) / height), 1.3);
        minimumScale = Math.Min(0.08, scale);
        offset = new Vector(30 - minX * scale, 30 - minY * scale);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#111925")), Bounds.WithX(0).WithY(0));
        if(nodes.Count == 0)
        {
            DrawText(context, "Load a solution to explore dependencies.", new Point(30, 30), Brushes.LightSlateGray, 16);
            return;
        }
        using var transform = context.PushTransform(new Matrix(scale, 0, 0, scale, offset.X, offset.Y));
        foreach(var edge in edges)
        {
            if(!positions.TryGetValue(edge.SourceId, out var start) || !positions.TryGetValue(edge.TargetId, out var end)) continue;
            var a = start + new Vector(nodeWidth / 2, nodeHeight / 2);
            var b = end + new Vector(nodeWidth / 2, nodeHeight / 2);
            var delta = new Vector(b.X - a.X, b.Y - a.Y);
            if(delta.Length < 1) continue;
            var fraction = Math.Min(nodeWidth / 2 / Math.Max(0.001, Math.Abs(delta.X)),
                nodeHeight / 2 / Math.Max(0.001, Math.Abs(delta.Y)));
            b -= delta * Math.Min(0.49, fraction);
            var connected = edge.SourceId == selected || edge.TargetId == selected;
            var brush = connected ? Brushes.Gold : edge.Kind == "inherits / implements" ? Brushes.MediumPurple : Brushes.SlateGray;
            var pen = new Pen(brush, connected ? 2.5 : 1.3);
            context.DrawLine(pen, a, b);
            var unit = delta / delta.Length;
            var normal = new Vector(-unit.Y, unit.X);
            context.DrawLine(pen, b, b - unit * 12 + normal * 5);
            context.DrawLine(pen, b, b - unit * 12 - normal * 5);
        }
        foreach(var node in nodes)
        {
            var point = positions[node.Id];
            var rectangle = new Rect(point, new Size(nodeWidth, nodeHeight));
            var fill = new SolidColorBrush(Color.Parse(node.Id == selected ? "#264B66" : "#213044"));
            context.DrawRectangle(fill, new Pen(node.Id == selected ? Brushes.Gold : Brushes.SlateGray, 1), rectangle, 8, 8);
            var indicator = node.Complexity > 20 ? Brushes.Salmon : node.Complexity > 10 ? Brushes.Gold : Brushes.MediumTurquoise;
            context.FillRectangle(indicator, new Rect(point + new Vector(0, 12), new Size(4, 42)));
            using(context.PushClip(rectangle.Deflate(10)))
            {
                DrawText(context, node.Label, point + new Vector(12, 10), Brushes.White, 14);
                DrawText(context, node.Subtitle, point + new Vector(12, 36), Brushes.LightSlateGray, 11);
                if(ShowMetrics && node.Metrics.Length > 0)
                    DrawText(context, node.Metrics, point + new Vector(12, 58), Brushes.LightGray, 11);
            }
        }
    }

    private static void DrawText(DrawingContext context, string text, Point point, IBrush brush, double size) =>
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush), point);

    private Point ToWorld(Point p) => new((p.X - offset.X) / scale, (p.Y - offset.Y) / scale);

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _ = Focus();
        lastPointer = e.GetPosition(this);
        var world = ToWorld(lastPointer);
        dragging = nodes.LastOrDefault(n => new Rect(positions[n.Id], new Size(nodeWidth, nodeHeight)).Contains(world))?.Id;
        panning = dragging is null;
        e.Pointer.Capture(this);
        if(dragging is not null)
        {
            var id = dragging;
            selected = id;
            NodeSelected?.Invoke(id);
            if(e.ClickCount == 2) NodeOpened?.Invoke(id);
        }
        InvalidateVisual();
        e.Handled = true;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        if(dragging is not null && positions.ContainsKey(dragging)) positions[dragging] += (point - lastPointer) / scale;
        else if(panning) offset += point - lastPointer;
        else
        {
            var world = ToWorld(point);
            var node = nodes.LastOrDefault(n => new Rect(positions[n.Id], new Size(nodeWidth, nodeHeight)).Contains(world));
            ToolTip.SetTip(this, node is null ? null : $"{node.Label}\n{node.Subtitle}\n{node.Metrics}");
        }
        lastPointer = point;
        InvalidateVisual();
    }

    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        dragging = null;
        panning = false;
        e.Pointer.Capture(null);
    }

    private void Wheel(object? sender, PointerWheelEventArgs e)
    {
        var point = e.GetPosition(this);
        var world = ToWorld(point);
        scale = Math.Clamp(scale * Math.Pow(1.15, e.Delta.Y), minimumScale, 4);
        offset = new Vector(point.X - world.X * scale, point.Y - world.Y * scale);
        InvalidateVisual();
        e.Handled = true;
    }
}