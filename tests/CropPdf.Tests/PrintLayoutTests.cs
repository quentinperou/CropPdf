using CropPdf.Core;

namespace CropPdf.Tests;

public class PrintLayoutTests
{
    // A 4 x 6 inch label, in WPF units (1/96 inch).
    private const double LabelWidth = 384, LabelHeight = 576;

    [Fact]
    public void Fit_PortraitAreaOnPortraitLabel_FillsItWithoutTurning()
    {
        var layout = PrintLayout.Compute(100, 150, LabelWidth, LabelHeight, PrintScaling.FitToPaper);
        Assert.Equal(new PrintLayout(384, 576, Rotate: false), layout);
    }

    [Fact]
    public void Fit_LandscapeAreaOnPortraitLabel_IsTurned()
    {
        var layout = PrintLayout.Compute(150, 100, LabelWidth, LabelHeight, PrintScaling.FitToPaper);
        Assert.True(layout.Rotate);
        Assert.Equal(576, layout.Width, 6);
        Assert.Equal(384, layout.Height, 6);
    }

    [Fact]
    public void Fit_KeepsTheProportions()
    {
        var layout = PrintLayout.Compute(100, 300, LabelWidth, LabelHeight, PrintScaling.FitToPaper);
        Assert.False(layout.Rotate);
        Assert.Equal(192, layout.Width, 6);
        Assert.Equal(576, layout.Height, 6);
    }

    [Fact]
    public void Fit_SquareArea_IsNotTurned()
    {
        Assert.False(PrintLayout.Compute(100, 100, LabelWidth, LabelHeight, PrintScaling.FitToPaper).Rotate);
    }

    [Fact]
    public void ActualSize_SmallArea_KeepsItsSize()
    {
        Assert.Equal(new PrintLayout(150, 100, Rotate: false),
            PrintLayout.Compute(150, 100, LabelWidth, LabelHeight, PrintScaling.ActualSize));
    }

    [Fact]
    public void ActualSize_AreaLargerThanThePaper_IsReducedButNeverTurned()
    {
        var layout = PrintLayout.Compute(1000, 500, LabelWidth, LabelHeight, PrintScaling.ActualSize);
        Assert.False(layout.Rotate);
        Assert.Equal(384, layout.Width, 6);
        Assert.Equal(192, layout.Height, 6);
    }

    [Fact]
    public void EmptyArea_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintLayout.Compute(0, 100, LabelWidth, LabelHeight, PrintScaling.FitToPaper));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintLayout.Compute(100, 100, 0, LabelHeight, PrintScaling.FitToPaper));
    }
}