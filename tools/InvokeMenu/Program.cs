using System.Runtime.InteropServices;

// Calls the installed "Crop PDF" context menu command (CropPdfMenu.dll, through its COM class) exactly like
// File Explorer does: IExplorerCommand GetTitle / GetIcon / GetState, then Invoke on the given file.
// Useful to test the DLL without clicking in File Explorer. Crop PDF must be installed.
//
//   dotnet run --project tools\InvokeMenu -- C:\temp\sample.pdf
//
// Then check the started CropPdf.exe: its parent must be explorer.exe and it must have no package identity.

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: InvokeMenu <file.pdf>");
    return 1;
}

return Program.Run(Path.GetFullPath(args[0]));

static partial class Program
{
    // Same CLSID as src/CropPdf.Shell/dllmain.cpp and package/AppxManifest.xml.
    private static readonly Guid CommandClsid = new("5C9B0CAD-9A30-49DA-827A-1672AC5D7FB8");

    public static int Run(string file)
    {
        var type = Type.GetTypeFromCLSID(CommandClsid)!;
        var command = (IExplorerCommand)Activator.CreateInstance(type)!;

        Guid shellItemId = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        Guid itemArrayId = new("b63ea76d-1f85-456f-a19c-48159efa858b");
        Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(file, IntPtr.Zero, ref shellItemId, out var item));
        Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(item, ref itemArrayId, out var items));

        command.GetTitle(items, out var title);
        command.GetIcon(items, out var icon);
        command.GetState(items, false, out var state);
        Console.WriteLine($"Title: {title}");
        Console.WriteLine($"Icon:  {icon}");
        Console.WriteLine($"State: {state} (0 = enabled)");

        int hr = command.Invoke(items, IntPtr.Zero);
        Console.WriteLine($"Invoke: 0x{hr:X8}");
        return hr == 0 ? 0 : 1;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IntPtr item);

    [DllImport("shell32.dll")]
    private static extern int SHCreateShellItemArrayFromShellItem(IntPtr item, ref Guid riid, out IntPtr array);
}

[ComImport, Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e0b9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IExplorerCommand
{
    [PreserveSig] int GetTitle(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] out string title);
    [PreserveSig] int GetIcon(IntPtr items, [MarshalAs(UnmanagedType.LPWStr)] out string icon);
    [PreserveSig] int GetToolTip(IntPtr items, out IntPtr tip);
    [PreserveSig] int GetCanonicalName(out Guid guid);
    [PreserveSig] int GetState(IntPtr items, [MarshalAs(UnmanagedType.Bool)] bool okToBeSlow, out uint state);
    [PreserveSig] int Invoke(IntPtr items, IntPtr bindContext);
    [PreserveSig] int GetFlags(out uint flags);
    [PreserveSig] int EnumSubCommands(out IntPtr commands);
}