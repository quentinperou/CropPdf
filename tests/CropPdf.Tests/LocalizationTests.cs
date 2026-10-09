using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CropPdf.Tests;

/// <summary>Keeps the English and French UI strings (src/CropPdf/Properties/*.resx) in step with each other and with the code.</summary>
public class LocalizationTests
{
    private static readonly string AppDir = Path.Combine(FindRepositoryRoot(), "src", "CropPdf");

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CropPdf.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (CropPdf.slnx) not found.");
    }

    private static Dictionary<string, string> ReadStrings(string fileName) =>
        XDocument.Load(Path.Combine(AppDir, "Properties", fileName)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    private static HashSet<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).ToHashSet();

    [Fact]
    public void FrenchTranslation_HasExactlyTheEnglishKeys()
    {
        Assert.Equal(
            ReadStrings("Strings.resx").Keys.Order(),
            ReadStrings("Strings.fr.resx").Keys.Order());
    }

    [Fact]
    public void Translations_UseTheSamePlaceholders()
    {
        var french = ReadStrings("Strings.fr.resx");
        foreach (var (key, english) in ReadStrings("Strings.resx"))
            Assert.True(Placeholders(english).SetEquals(Placeholders(french[key])), $"Placeholders differ for \"{key}\".");
    }

    [Fact]
    public void EveryKeyUsedByTheApp_Exists()
    {
        var keys = ReadStrings("Strings.resx").Keys.ToHashSet();
        var used = Directory.EnumerateFiles(AppDir)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".xaml"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{local:Tr (\w+)\}|Strings\.(?:Get|Format)\(""(\w+)""|""(Confirm\w+)"""))
            .Select(m => m.Groups.Values.Skip(1).First(g => g.Success).Value)
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, keys));
    }
}