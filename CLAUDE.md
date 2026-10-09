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
The repository is public and CI passes on push (tests, full build, installer uploaded as the `CropPdf-Setup`
artifact). **v1.0.0 is released**: the `v1.0.0` tag (on the initial commit) ran the release job, which published
`CropPdf-Setup-1.0.0.exe` in the GitHub Release. Release notes are written for 1.0.0 (bilingual), 1.1.0-beta.1 and
1.1.0 (English; the 1.1.0 notes link to the privacy policy).

**Updates, released as `v1.1.0-beta.1` (pre-release, Beta channel only; Stable “latest” is still 1.0.0)**:
startup check of the GitHub Releases, “new version” bar in the main window, *Updates* card in Settings (status,
check/update button, startup-check toggle, Stable/Beta channel), download + SHA-256 check + silent install that
closes and restarts the app; beta tags published as pre-releases (verified: the `v1.1.0-beta.1` tag gave a
pre-release). 61 unit tests pass (versions, channels, feed parsing, checksum, download). Checked on screen through
UI Automation with a build versioned 0.9.0 (“1.0.0 available”) and with 1.0.0 (“up to date”), light/dark,
English/French. **Update path tested by the author**: a 0.9.0 build (run from outside the install folder) on the
Beta channel offered 1.1.0-beta.1, downloaded it, installed it after UAC (first install on this PC: package
`Status : Ok`, classic verb registered) and the installed beta started; it then removed `%TEMP%\CropPdf-Update`.

**v1.1.0 is released (stable, “latest”)**: the update system of 1.1.0-beta.1, plus a “Source code on GitHub”
link in the About card. Users of 1.0.0 install it by hand once (1.0.0 has no update check); later versions are
offered by the app.

**Code signing: postponed** (author's decision, October 9, 2026: not needed for now; SmartScreen only shows on
the first manual install, updates from the app do not get it). SignPath Foundation is the chosen route for later
(over the Microsoft Store). The “Code signing policy” section was written then removed from the READMEs (text in
commit `0f1ec85`, restore it before applying); the “Privacy policy” section stays. Application form answers:
project name `CropPdf`, category Productivity, maintainer type individual, build system GitHub Actions, download
URL `…/CropPdf#installation` (that page must mention SignPath), privacy policy URL `…/CropPdf#privacy-policy`;
the weak point is reputation (brand-new project).

**`v1.2.0-beta.1` (pre-release, Beta channel)**: the **print window** (printer, paper incl. driver label sizes,
fit/actual size, live preview, printer and paper remembered, *More settings…* → Windows dialog), the installer fix
for quotes in the install folder, and the CI on Node 24 actions (this release is the first run of the new release
job). 68 unit tests pass. The window was checked on screen through a harness (A4 and a 55 × 85 mm card, light/dark,
English/French) and validated by the author, including a print to Microsoft Print to PDF matching the preview. This
PC has no label printer (only Fax, Print to PDF, XPS Writer, OneNote).

Not verified yet:
- shipping labels on the label printer from the print window (the main use case): do its label sizes show up in
  the paper list, is the printable area right, does the label come out right;
- an English Windows: the “Crop PDF” menu label was only checked with `LoadString` under a forced thread UI
  language, and the installer language auto-selection was only seen in French;
- Windows 10 (classic menu only);
- UAC answered with a *different* administrator account: the package is then registered for that account only;
- installing from the Release download (SmartScreen “Mark of the Web” path) on a clean PC;
- updating an *installed* copy: the Restart Manager closing the running app before replacing its files (the
  test above ran from another folder, so the app closed itself after the installer's exit code 0), declining
  the UAC prompt (“not installed” message), and the relaunched app being unelevated (`runasoriginaluser`, not
  checked: the app was already closed when looked at).

Next steps:
1. Update the installed 1.1.0-beta.1 (Beta channel) to 1.1.0 from its own Settings: checks the Restart Manager
   path and the relaunch; also try declining the UAC prompt once.
2. On the label printer's PC (Crop PDF 1.1.0 installed, Beta channel): update to 1.2.0-beta.1 from the app, then
   print shipping labels from the print window. If all is well, release the final `v1.2.0`.
3. Later, if the app spreads: code signing through SignPath Foundation (see “To do” → Signing).

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
| `src/CropPdf.Core` | Pure, tested logic: `CropGeometry` (screen → PDF coordinates), `PdfCropper` (writing with PDFsharp), `OutputPath` (`_crop` name), `PrintLayout` (size/rotation on the sheet), `PrintSchema` (paper sizes from a driver's PrintCapabilities XML, partial paper ticket), `CropAreaTooSmallException`, `SemanticVersion` (SemVer parsing/ordering), `UpdateFeed` (GitHub Releases: parsing, channel choice, download, SHA-256 check). No UI text. |
| `src/CropPdf` | WPF .NET 10. `MainWindow` (open, zoom, render, save, update bar), `SelectionOverlay` (draw/move/resize), `PdfPageRenderer` (rendering through `Windows.Data.Pdf`), `PageItem` (page model), `SettingsWindow` + `AppSettings` (language, updates, print choices), `PrintWindow` + `Printing` (print window, printers, sheet, job), `Updater` (startup check, install), `Localization.cs` (`Strings`, `{local:Tr}`), `Properties/Strings*.resx`. |
| `src/CropPdf.Shell` | C++ DLL (WRL) implementing `IExplorerCommand`: has File Explorer start `CropPdf.exe "<file>"`, located next to it. Localized label in `CropPdfMenu.rc`. |
| `package/` | `AppxManifest.xml` of the “sparse” package + logos. Packed by `makeappx` in `build.ps1`. |
| `installer/CropPdf.iss` | Inno Setup installer (English/French): copies to Program Files, registers the package, adds the classic verb. |
| `scripts/` | `build.ps1`, `make-icons.ps1` (regenerates the `.ico` and logos; the generated files are committed). |
| `.github/workflows/build.yml` | CI: build + tests on every push; on a `v*` tag, publishes the installer in a Release (the installer goes from the Windows job to the release job as an artifact, uploaded for tags only and kept 1 day). Actions on their Node 24 majors (checkout v7, setup-dotnet v6, upload-artifact v7, download-artifact v8, action-gh-release v3). |
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
- **Printing** (`PrintWindow`, `Printing`, `PrintSchema`): nothing is saved. The print button / `Ctrl+P` open our own
  print window (chosen over a preview inside the Windows dialog, see “To do”): printer, paper, *fit to paper* /
  *actual size*, live preview, Print (`Enter`), and *More settings…* which hands over to WPF's `PrintDialog`.
  - Papers come from the driver's **PrintCapabilities XML** (`PrintSchema.ReadPaperSizes`), not WPF's object
    model: label printers declare their labels as driver-specific options (own namespace) with a localized
    `DisplayName`. The choice is applied by name through a partial ticket (`PrintSchema.PaperTicket`, also forcing
    portrait) merged with `MergeAndValidatePrintTicket`; the printable area then comes from
    `GetPrintCapabilities(ticket).PageImageableArea` (`PrintSheet`).
  - Driver calls run on a background **STA thread** with their own `PrintQueue` (network printers can be slow);
    tickets cross threads as XML. Shared printers are opened as `\\server\printer` (`Printing.OpenQueue`).
  - The area is rendered by `Windows.Data.Pdf` with `SourceRect` (displayed coordinates, rotation included —
    verified on a `/Rotate 90` page) at the printer resolution (`PageResolution`, 300 dpi by default), laid out by
    `PrintLayout`: *fit to paper* (turned a quarter when that gives a bigger print) or *actual size* (reduced only
    when too large, never turned). `Printing.BuildPage` builds the sheet (area centered in the printable part);
    the preview shows the same element with a lower-resolution bitmap, plus the printable area dashed. The job is
    sent with `PrintQueue.CreateXpsDocumentWriter(queue).Write(page, ticket)`, job name in `CurrentJobSettings`.
  - Remembered (`AppSettings`): `PrintPrinter`, `PrintPapers` (paper per printer), `PrintScaling`, saved on Print.
  - The print button is deliberately discreet (icon only, like Settings), as asked by the author.
- **Updates** (`Updater`, `UpdateFeed`): the source is the GitHub Releases of `quentinperou/CropPdf`
  (`/releases?per_page=30`, unauthenticated: 60 requests/hour per IP, a failure at startup is silent). A release
  counts if it is not a draft, its tag parses as SemVer and it has a `CropPdf-Setup-*.exe` asset. *Stable* =
  non-pre-releases only; *Beta* = everything; the newest version above the running one is offered (never a
  downgrade: back on Stable, a beta is kept until a newer stable). The running version is the assembly's
  `InformationalVersion` without `+commit`. Checked at every startup (toggle in Settings, default on) and on
  demand; offered, never forced (installing needs UAC). Install: download to `%TEMP%\CropPdf-Update` (deleted at
  the next startup), SHA-256 compared with the asset `digest` published by GitHub while the file is kept open
  read-only (the checked file is the one that runs), then `/SP- /SILENT /NORESTART /CLOSEAPPLICATIONS
  /FORCECLOSEAPPLICATIONS /RELAUNCH=1`. The installer starts unelevated and asks for UAC itself, so the app does
  not exit on its own: it waits for the installer, which closes it through the Restart Manager; a non-zero exit
  code means “not installed” (UAC declined, error). `/RELAUNCH=1` makes the `[Run]` entry start the new version
  `runasoriginaluser` (not elevated).
- **Beta releases**: `<Version>1.3.0-beta.1</Version>` + tag `v1.3.0-beta.1`; the workflow marks tags containing
  `-` as pre-releases. The installer's `VersionInfoVersion` uses the numeric part only (ISPP `Pos`/`Copy`).

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
- **Release notes** (GitHub Releases) are written in English only (since v1.1.0-beta.1; v1.0.0 is bilingual).
  They describe the changes, how to install (SmartScreen, UAC) and give the installer's SHA-256 (asset `digest`).
  Once releases are signed, they must also link to the README's “Code signing policy” (SignPath requirement for
  download pages).
- The README's **Privacy policy** section (`#privacy-policy`, linked from the 1.1.0 release notes) lists every
  network access of the app: keep it in step with the code (today only the update check, the update download and
  links opened on click).
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
- Inno Setup → PowerShell: never put a path inside the `-Command` text (a `'` or a typographic `’` in the folder
  name ends PowerShell's quoted string); pass it through an environment variable (`SetEnvironmentVariableW`,
  inherited by the child) and use `$env:NAME`, as `RegisterContextMenu` does.
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
- WPF's Fluent theme has the `ToggleSwitch*` brushes but no toggle switch style: `SettingsWindow` defines one
  (`ToggleSwitch`, a `CheckBox` template). Theme resource keys can be listed with a file-based app
  (`dotnet run keys.cs` with `#:property UseWPF=true`, `PublishAot=false`, `NoWarn=WPF0001`).
- The Inno Setup installer is `asInvoker` and elevates by restarting itself: starting it never throws for a
  declined UAC prompt; only its exit code tells (0 = installed).
- The first launch after an install is slower (cold .NET start): wait a few seconds before checking the window.
- After moving or renaming the project folder, CMake refuses the old cache in `build\shell` ("CMakeCache.txt
  directory is different"): delete `build\shell` and build again.
- On Windows 11, WPF's `PrintDialog` (*More settings…*) appears as the **modern print dialog**, hosted by
  `explorer.exe` (window “Crop PDF – Imprimer”, class `ApplicationFrameWindow`), and shows “This app doesn't
  support print preview” (classic printing API, see “To do”). It may ignore the ticket it is given (dotnet/wpf#8355).
- **Never automate printing on a machine with real printers** (the Windows dialog or the *Print* button of our
  window). UI Automation once reported “Microsoft Print to PDF” as selected while the list still showed the default
  printer (a label printer): a click on Print could have wasted labels. Test printing by hand, or check the layout
  through `PrintLayout` tests and the preview.
- A selection cannot be drawn with posted mouse messages (WPF checks the real button state). To look at the print
  window without drawing, use a small harness project (outside the repo) that references `src\CropPdf\CropPdf.csproj`,
  creates an `Application` with `ThemeMode`, loads a `PdfPageRenderer` and shows `new PrintWindow(new PrintRequest(...))`;
  give it `<ApplicationIcon>` (windows take the exe's icon) and capture it with `PrintWindow`.

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
  files (single page, boxes, rotation kept, overwrite, `_crop` name), print layout, updates (`SemanticVersion`
  ordering, `UpdateFeed` parsing of a GitHub-shaped JSON, Stable/Beta choice, checksum, download through a fake
  `HttpMessageHandler`), print schema (paper sizes of a label driver, prefixes, selected paper, partial ticket),
  plus `LocalizationTests`.
- The UI is checked by hand: open the PDF from `tools\SamplePdf` (rotated page, page with an existing CropBox),
  draw, save, reopen the result.
- The UI can also be driven with UI Automation (`System.Windows.Automation`) without moving the mouse: icon
  buttons have accessible names, and the Settings window is a child of the main window in the UIA tree.
  `PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT)` from a DPI-aware process captures a window for review.
- Update offer on screen: build a copy with an older version (`dotnet build src\CropPdf -p:Version=0.9.0 -o <dir>`)
  and start it: it finds the latest Release. Do not click *Update* there unless installing is intended.

## To do (see also the README)

- **Preview inside the Windows 11 print dialog**: not planned (audit of October 9, 2026; our print window was built
  instead). It would need `IPrintManagerInterop` (HWND) → `PrintTask` with an `IPrintDocumentSource` that also
  implements `IPrintDocumentPageSource` and `IPrintPreviewPageCollection` (Paginate/MakePage, called again on every
  option change), preview pages drawn on DXGI surfaces (`IPrintPreviewDxgiPackageTarget::DrawPage`) and the job
  through `ID2D1PrintControl` (off the UI thread, multithreaded D2D factory). WPF has nothing built in; no desktop
  toolkit does it. Best as a separate C++/WinRT DLL (not in `CropPdfMenu.dll`) taking its layout from C#: about
  600–900 lines of C++, 1–2 weeks, risky (threading, DPI, `IPdfRendererNative::RenderPageToDeviceContext`
  documented as UWP-only). `PrintTaskOptions.GetPageDescription` would give the page and imageable sizes.
- Signing (findings from October 2026, to re-check before acting):
  - **SignPath Foundation** (chosen, postponed): free for open source. Requires an OSI license with no proprietary
    parts, an existing release and active maintenance, MFA for the whole team, builds in CI with a manual approval
    of each release, and a “Code signing policy” section on the project page (written in commit `0f1ec85`, then
    removed while signing is postponed). The certificate is
    issued to SignPath Foundation, which is shown as the publisher. The manifest `Publisher` must then become
    `CN=SignPath Foundation, O=SignPath Foundation, L=Lewes, S=Delaware, C=US`. Once signed, the package can be
    registered per user without admin rights (no more `-AllowUnsigned`).
    Steps when resuming: (1) restore the “Code signing policy” section (commit `0f1ec85`); (2) MFA on GitHub
    (author); (3) maybe an installer option for the update check, see below; (4) apply on signpath.org (author);
    (5) once accepted: SignPath GitHub integration and CI signing step with manual approval, manifest `Publisher`,
    installer without `-AllowUnsigned` (possibly per-user, no UAC), release notes linking to the policy, README
    status note removed.
    Their rules (draft terms): software that sends data to systems the user did not choose must show its privacy
    policy during installation and offer an installation option to turn it off. The startup update check sends
    no user data (only the request itself: IP and version reach GitHub) and can be turned off in Settings; if
    SignPath considers it a data transfer, add an Inno Setup task (“Check for updates at startup”, with the
    privacy note) whose choice becomes the default of `AppSettings.CheckForUpdatesAtStartup`.
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