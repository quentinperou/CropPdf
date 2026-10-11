using System.IO;
using Microsoft.Win32;

namespace CropPdf;

/// <summary>
/// Whether "Crop PDF" shows in the Windows 11 context menu, for the current user. Kept in the registry rather than in
/// settings.json: the shell extension (src/CropPdf.Shell/dllmain.cpp, same key and value) reads it each time the
/// menu opens and hides the entry when it is 0, in the new menu and in "Show more options" alike.
/// </summary>
/// <remarks>
/// On Windows 11 the installer writes no classic entry, because "Show more options" already lists the entry of the
/// package: it would show twice. So, while the user hides the package's entry, this class adds a classic entry for
/// that user (HKCU), which keeps "Crop PDF" under "Show more options".
/// </remarks>
public static class ContextMenuSetting
{
    private const string KeyPath = @"Software\CropPdf";
    private const string ValueName = "ShowInContextMenu";

    // Same key as installer/CropPdf.iss (ClassicVerbKey), there under HKLM for Windows 10.
    private const string ClassicVerbKey = @"Software\Classes\SystemFileAssociations\.pdf\shell\CropPdf";

    /// <summary>The new context menu only exists on Windows 11.</summary>
    public static bool IsAvailable => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    public static bool Show
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is not int show || show != 0;
        }
        set
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                key.SetValue(ValueName, value ? 1 : 0, RegistryValueKind.DWord);
            SetClassicVerb(!value);
        }
    }

    /// <summary>
    /// Puts the user's classic entry in line with the setting, at startup: it was hidden with 1.2.0, which did not add
    /// it, and the install folder may have changed since it was written.
    /// </summary>
    public static void Sync()
    {
        if (!IsAvailable)
            return;
        try
        {
            SetClassicVerb(!Show);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // The new menu still works; only "Show more options" may miss the entry while it is hidden.
        }
    }

    private static void SetClassicVerb(bool present)
    {
        if (!IsAvailable)
            return;
        if (!present)
        {
            Registry.CurrentUser.DeleteSubKeyTree(ClassicVerbKey, throwOnMissingSubKey: false);
            return;
        }

        // Same values as the installer's classic entry: the label comes from the DLL's string table (Windows
        // display language), the default value is only a fallback.
        string exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CropPdf.exe");
        string dll = Path.Combine(Path.GetDirectoryName(exe)!, "CropPdfMenu.dll");
        using var key = Registry.CurrentUser.CreateSubKey(ClassicVerbKey);
        key.SetValue("", Strings.Get("ContextMenuLabel"));
        key.SetValue("MUIVerb", $"@{dll},-101");
        key.SetValue("Icon", $"\"{exe}\",0");
        using var command = key.CreateSubKey("command");
        command.SetValue("", $"\"{exe}\" \"%1\"");
    }
}
