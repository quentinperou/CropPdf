using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace CropPdf;

/// <summary>
/// UI strings from Properties/Strings.resx (English, the default) and Strings.fr.resx (French).
/// The language follows the Windows display language (CultureInfo.CurrentUICulture).
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("CropPdf.Properties.Strings", typeof(Strings).Assembly);

    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}

/// <summary>In XAML, <c>Text="{local:Tr Open}"</c> shows the "Open" string in the UI language.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}