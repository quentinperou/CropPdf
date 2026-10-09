using System.Text;
using System.Xml.Linq;
using CropPdf.Core;

namespace CropPdf.Tests;

public class PrintSchemaTests
{
    private const string LabelNamespace = "http://schemas.example.com/labelprinter/printschemakeywords";
    private static readonly string A4 = "{" + PrintSchema.Keywords.NamespaceName + "}ISOA4";
    private static readonly string Label = "{" + LabelNamespace + "}Label62x100";

    // A label printer driver: one standard size, one label of its own, one size typed in by the user.
    private const string Capabilities = $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <psf:PrintCapabilities version="1"
            xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
            xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords"
            xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"
            xmlns:ns0000="{LabelNamespace}">
          <psf:Feature name="psk:PageOrientation">
            <psf:Option name="psk:Portrait" />
          </psf:Feature>
          <psf:Feature name="psk:PageMediaSize">
            <psf:Property name="psf:SelectionType"><psf:Value xsi:type="xsd:QName">psk:PickOne</psf:Value></psf:Property>
            <psf:Option name="psk:ISOA4">
              <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string">A4</psf:Value></psf:Property>
              <psf:ScoredProperty name="psk:MediaSizeWidth"><psf:Value xsi:type="xsd:integer">210000</psf:Value></psf:ScoredProperty>
              <psf:ScoredProperty name="psk:MediaSizeHeight"><psf:Value xsi:type="xsd:integer">297000</psf:Value></psf:ScoredProperty>
            </psf:Option>
            <psf:Option name="ns0000:Label62x100">
              <psf:Property name="psk:DisplayName"><psf:Value xsi:type="xsd:string"> 62mm x 100mm </psf:Value></psf:Property>
              <psf:ScoredProperty name="psk:MediaSizeWidth"><psf:Value xsi:type="xsd:integer">62000</psf:Value></psf:ScoredProperty>
              <psf:ScoredProperty name="psk:MediaSizeHeight"><psf:Value xsi:type="xsd:integer">100000</psf:Value></psf:ScoredProperty>
            </psf:Option>
            <psf:Option name="psk:CustomMediaSize">
              <psf:ScoredProperty name="psk:MediaSizeWidth"><psf:ParameterRef name="psk:PageMediaSizeMediaSizeWidth" /></psf:ScoredProperty>
              <psf:ScoredProperty name="psk:MediaSizeHeight"><psf:ParameterRef name="psk:PageMediaSizeMediaSizeHeight" /></psf:ScoredProperty>
            </psf:Option>
          </psf:Feature>
        </psf:PrintCapabilities>
        """;

    private static Stream Xml(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public void PaperSizes_IncludeDriverLabels_AndSkipTypedInSizes()
    {
        var sizes = PrintSchema.ReadPaperSizes(Xml(Capabilities));

        Assert.Equal(
            [new PaperSize(A4, "A4", 210, 297), new PaperSize(Label, "62mm x 100mm", 62, 100)],
            sizes);
    }

    [Fact]
    public void PaperSizes_AreFound_WhateverThePrefixes()
    {
        string renamed = Capabilities.Replace("psf:", "f:").Replace("xmlns:psf", "xmlns:f")
            .Replace("psk:", "k:").Replace("xmlns:psk", "xmlns:k");

        Assert.Equal(2, PrintSchema.ReadPaperSizes(Xml(renamed)).Count);
    }

    [Fact]
    public void PaperSizes_AreEmpty_WithoutThePageMediaSizeFeature()
    {
        const string none = """
            <psf:PrintCapabilities version="1" xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework" />
            """;
        Assert.Empty(PrintSchema.ReadPaperSizes(Xml(none)));
    }

    [Fact]
    public void Ticket_GivesTheSelectedPaperSize()
    {
        string ticket = $"""
            <psf:PrintTicket version="1"
                xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework"
                xmlns:psk="http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords"
                xmlns:ns0000="{LabelNamespace}">
              <psf:Feature name="psk:PageMediaSize"><psf:Option name="ns0000:Label62x100" /></psf:Feature>
            </psf:PrintTicket>
            """;
        Assert.Equal(Label, PrintSchema.ReadPaperSizeId(Xml(ticket)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PaperTicket_SelectsThePaperByName_InPortrait(bool driverLabel)
    {
        string id = driverLabel ? Label : A4;
        var ticket = XDocument.Parse(PrintSchema.PaperTicket(id));

        Assert.Equal(id, PrintSchema.ReadPaperSizeId(Xml(ticket.ToString())));
        var orientation = ticket.Root!.Elements(PrintSchema.Framework + "Feature")
            .Single(f => f.Attribute("name")!.Value == "psk:PageOrientation");
        Assert.Equal("psk:Portrait", orientation.Element(PrintSchema.Framework + "Option")!.Attribute("name")!.Value);
    }

    [Fact]
    public void PaperTicket_WithoutPaper_OnlySetsTheOrientation()
    {
        string ticket = PrintSchema.PaperTicket(null);
        Assert.Null(PrintSchema.ReadPaperSizeId(Xml(ticket)));
        Assert.Contains("psk:Portrait", ticket);
    }
}
