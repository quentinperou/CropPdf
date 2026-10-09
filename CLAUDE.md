# CLAUDE.md

Guidance for Claude Code and contributors working on this repository. `README.md` (English) and
`README.fr.md` (French) describe the project for users.

## The project

**Crop PDF**: a small open-source (MIT) Windows 11 app to crop one PDF page from the File Explorer context menu.
The user draws a rectangle on a page; the saved file contains **only that page**, cropped, saved as a
`name_crop.pdf` copy or in place of the original. The UI is in English and French. Author:
[QuentinPerou](https://github.com/quentinperou). Repository: https://github.com/quentinperou/CropPdf (public). Credits wording chosen by the author (bottom of the READMEs): "Developed by QuentinPerou, with Claude Code (Anthropic)"; the About card shows "Developed by QuentinPerou".

## Project status (October 9, 2026)

**Version 1.0.0 works end to end**, developed and tested on Windows 11 (build 26200): full build, install,
context-menu package registration (`Status : Ok`), menu command invoked the way File Explorer does (the app opens
on the PDF, started by `explorer.exe`, without package identity, not minimized), drawing and saving a copy,
light and dark themes, English/French UI with live switching from Settings. 30 unit tests pass.
Printing of the selected area (printer button / `Ctrl+P`) is implemented, installed and validated by the author
with Microsoft Print to PDF on A4: *fit to paper* turned a landscape area a quarter and filled the page height,
centered; *actual size* printed it at 100 %, centered. Layout unit-tested; area rendering checked on a rotated page.
Checked on screen by the author: the “Recadrer le PDF” entry with its icon in the new Windows 11 context menu,
and the app icon in the taskbar when started from it.

Not verified yet:
- printing on a physical printer, especially shipping labels on a label printer (the main use case);
- an English Windows: the “Crop PDF” menu label was only checked with `LoadString` under a forced thread UI
  language, and the installer language auto-selection was only seen in French;
- Windows 10 (classic menu only);
- UAC answered with a *different* administrator account: the package is then registered for that account only;
- the GitHub Actions workflow (never run).

Next steps:
1. Check that CI passes on the first push, then release `v1.0.0` (see “Releasing a version” in the README).
2. Add the update check (see “To do”) against the repository above; the About card and the installer's
   `AppPublisherURL` (both pointing at the GitHub profile today) could then link to the repository too.

## Commands

```powershell
.\scripts\build.ps1                       # everything: tests, app, DLL, MSIX package, installer -> dist\
.\scripts\build.ps1 -SkipTests -NoInstaller
dotnet test tests\CropPdf.Tests           # unit tests (fast, no UI)
dotnet build src\CropPdf                  # build the app only
dotnet run --project src\CropPdf -- C:\path\to\file.pdf
```

- `CROPPDF_THEME=Light|Dark` and `CROPPDF_LANG=en|fr` force the theme / UI language (by default the app
  follows Windows and the Settings): handy to check both.
- User settings live in `%LOCALAPPDATA%\CropPdf\settings.json`.
- The context menu can only be tested installed (`dist\CropPdf-Setup-<version>.exe`, UAC prompt).
- `build.ps1` enters the Visual Studio environment itself (MSVC, CMake, Ninja, makeappx) through vswhere.
- Dev tools (not part of the app, not in the solution):
  - `dotnet run --project tools\SamplePdf -- C:\temp\sample.pdf` writes a 3-page test PDF (plain page,
    `/Rotate 90` page, page with an existing CropBox);
  - `dotnet run --project tools\InvokeMenu -- C:\temp\sample.pdf` calls the **installed** context menu command
    like File Explorer does (prints title/icon/state, then starts the app on the file).

## Working on the project

- The app may be in use on screen while work is in progress: drive it through UI Automation or posted window
  messages rather than sending input to the screen.
- Docs (both READMEs, this file) are expected to be kept up to date at the end of each work session, including
  the “Project status” section above.
- Personal or machine-specific notes go in `CLAUDE.local.md` (git-ignored), not in this file.

## Architecture

| Folder | Role |
|--------|------|
| `src/CropPdf.Core` | Pure, tested logic: `CropGeometry` (screen → PDF coordinates), `PdfCropper` (writing with PDFsharp), `OutputPath` (`_crop` name), `PrintLayout` (size/rotation on the sheet), `CropAreaTooSmallException`. No UI text. |
| `src/CropPdf` | WPF .NET 10. `MainWindow` (open, zoom, render, save), `SelectionOverlay` (draw/move/resize), `PdfPageRenderer` (rendering through `Windows.Data.Pdf`), `PageItem` (page model), `SettingsWindow` + `AppSettings` (language setting), `Localization.cs` (`Strings`, `{local:Tr}`), `Properties/Strings*.resx`. |
| `src/CropPdf.Shell` | C++ DLL (WRL) implementing `IExplorerCommand`: has File Explorer start `CropPdf.exe "<file>"`, located next to it. Localized label in `CropPdfMenu.rc`. |
| `package/` | `AppxManifest.xml` of the “sparse” package + logos. Packed by `makeappx` in `build.ps1`. |
| `installer/CropPdf.iss` | Inno Setup installer (English/French): copies to Program Files, registers the package, adds the classic verb. |
| `scripts/` | `build.ps1`, `make-icons.ps1` (regenerates the `.ico` and logos; the generated files are committed). |
| `.github/workflows/build.yml` | CI: build + tests on every push; on a `v*` tag, publishes the installer in a Release. |
| `docs/` | Icon used by the README. |
| `tools/` | Dev tools: `SamplePdf` (test PDF), `InvokeMenu` (runs the installed menu command). Not in the solution. |

### Technical choices (and why)

- **New Windows 11 menu** = package identity required. We use an **unsigned** *sparse package* (package with
  external location): `Publisher` contains the OID `2.25.311729368913984317654407730594956997722=1` and the
  installer runs `Add-AppxPackage -ExternalLocation <folder> -AllowUnsigned` as administrator. No certificate
  to buy or trust. Future option: free signing through SignPath Foundation (open source projects).
- **Classic entry as well** (HKLM `SystemFileAssociations\.pdf\shell\CropPdf`): Windows 10 and “Show more options”.
- **WPF + built-in Fluent theme** (`Application.ThemeMode="System"`) rather than WinUI 3: Windows 11 look
  (Mica, light/dark, accent) without the complexity of the Windows App SDK. `ThemeMode` is flagged as
  experimental (warning WPF0001 silenced in the csproj).
- **Rendering**: `Windows.Data.Pdf` (built into Windows, nothing to ship). **Writing**: PDFsharp (MIT).
- **Cropping** = `MediaBox` and `CropBox` set to the area; `TrimBox`/`BleedBox`/`ArtBox` removed. Content
  outside the area stays in the file (hidden), as in Acrobat.
- **A single page in the output**: `PdfCropper` imports the page into a new document (the rest of the file,
  bookmarks and forms included, is not copied). Title/author are kept.
- **Safe writing**: temporary file in the same folder, then `File.Move(overwrite)`; the source is read into
  memory first, so overwriting the original works.
- **Framework-dependent app** (small installer): the installer offers to install the .NET 10 Desktop Runtime.
- DLL built with the **static CRT** (`/MT`): no dependency on the Visual C++ redistributable.
- **Printing** (`MainWindow.PrintAsync`): nothing is saved. WPF `PrintDialog` + `PrintVisual`; the selected area is
  rendered by `Windows.Data.Pdf` with `SourceRect` (displayed coordinates, rotation included — verified on a
  `/Rotate 90` page) at the printer resolution (`PrintTicket.PageResolution`, 300 dpi by default), then laid out
  in the printable area (`PrintCapabilities.PageImageableArea`, the visual is arranged at its origin) by
  `PrintLayout`: *fit to paper* (turned a quarter when that gives a bigger print) or *actual size* (reduced only
  when too large, never turned). The mode used last is saved (`AppSettings.PrintScaling`) and reused by `Ctrl+P`.
  The print button is deliberately discreet (icon only, like Settings), as asked by the author.

### Localization

- App texts: `Properties/Strings.resx` (English, neutral/default) and `Strings.fr.resx`. XAML uses
  `{local:Tr Key}`, code uses `Strings.Get("Key")` / `Strings.Format("Key", ...)`. **Never hard-code a UI text.**
- Texts are read when a window is built, so changing the language rebuilds the main window
  (`App.ReloadMainWindow`: same place, same document) and reopens Settings.
- `AppSettings.Language` is `"system"`, `"en"` or `"fr"`. `"system"` = the Windows display language captured at
  startup; any language without a translation falls back to English.
- `LocalizationTests` checks that both files have the same keys and placeholders and that every key used in
  `src/CropPdf/*.cs|*.xaml` exists (patterns: `{local:Tr X}`, `Strings.Get("X")`, `Strings.Format("X"`, `"ConfirmX"`).
- Context menu label: string tables in `CropPdfMenu.rc` (English neutral, en-US, French neutral). **Never
  `LANG_NEUTRAL`**: the resource loader would pick it first whatever the user's language. The label follows the
  Windows display language, not the app setting. The classic verb uses `MUIVerb = @<app>\CropPdfMenu.dll,-101`.
- Installer: `[Languages]` english/french + `[CustomMessages]`, `ShowLanguageDialog=auto`.
- Adding a language: resx, `.rc` table, `.iss`, `AppSettings.SupportedLanguages`, and a `README.<lang>.md`.

### Coordinates (sensitive)

- The selection is stored as a `NormalizedRect`: fractions (0..1) of the page **as displayed** (rotation
  applied), origin top-left. Independent of the zoom.
- `CropGeometry.ToPdfPoint` converts to PDF space (points, origin bottom-left) by undoing `/Rotate`
  (clockwise). The 4 rotations are covered by tests: any change must keep them green.
- The reference visible area is `CropBox ∩ MediaBox` (`PdfCropper.GetVisibleBox`).
- `Windows.Data.Pdf`: `PdfPage.Size` is in DIPs (1/96 inch), rotation and CropBox already applied.

## Conventions

- **Code, comments, docs, scripts and commit messages in English.** User-facing texts go through the resources
  (English + French). The README exists in English and French: keep both in sync.
- Comments explain *why*, not *what*.
- `.ps1` and `.iss` files are **UTF-8 with BOM** and CRLF (Windows PowerShell 5.1 and Inno Setup misread
  BOM-less UTF-8), see `.editorconfig`. Scripts must stay compatible with Windows PowerShell 5.1.
- UI colors: always Fluent theme resources through `DynamicResource` (`TextFillColorSecondaryBrush`,
  `AccentFillColorDefaultBrush`…), never hard-coded colors, so that light/dark is followed. Exception: the
  white page background.
- Icon-only buttons get an `AutomationProperties.Name` (screen readers, UI automation).
- A single version in `Directory.Build.props` (app + installer). The **package** version (`AppxManifest.xml`)
  only changes when the manifest changes.

## Known pitfalls

- **CLSID `5C9B0CAD-9A30-49DA-827A-1672AC5D7FB8`**: in `dllmain.cpp` *and* `AppxManifest.xml`. The package name
  `CropPdf.ContextMenu`: in `AppxManifest.xml` *and* `CropPdf.iss`. Keep them in sync.
- Inno Setup: a line starting with `[` is read as a section header, even indented inside `[Code]`.
- **Starting the app from the DLL**: a direct `CreateProcess` from the `dllhost` surrogate (which has the package
  identity) gives `CropPdf.exe` the package identity too, because it sits in the external location, whatever the
  `PROC_THREAD_ATTRIBUTE_DESKTOP_APP_POLICY`. The taskbar then shows a blank tile. So the DLL asks File Explorer
  to start the app (`IShellWindows` → `IShellDispatch2::ShellExecute`): parent `explorer.exe`, no identity, can
  take the foreground. The direct `CreateProcess` is only a fallback (with `SW_SHOWNORMAL` forced, otherwise the
  window inherits the hidden state of `dllhost` and opens minimized).
- `exdisp.h` does not compile with `WIN32_LEAN_AND_MEAN` (do not put it back in the CMakeLists).
- `rc.exe` requires a trailing newline at the end of `resource.h`.
- File Explorer may keep the DLL loaded during an update: the installer then replaces it at the next restart
  (`restartreplace`).
- PDFsharp: the `PdfRectangle(double, double, double, double)` constructor is internal; use two `XPoint`s.
- Fluent-based styles have a disabled state with a solid background: subtle buttons are hidden rather than disabled.
- The first launch after an install is slower (cold .NET start): wait a few seconds before checking the window.
- On Windows 11, WPF's `PrintDialog` appears as the **modern print dialog**, hosted by `explorer.exe` (window
  “Crop PDF – Imprimer”, class `ApplicationFrameWindow`), and shows “This app doesn't support print preview”
  (classic printing API, see “To do”).
- **Never automate the print dialog on a machine with real printers.** UI Automation reported “Microsoft Print to
  PDF” as selected while the list still showed the default printer (a label printer): a click on Print could have
  wasted labels. Test printing by hand, or check the layout through `PrintLayout` tests.

## Debugging the context menu

- Is the package registered? `Get-AppxPackage CropPdf.ContextMenu` must show `Status : Ok` and an
  `InstallLocation` under `WindowsApps`.
- The DLL is only updated by reinstalling (`dist\CropPdf-Setup-<version>.exe`). If File Explorer keeps it
  loaded, restart File Explorer (or the session).
- Testing without File Explorer: `tools\InvokeMenu` creates the COM object from its CLSID and calls
  `IExplorerCommand::Invoke` with an `IShellItemArray`, the same path as File Explorer.
- Identity of the started app: `GetPackageFullName` on the `CropPdf.exe` process must fail (error 15700,
  “no package”) and its parent must be `explorer.exe`.
- Menu label per language: load `CropPdfMenu.dll` as a data file and call `LoadString(101)` after
  `SetThreadPreferredUILanguages` (careful: on a French Windows, languages without a table fall back to French).
- Running a program *inside* the package context:
  `Invoke-CommandInDesktopPackage -PackageFamilyName (Get-AppxPackage CropPdf.ContextMenu).PackageFamilyName -AppId CropPdf -Command <exe>`.

## Tests

- `tests/CropPdf.Tests` (xUnit) covers `CropPdf.Core`: geometry (rotations, offset CropBox, bounds) and produced
  files (single page, boxes, rotation kept, overwrite, `_crop` name), plus `LocalizationTests`.
- The UI is checked by hand: open the PDF from `tools\SamplePdf` (rotated page, page with an existing CropBox),
  draw, save, reopen the result.
- The UI can also be driven with UI Automation (`System.Windows.Automation`) without moving the mouse: icon
  buttons have accessible names, and the Settings window is a child of the main window in the UIA tree.

## To do (see also the README)

- **Updates through GitHub Releases** once the repository is public: query
  `https://api.github.com/repos/<owner>/<repo>/releases/latest` at most once a day, compare with the assembly
  version, show an InfoBar “Version x available” with a button that downloads the `CropPdf-Setup-*.exe` asset
  and runs it (`/SILENT`). **Offer** the update rather than forcing it: installing needs UAC anyway.
- **Print preview** in the Windows 11 print dialog (idea kept by the author for later): requires the modern print
  API instead of WPF's `PrintDialog`: `PrintManager` (through `IPrintManagerInterop` for a window handle) with an
  `IPrintDocumentSource` that provides preview pages (`IPrintPreviewPageCollection`) and the final document,
  drawn with Direct2D (`ID2D1PrintControl`). Several hundred lines, most likely native code (could live next to
  `CropPdf.Shell`). Worth it only if the preview is missed in daily use.
- Signing (findings from October 2026, to re-check before acting):
  - **SignPath Foundation**: free for open source. Requires an OSI license with no proprietary parts, an existing
    release and active maintenance, MFA for the whole team, builds in CI with a manual approval of each release,
    and a “Code signing policy” section on the project page. The manifest `Publisher` must then become
    `CN=SignPath Foundation, O=SignPath Foundation, L=Lewes, S=Delaware, C=US`. Once signed, the package can be
    registered per user without admin rights (no more `-AllowUnsigned`).
  - Reference project for that setup: [dnGrep](https://github.com/dnGrep/dnGrep) (WPF + C++ `IExplorerCommand`
    DLL + sparse package, signed through SignPath, registered from the app).
  - **Microsoft Store**: free registration for individuals; the Store signs the package, but only a full MSIX
    (app + menu extension packaged together), not a sparse package. The Windows App Certification Kit (also
    available as a GitHub action) checks an app before submission.
  - **Azure Artifact Signing** (ex Trusted Signing): about $10/month; individual accounts are limited to the US
    and Canada.
  - Current unsigned setup: `-AllowUnsigned` needs an elevated prompt; a per-user (non-admin) attempt fails with
    `0x80073D2B`. Projects using the same unsigned approach: Filee, PermaDel.
- ARM64 (DLL and app), more languages.