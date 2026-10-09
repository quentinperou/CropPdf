using CropPdf.Core;

namespace CropPdf.Tests;

public class CropGeometryTests
{
    // A portrait page, 600 x 800 pt, whose visible area does not start at the origin.
    private static readonly PdfBox Page = new(10, 20, 610, 820);

    // Top-left quarter of the page *as displayed*.
    private static readonly NormalizedRect TopLeftQuarter = new(0, 0, 0.5, 0.5);

    [Fact]
    public void FullSelection_GivesVisibleBox_ForEveryRotation()
    {
        foreach (int rotate in new[] { 0, 90, 180, 270 })
            Assert.Equal(Page, CropGeometry.ToPdfBox(new NormalizedRect(0, 0, 1, 1), Page, rotate));
    }

    [Fact]
    public void NoRotation_TopLeftOnScreen_IsTopLeftInPdf()
    {
        // Screen Y points down, PDF Y points up: the top half is the upper 400 pt.
        Assert.Equal(new PdfBox(10, 420, 310, 820), CropGeometry.ToPdfBox(TopLeftQuarter, Page, 0));
    }

    [Fact]
    public void Rotate90_TopLeftOnScreen_IsBottomLeftInPdf()
    {
        // Turned clockwise, the PDF bottom-left corner ends up at the top-left of the screen.
        // Displayed size is 800 x 600: half the screen width is half the PDF height.
        Assert.Equal(new PdfBox(10, 20, 310, 420), CropGeometry.ToPdfBox(TopLeftQuarter, Page, 90));
    }

    [Fact]
    public void Rotate180_TopLeftOnScreen_IsBottomRightInPdf()
    {
        Assert.Equal(new PdfBox(310, 20, 610, 420), CropGeometry.ToPdfBox(TopLeftQuarter, Page, 180));
    }

    [Fact]
    public void Rotate270_TopLeftOnScreen_IsTopRightInPdf()
    {
        Assert.Equal(new PdfBox(310, 420, 610, 820), CropGeometry.ToPdfBox(TopLeftQuarter, Page, 270));
    }

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(450, 90)]
    [InlineData(360, 0)]
    [InlineData(-180, 180)]
    public void NormalizeRotation_HandlesOutOfRangeValues(int rotate, int expected)
    {
        Assert.Equal(expected, CropGeometry.NormalizeRotation(rotate));
    }

    [Fact]
    public void DisplaySize_SwapsAxesWhenRotatedAQuarterTurn()
    {
        Assert.Equal((600d, 800d), CropGeometry.DisplaySize(Page, 0));
        Assert.Equal((800d, 600d), CropGeometry.DisplaySize(Page, 90));
        Assert.Equal((800d, 600d), CropGeometry.DisplaySize(Page, -90));
    }

    [Fact]
    public void Selection_OutsideThePage_IsClamped()
    {
        var box = CropGeometry.ToPdfBox(new NormalizedRect(-0.5, 0.5, 2, 2), Page, 0);
        Assert.Equal(new PdfBox(10, 20, 610, 420), box);
    }
}
