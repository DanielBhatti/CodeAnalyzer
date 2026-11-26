using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Globalization;
using System.Numerics;
using Vector = Avalonia.Vector;

namespace CodeAnalyzer.UI;

public sealed class Graph3DView : Control
{
    private IReadOnlyList<GraphNode> nodes = [];
    private IReadOnlyList<Dependency> edges = [];
    private string? selected;
    private string? originalSelection;
    private double yaw = 0.4;
    private double pitch = -0.25;
    private double zoom = 1;
    private double extent = 140;
    private Vector pan;
    private Point lastPointer;
    private bool rotating;
    private bool panning;

    public event Action<string>? NodeSelected;
    public event Action<string>? NodeOpened;

    public bool ShowMetrics
    {
        get;
        set { field = value; InvalidateVisual(); }
    } = true;

    internal IReadOnlyDictionary<string, Vector3> Positions { get; private set; } = new Dictionary<string, Vector3>();

    public Graph3DView()
    {
        ClipToBounds = true;
        Focusable = true;
        PointerPressed += Pressed;
        PointerMoved += Moved;
        PointerReleased += (_, e) => { rotating = panning = false; e.Pointer.Capture(null); };
        PointerCaptureLost += (_, _) => rotating = panning = false;
        PointerWheelChanged += (_, e) =>
        {
            zoom = Math.Clamp(zoom * Math.Pow(1.15, e.Delta.Y), 0.15, 8);
            InvalidateVisual();
            e.Handled = true;
        };
        SizeChanged += (_, _) => InvalidateVisual();
    }

    public void SetGraph(IReadOnlyList<GraphNode> newNodes, IReadOnlyList<Dependency> newEdges, string? selectedId = null)
    {
        nodes = newNodes;
        edges = newEdges;
        Positions = SpatialGraphLayout.Calculate(nodes, edges, selectedId);
        extent = Math.Max(140, Positions.Values.Select(p => (double)p.Length()).DefaultIfEmpty(140).Max());
        originalSelection = selectedId;
        ResetCamera();
    }

    public void Reset()
    {
        ResetCamera();
        if(selected is not null) NodeSelected?.Invoke(selected);
    }

    private void ResetCamera()
    {
        selected = originalSelection;
        yaw = 0.4;
        pitch = -0.25;
        zoom = 1;
        pan = default;
        rotating = panning = false;
        ToolTip.SetTip(this, null);
        InvalidateVisual();
    }

    internal (Point Point, double Depth, double Radius) Project(string id)
    {
        var rotation = Matrix4x4.CreateRotationY((float)yaw) * Matrix4x4.CreateRotationX((float)pitch);
        var rotated = Vector3.Transform(Positions[id], rotation);
        var perspective = 3 * extent / (3 * extent + rotated.Z);
        var scale = Math.Max(1, Math.Min(Bounds.Width, Bounds.Height) - 100) / (2 * extent) * zoom;
        return (new Point(Bounds.Width / 2 + pan.X + rotated.X * scale * perspective,
            Bounds.Height / 2 + pan.Y + rotated.Y * scale * perspective), rotated.Z,
            Math.Clamp(10 * perspective * Math.Sqrt(zoom), 5, 28));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.Parse("#111925")), new Rect(Bounds.Size));
        if(nodes.Count == 0)
        {
            DrawText(context, "Select a class to explore its relationships in 3D.", new Point(24, 24), Brushes.LightSlateGray, 14);
            return;
        }
        var projected = nodes.ToDictionary(n => n.Id, n => Project(n.Id), StringComparer.Ordinal);
        foreach(var edge in edges)
        {
            if(!projected.TryGetValue(edge.SourceId, out var a) || !projected.TryGetValue(edge.TargetId, out var b)) continue;
            var connected = edge.SourceId == selected || edge.TargetId == selected;
            var brush = connected ? Brushes.Gold : edge.Kind == "inherits / implements" ? Brushes.MediumPurple : Brushes.SlateGray;
            var pen = new Pen(brush, connected ? 2 : 1);
            if(edge.SourceId == edge.TargetId)
            {
                var loop = a.Point + new Vector(a.Radius, -a.Radius);
                context.DrawEllipse(null, pen, loop, a.Radius, a.Radius);
                context.DrawLine(pen, a.Point + new Vector(2 * a.Radius, -a.Radius),
                    a.Point + new Vector(2 * a.Radius - 5, -a.Radius - 7));
                continue;
            }
            var delta = new Vector(b.Point.X - a.Point.X, b.Point.Y - a.Point.Y);
            if(delta.Length <= a.Radius + b.Radius) continue;
            var unit = delta / delta.Length;
            var start = a.Point + unit * a.Radius;
            var end = b.Point - unit * b.Radius;
            var normal = new Vector(-unit.Y, unit.X);
            context.DrawLine(pen, start, end);
            context.DrawLine(pen, end, end - unit * 9 + normal * 4);
            context.DrawLine(pen, end, end - unit * 9 - normal * 4);
        }
        foreach(var node in nodes.OrderByDescending(n => projected[n.Id].Depth))
        {
            var p = projected[node.Id];
            var brush = node.Complexity > 20 ? Brushes.Salmon : node.Complexity > 10 ? Brushes.Gold : Brushes.MediumTurquoise;
            context.DrawEllipse(brush, new Pen(node.Id == selected ? Brushes.Gold : Brushes.White, node.Id == selected ? 3 : 1),
                p.Point, p.Radius, p.Radius);
            var label = p.Point + new Vector(p.Radius + 5, -8);
            DrawText(context, node.Label, label, Brushes.White, 12);
            if(ShowMetrics && node.Id == selected && node.Metrics.Length > 0)
                DrawText(context, node.Metrics, label + new Vector(0, 18), Brushes.LightGray, 11);
        }
    }

    private static void DrawText(DrawingContext context, string text, Point point, IBrush brush, double size) =>
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush), point);

    private GraphNode? HitTestNode(Point point) => nodes.OrderBy(n => Project(n.Id).Depth).FirstOrDefault(n =>
    {
        var p = Project(n.Id);
        return new Vector(point.X - p.Point.X, point.Y - p.Point.Y).Length <= p.Radius + 4;
    });

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if(!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed) return;
        _ = Focus();
        lastPointer = e.GetPosition(this);
        if(properties.IsLeftButtonPressed && HitTestNode(lastPointer) is { } node)
        {
            selected = node.Id;
            NodeSelected?.Invoke(node.Id);
            if(e.ClickCount == 2) NodeOpened?.Invoke(node.Id);
        }
        else
        {
            panning = properties.IsRightButtonPressed || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            rotating = !panning;
            e.Pointer.Capture(this);
        }
        InvalidateVisual();
        e.Handled = true;
    }

    private void Moved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(this);
        var delta = point - lastPointer;
        if(rotating)
        {
            yaw += delta.X * 0.01;
            pitch = Math.Clamp(pitch + delta.Y * 0.01, -Math.PI / 2, Math.PI / 2);
        }
        else if(panning) pan += delta;
        else
        {
            var node = HitTestNode(point);
            ToolTip.SetTip(this, node is null ? null : $"{node.Label}\n{node.Subtitle}\n{node.Metrics}");
        }
        lastPointer = point;
        InvalidateVisual();
    }
}