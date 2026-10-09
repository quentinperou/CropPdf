using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CropPdf.Core;

namespace CropPdf;

/// <summary>
/// Transparent layer over a page: drag to draw the crop rectangle, drag inside it to move it,
/// drag its edges or corners to resize it. The rectangle is stored as fractions of the page size.
/// </summary>
public sealed class SelectionOverlay : FrameworkElement
{
    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection), typeof(NormalizedRect?), typeof(SelectionOverlay),
        new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private const double GripTolerance = 8; // pixels around an edge that grab it
    private const double GripRadius = 5;
    private const double MinDragDistance = 4; // a shorter drag is a click, not a new rectangle

    private static readonly Brush ShadeBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x88, 0, 0, 0)));
    private static readonly Pen HaloPen = Frozen(new Pen(Brushes.White, 3));

    [Flags]
    private enum Edges { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

    private enum DragMode { None, Create, Move, Resize }

    private DragMode _mode;
    private Edges _edges;
    private Point _start;
    private Rect _startRect;

    public SelectionOverlay()
    {
        Cursor = Cursors.Cross;
        Focusable = false;
    }

    public NormalizedRect? Selection
    {
        get => (NormalizedRect?)GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    private Rect? PixelRect => Selection is { } s
        ? new Rect(s.X * ActualWidth, s.Y * ActualHeight, s.Width * ActualWidth, s.Height * ActualHeight)
        : null;

    private Brush AccentBrush => TryFindResource("AccentFillColorDefaultBrush") as Brush ?? SystemColors.HighlightBrush;

    // The element has no background: accept the mouse everywhere on the page anyway.
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) =>
        new Rect(RenderSize).Contains(hitTestParameters.HitPoint)
            ? new PointHitTestResult(this, hitTestParameters.HitPoint)
            : null;

    protected override void OnRender(DrawingContext dc)
    {
        if (PixelRect is not { } r)
            return;

        var outside = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(RenderSize)), new RectangleGeometry(r));
        dc.DrawGeometry(ShadeBrush, null, outside);

        var accentPen = new Pen(AccentBrush, 1.5);
        dc.DrawRectangle(null, HaloPen, r);
        dc.DrawRectangle(null, accentPen, r);
        foreach (var grip in GripPoints(r))
            dc.DrawEllipse(Brushes.White, accentPen, grip, GripRadius, GripRadius);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var p = ClampToPage(e.GetPosition(this));
        var rect = PixelRect;

        _edges = rect is { } r ? HitEdges(r, p) : Edges.None;
        _mode = _edges != Edges.None ? DragMode.Resize
            : rect is { } inside && inside.Contains(p) ? DragMode.Move
            : DragMode.Create;
        _start = p;
        _startRect = rect ?? Rect.Empty;

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = ClampToPage(e.GetPosition(this));
        switch (_mode)
        {
            case DragMode.None:
                Cursor = CursorAt(p);
                break;

            case DragMode.Create:
                if (IsClick(p))
                    return;
                SetPixelRect(new Rect(_start, p));
                break;

            case DragMode.Move:
                var offset = p - _start;
                double x = Math.Clamp(_startRect.X + offset.X, 0, ActualWidth - _startRect.Width);
                double y = Math.Clamp(_startRect.Y + offset.Y, 0, ActualHeight - _startRect.Height);
                SetPixelRect(new Rect(x, y, _startRect.Width, _startRect.Height));
                break;

            case DragMode.Resize:
                SetPixelRect(Resize(_startRect, _edges, p));
                break;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        // A simple click outside the rectangle removes it.
        if (_mode == DragMode.Create && IsClick(ClampToPage(e.GetPosition(this))))
            Selection = null;

        _mode = DragMode.None;
        if (IsMouseCaptured)
            ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        _mode = DragMode.None;
        base.OnLostMouseCapture(e);
    }

    private bool IsClick(Point p) =>
        Math.Abs(p.X - _start.X) < MinDragDistance && Math.Abs(p.Y - _start.Y) < MinDragDistance;

    private Point ClampToPage(Point p) =>
        new(Math.Clamp(p.X, 0, ActualWidth), Math.Clamp(p.Y, 0, ActualHeight));

    private void SetPixelRect(Rect r)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;
        Selection = new NormalizedRect(r.X / ActualWidth, r.Y / ActualHeight, r.Width / ActualWidth, r.Height / ActualHeight)
            .ClampToPage();
    }

    private static Rect Resize(Rect r, Edges edges, Point p)
    {
        double left = edges.HasFlag(Edges.Left) ? p.X : r.Left;
        double right = edges.HasFlag(Edges.Right) ? p.X : r.Right;
        double top = edges.HasFlag(Edges.Top) ? p.Y : r.Top;
        double bottom = edges.HasFlag(Edges.Bottom) ? p.Y : r.Bottom;
        return new Rect(new Point(left, top), new Point(right, bottom));
    }

    private static Edges HitEdges(Rect r, Point p)
    {
        var grab = r;
        grab.Inflate(GripTolerance, GripTolerance);
        if (!grab.Contains(p))
            return Edges.None;

        var edges = Edges.None;
        if (Math.Abs(p.X - r.Left) <= GripTolerance) edges |= Edges.Left;
        else if (Math.Abs(p.X - r.Right) <= GripTolerance) edges |= Edges.Right;
        if (Math.Abs(p.Y - r.Top) <= GripTolerance) edges |= Edges.Top;
        else if (Math.Abs(p.Y - r.Bottom) <= GripTolerance) edges |= Edges.Bottom;
        return edges;
    }

    private Cursor CursorAt(Point p)
    {
        if (PixelRect is not { } r)
            return Cursors.Cross;

        var edges = HitEdges(r, p);
        bool horizontal = (edges & (Edges.Left | Edges.Right)) != 0;
        bool vertical = (edges & (Edges.Top | Edges.Bottom)) != 0;
        if (horizontal && vertical)
            return edges == (Edges.Left | Edges.Top) || edges == (Edges.Right | Edges.Bottom)
                ? Cursors.SizeNWSE
                : Cursors.SizeNESW;
        if (horizontal)
            return Cursors.SizeWE;
        if (vertical)
            return Cursors.SizeNS;
        return r.Contains(p) ? Cursors.SizeAll : Cursors.Cross;
    }

    private static IEnumerable<Point> GripPoints(Rect r)
    {
        double cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
        yield return r.TopLeft;
        yield return new Point(cx, r.Top);
        yield return r.TopRight;
        yield return new Point(r.Right, cy);
        yield return r.BottomRight;
        yield return new Point(cx, r.Bottom);
        yield return r.BottomLeft;
        yield return new Point(r.Left, cy);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
