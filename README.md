<p align="center">
  <img src="docs/icon-256.png" width="96" alt="Crop PDF icon">
</p>

<h1 align="center">Crop PDF</h1>

<p align="center">
  Crop a PDF page in two clicks, straight from the Windows 11 right-click menu.
</p>

<p align="center">
  <b>English</b> · <a href="README.fr.md">Français</a>
</p>

---

## Features

- **Right-click a PDF → “Crop PDF”**, in the new Windows 11 context menu (and under “Show more options” / on Windows 10).
- Every page is shown: **draw a rectangle** on the page you want, then move it or resize it with its handles.
- Two ways to save:
  - **Save a copy**: creates `name_crop.pdf` next to the original (`name_crop_2.pdf`… if the name is taken);
  - **Overwrite original**: replaces the original file (asks for confirmation when it has several pages).
- **The saved file contains only the cropped page**: the other pages are removed.
- **Print the cropped area directly**, without saving anything (printer button or `Ctrl+P`):
  - *fit to paper*: fills the sheet or the label, turned a quarter when it fits better (ideal for shipping
    labels on a label printer);
  - *actual size*: 100 %, centered, only reduced when larger than the paper.
- Lossless: text stays selectable and vector graphics stay vector.
- Windows 11 look (Fluent, Mica) that **follows the light/dark theme** and the accent color of Windows.
- **English and French**: the app, the context menu and the installer follow the Windows display language;
  the app language can also be chosen in **Settings** (gear icon).

### Shortcuts

| Action | Shortcut |
|--------|----------|
| Open a PDF | `Ctrl+O` (or drop the file into the window) |
| Save a copy | `Ctrl+S` |
| Print the selected area | `Ctrl+P` (with the print mode used last) |
| Clear the selection | `Esc` |
| Zoom | `Ctrl` + mouse wheel, `Ctrl` + `+` or `Ctrl` + `-` |
| Fit to window | `Ctrl+0` |
| Settings | `Ctrl+,` |

> **Good to know:** like Acrobat, cropping *hides* what lies outside the area, it does not erase it from the
> file. To remove confidential information, use a redaction tool.

## Installation

1. Download `CropPdf-Setup-x.y.z.exe` from the [Releases](../../releases) page.
2. Run it. As the installer is not signed, Windows SmartScreen may show “Windows protected your PC”:
   click **More info → Run anyway**.
3. Accept the administrator prompt (UAC).

The installer:

- copies the app to `C:\Program Files\Crop PDF` and adds it to the Start menu;
- adds the “Crop PDF” entry to the right-click menu of `.pdf` files;
- offers to install the Microsoft **.NET 10 Desktop Runtime** if it is missing.

To uninstall: **Settings → Apps → Installed apps → Crop PDF**.

**Requirements:** Windows 11 x64 for the new context menu (Windows 10 2004+ works, with the entry in the classic menu).

## Development

### Prerequisites

Everything installs with `winget` (built into Windows 11), from a terminal:

| Tool | Purpose | Install |
|------|---------|---------|
| **Git** | Get the code | `winget install Git.Git` |
| **.NET 10 SDK** | Build the app (C# / WPF) and run the tests | `winget install Microsoft.DotNet.SDK.10` |
| **Visual Studio Build Tools 2026**, “Desktop development with C++” workload | Build the context menu DLL (MSVC, CMake, Ninja) and the MSIX package (`makeappx`, Windows SDK 10.0.26100) | see below |
| **Inno Setup 6** | Build the installer | `winget install JRSoftware.InnoSetup` |

```powershell
winget install Microsoft.VisualStudio.BuildTools --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

Visual Studio 2026 (Community or above) with the same workload works too. Any editor (Visual Studio, VS Code,
Rider) is fine for the C# code.

### Build

```powershell
.\scripts\build.ps1
```

The script runs the tests, then produces in `dist\`:

| File | Content |
|------|---------|
| `app\` | the app (`CropPdf.exe`…) and the menu DLL (`CropPdfMenu.dll`) |
| `CropPdf.Menu.msix` | the “sparse” package that declares the Windows 11 menu entry |
| `CropPdf-Setup-<version>.exe` | the installer |

Options: `-SkipTests`, `-NoInstaller`.

### While developing

```powershell
dotnet test tests\CropPdf.Tests                                 # unit tests
dotnet run --project src\CropPdf -- "C:\path\to\file.pdf"       # run the app on a PDF
```

Set the `CROPPDF_THEME` environment variable to `Light` or `Dark`, or `CROPPDF_LANG` to `en` or `fr`,
before starting the app to check the other theme or language without changing any setting.

The context menu can only be tested **installed**: run `dist\CropPdf-Setup-<version>.exe`
(installing again replaces the previous install).

Two small dev tools help:

```powershell
dotnet run --project tools\SamplePdf -- C:\temp\sample.pdf     # 3-page test PDF (rotated page, existing CropBox)
dotnet run --project tools\InvokeMenu -- C:\temp\sample.pdf    # run the installed menu command, like File Explorer
```

### Code layout

```
src/
  CropPdf.Core/     Cropping logic, no UI (PDFsharp): coordinate conversion, writing the PDF
  CropPdf/          WPF app: page display (Windows.Data.Pdf), selection, saving, settings, UI strings
  CropPdf.Shell/    C++ DLL for the Windows 11 context menu (IExplorerCommand), with its localized label
package/            Manifest and logos of the “sparse” package (the app identity for the Windows 11 menu)
installer/          Inno Setup script of the installer
scripts/            build.ps1 (full build), make-icons.ps1 (regenerates the icons)
tests/              Unit tests (xUnit)
docs/               README icon
.github/workflows/  Automated build on GitHub (and release publishing)
tools/              Dev tools: SamplePdf (test PDF), InvokeMenu (runs the installed menu command)
```

### Translations

| What | Where |
|------|-------|
| App texts | `src/CropPdf/Properties/Strings.resx` (English, the default) and `Strings.fr.resx` (French). The tests check that both have the same keys and placeholders. |
| Context menu label | String table in `src/CropPdf.Shell/CropPdfMenu.rc` |
| Installer messages | `[Languages]` and `[CustomMessages]` in `installer/CropPdf.iss` |
| Languages offered in Settings | `AppSettings.SupportedLanguages` in `src/CropPdf/AppSettings.cs` |

Adding a language means updating these four places, plus a `README.<language>.md` if you like.

### How the Windows 11 menu works

The new context menu only accepts apps that have a **package identity**. The app remains a classic desktop
app, but the installer registers a small “sparse” package (`package/AppxManifest.xml`) that points at the
install folder and declares `CropPdfMenu.dll`, which asks File Explorer to start `CropPdf.exe` on the chosen
file. This package is **not signed**: Windows 11 accepts it with `Add-AppxPackage -AllowUnsigned` from an
administrator session, hence the UAC prompt during installation.

### Releasing a version

1. Update `<Version>` in `Directory.Build.props`.
2. Commit, then create and push a tag: `git tag v1.2.0`, then `git push origin v1.2.0`.
3. The GitHub Actions workflow builds, tests and publishes the installer in a Release.

## Roadmap

- [ ] **Updates**: at startup (at most once a day), query the GitHub Releases; if a newer version exists, show it in the app with an “Update” button that downloads and runs the new installer.
- [ ] **Free signing** of the installer and package through [SignPath Foundation](https://signpath.org) (for open source projects that already have a release): no more SmartScreen warning, and no administrator rights needed for the menu.
- [ ] Possibly, publishing on the **Microsoft Store** (free for individuals, the Store signs the app); the [Windows App Certification Kit](https://learn.microsoft.com/windows/uwp/debug-test-perf/windows-app-certification-kit) checks the app before submission ([GitHub action](https://github.com/marketplace/actions/run-the-windows-app-certification-kit)).
- [ ] **Print preview** in the Windows 11 print dialog. It currently says “This app doesn't support print preview”: WPF prints through the classic printing API, which cannot provide preview pages. It needs the modern print API (`PrintManager` + `IPrintDocumentSource`, preview pages drawn with Direct2D).
- [ ] ARM64 build.
- [ ] More languages.

## License

[MIT](LICENSE.md). Uses [PDFsharp](https://github.com/empira/PDFsharp) (MIT).

## Credits

Developed by [QuentinPerou](https://github.com/quentinperou), with [Claude Code](https://claude.com/claude-code) (Anthropic).
