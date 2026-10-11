using Microsoft.Win32;

namespace CropPdf;

/// <summary>
/// Whether "Crop PDF" shows in the Windows 11 context menu, for the current user. Kept in the registry rather than in
/// settings.json: the shell extension (src/CropPdf.Shell/dllmain.cpp, same key and value) reads it each time the
/// menu opens and hides the entry when it is 0. The classic menu ("Show more options") always keeps the entry.
/// </summary>
public static class ContextMenuSetting
{
    private const string KeyPath = @"Software\CropPdf";
    private const string ValueName = "ShowInContextMenu";

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
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key.SetValue(ValueName, value ? 1 : 0, RegistryValueKind.DWord);
        }
    }
}
