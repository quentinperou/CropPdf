using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using CropPdf.Core;

namespace CropPdf;

/// <summary>One page of the open document, as shown in the page list.</summary>
public sealed class PageItem : INotifyPropertyChanged
{
    private double _zoom = 1;
    private ImageSource? _image;
    private NormalizedRect? _selection;

    public PageItem(int index, int pageCount, double baseWidth, double baseHeight)
    {
        Index = index;
        Label = Strings.Format("PageLabel", index + 1, pageCount);
        BaseWidth = baseWidth;
        BaseHeight = baseHeight;
    }

    public int Index { get; }
    public string Label { get; }

    /// <summary>Displayed page size at 100 % zoom, in WPF units (1/96 inch), rotation applied.</summary>
    public double BaseWidth { get; }
    public double BaseHeight { get; }

    public double Zoom
    {
        get => _zoom;
        set
        {
            if (!Set(ref _zoom, value))
                return;
            OnPropertyChanged(nameof(DisplayWidth));
            OnPropertyChanged(nameof(DisplayHeight));
        }
    }

    public double DisplayWidth => Math.Round(BaseWidth * _zoom);
    public double DisplayHeight => Math.Round(BaseHeight * _zoom);

    /// <summary>Rendered bitmap, or null while not rendered (or evicted to save memory).</summary>
    public ImageSource? Image
    {
        get => _image;
        set => Set(ref _image, value);
    }

    /// <summary>Pixel width of <see cref="Image"/>; 0 when there is no image.</summary>
    public int RenderedWidth { get; set; }

    /// <summary>True while the page has a visual on screen (or close to it).</summary>
    public bool IsRealized { get; set; }

    /// <summary>Crop rectangle drawn on this page, if any. Only one page has a selection at a time.</summary>
    public NormalizedRect? Selection
    {
        get => _selection;
        set => Set(ref _selection, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
