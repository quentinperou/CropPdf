using System.Diagnostics.CodeAnalysis;

namespace CropPdf.Core;

/// <summary>
/// Version number as used by the release tags and <c>&lt;Version&gt;</c>: "1.2.0", "1.3.0-beta.2" (SemVer 2.0).
/// A leading "v" and a "+build" suffix are ignored. A pre-release sorts before its final version:
/// 1.3.0-beta.2 &lt; 1.3.0-beta.10 &lt; 1.3.0-rc.1 &lt; 1.3.0.
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private readonly string[] _preRelease;

    private SemanticVersion(int major, int minor, int patch, string[] preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _preRelease = preRelease;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    /// <summary>"beta.2" for 1.3.0-beta.2, empty for a final version.</summary>
    public string PreRelease => string.Join('.', _preRelease);

    public bool IsPreRelease => _preRelease.Length > 0;

    public static SemanticVersion Parse(string text) =>
        TryParse(text, out var version) ? version : throw new FormatException($"\"{text}\" is not a version number.");

    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
            s = s[1..];
        int plus = s.IndexOf('+');
        if (plus >= 0)
            s = s[..plus];

        int dash = s.IndexOf('-');
        string[] numbers = (dash >= 0 ? s[..dash] : s).Split('.');
        string[] preRelease = dash >= 0 ? s[(dash + 1)..].Split('.') : [];

        if (numbers.Length != 3
            || !TryParseNumber(numbers[0], out int major)
            || !TryParseNumber(numbers[1], out int minor)
            || !TryParseNumber(numbers[2], out int patch)
            || !preRelease.All(IsValidIdentifier))
            return false;

        version = new SemanticVersion(major, minor, patch, preRelease);
        return true;
    }

    private static bool TryParseNumber(string text, out int value)
    {
        value = 0;
        return text.Length > 0 && text.All(char.IsAsciiDigit) && int.TryParse(text, out value);
    }

    private static bool IsValidIdentifier(string identifier) =>
        identifier.Length > 0 && identifier.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
            return 1;
        int result = Major.CompareTo(other.Major);
        if (result == 0)
            result = Minor.CompareTo(other.Minor);
        if (result == 0)
            result = Patch.CompareTo(other.Patch);
        return result != 0 ? result : ComparePreRelease(_preRelease, other._preRelease);
    }

    // SemVer precedence: no pre-release > any pre-release; identifiers compared one by one, numbers
    // numerically and below words; with equal identifiers, the longer list wins (beta < beta.1).
    private static int ComparePreRelease(string[] a, string[] b)
    {
        if (a.Length == 0 || b.Length == 0)
            return b.Length.CompareTo(a.Length);

        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            bool aNumber = IsNumber(a[i], out long x);
            bool bNumber = IsNumber(b[i], out long y);
            int result = (aNumber, bNumber) switch
            {
                (true, true) => x.CompareTo(y),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(a[i], b[i]),
            };
            if (result != 0)
                return Math.Sign(result);
        }
        return a.Length.CompareTo(b.Length);
    }

    private static bool IsNumber(string identifier, out long value)
    {
        value = 0;
        return identifier.All(char.IsAsciiDigit) && long.TryParse(identifier, out value);
    }

    public bool Equals(SemanticVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public override string ToString() => IsPreRelease ? $"{Major}.{Minor}.{Patch}-{PreRelease}" : $"{Major}.{Minor}.{Patch}";

    public static bool operator ==(SemanticVersion? a, SemanticVersion? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(SemanticVersion? a, SemanticVersion? b) => !(a == b);
    public static bool operator <(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) < 0;
    public static bool operator >(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) > 0;
    public static bool operator <=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) <= 0;
    public static bool operator >=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) >= 0;
}
