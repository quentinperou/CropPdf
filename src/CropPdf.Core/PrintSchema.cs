using System.Globalization;
using System.Xml.Linq;

namespace CropPdf.Core;

/// <summary>A paper (or label) size offered by a printer driver.</summary>
/// <param name="Id">
/// Option name with its namespace, e.g. "{http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords}ISOA4":
/// drivers declare their own options (labels) in their own namespace.
/// </param>
/// <param name="DisplayName">Name given by the driver, in the Windows display language, if any.</param>
public sealed record PaperSize(string Id, string? DisplayName, double WidthMm, double HeightMm);

/// <summary>
/// Reads and writes the Print Schema XML of printer drivers (PrintCapabilities, PrintTicket). WPF's object model
/// only knows the standard paper names; a label printer describes its labels as driver-specific options, which must
/// be handed back by name for the driver to pick the right label.
/// </summary>
public static class PrintSchema
{
    public static readonly XNamespace Framework = "http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework";
    public static readonly XNamespace Keywords = "http://schemas.microsoft.com/windows/2003/08/printing/printschemakeywords";

    private static readonly XName PageMediaSize = Keywords + "PageMediaSize";

    /// <summary>
    /// Paper sizes listed in a PrintCapabilities document, in the driver's order. Options without a fixed size
    /// (sizes typed in by the user) are left out.
    /// </summary>
    public static IReadOnlyList<PaperSize> ReadPaperSizes(Stream capabilities)
    {
        var sizes = new List<PaperSize>();
        if (FindFeature(XDocument.Load(capabilities).Root, PageMediaSize) is not { } feature)
            return sizes;

        foreach (var option in feature.Elements(Framework + "Option"))
        {
            if (ResolveName(option) is not { } id
                || ReadMicrons(option, "MediaSizeWidth") is not { } width || width <= 0
                || ReadMicrons(option, "MediaSizeHeight") is not { } height || height <= 0)
                continue;

            string? displayName = option.Elements(Framework + "Property")
                .FirstOrDefault(p => ResolveName(p) == Keywords + "DisplayName")
                ?.Element(Framework + "Value")?.Value.Trim();
            sizes.Add(new PaperSize(id.ToString(), string.IsNullOrEmpty(displayName) ? null : displayName,
                width / 1000.0, height / 1000.0));
        }
        return sizes;
    }

    /// <summary>The paper size selected in a PrintTicket document, as a <see cref="PaperSize.Id"/>.</summary>
    public static string? ReadPaperSizeId(Stream ticket) =>
        FindFeature(XDocument.Load(ticket).Root, PageMediaSize)?.Element(Framework + "Option") is { } option
            ? ResolveName(option)?.ToString()
            : null;

    /// <summary>
    /// A partial PrintTicket that selects portrait orientation and, if given, a paper size; it is merged into a full
    /// ticket by the driver. Portrait because the app turns the printed area itself (see <see cref="PrintLayout"/>).
    /// </summary>
    public static string PaperTicket(string? paperSizeId)
    {
        var ticket = new XElement(Framework + "PrintTicket",
            new XAttribute(XNamespace.Xmlns + "psf", Framework),
            new XAttribute(XNamespace.Xmlns + "psk", Keywords),
            new XAttribute("version", "1"));

        if (paperSizeId is not null)
        {
            var name = XName.Get(paperSizeId);
            string prefix = "psk";
            if (name.Namespace != Keywords)
            {
                prefix = "ns0000";
                ticket.Add(new XAttribute(XNamespace.Xmlns + prefix, name.Namespace));
            }
            ticket.Add(Feature("psk:PageMediaSize", $"{prefix}:{name.LocalName}"));
        }
        ticket.Add(Feature("psk:PageOrientation", "psk:Portrait"));
        return ticket.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Feature(string name, string option) =>
        new(Framework + "Feature", new XAttribute("name", name),
            new XElement(Framework + "Option", new XAttribute("name", option)));

    private static XElement? FindFeature(XElement? root, XName name) =>
        root?.Elements(Framework + "Feature").FirstOrDefault(f => ResolveName(f) == name);

    // Print Schema names are QNames ("psk:ISOA4") whose prefix is declared in the document, under any spelling.
    private static XName? ResolveName(XElement element)
    {
        if (element.Attribute("name")?.Value is not { Length: > 0 } qualifiedName)
            return null;
        int colon = qualifiedName.IndexOf(':');
        if (colon < 0)
            return XName.Get(qualifiedName);
        var ns = element.GetNamespaceOfPrefix(qualifiedName[..colon]);
        return ns is null ? null : ns + qualifiedName[(colon + 1)..];
    }

    private static int? ReadMicrons(XElement option, string property)
    {
        string? value = option.Elements(Framework + "ScoredProperty")
            .FirstOrDefault(p => ResolveName(p) == Keywords + property)
            ?.Element(Framework + "Value")?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int microns) ? microns : null;
    }
}
