; Crop PDF installer (Inno Setup 6), in English and French (picked from the Windows display language).
; Built by scripts\build.ps1, which prepares dist\ first:
;   ISCC.exe /DAppVersion=1.0.0 installer\CropPdf.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "Crop PDF"
#define AppExe "CropPdf.exe"
#define PackageName "CropPdf.ContextMenu"

[Setup]
AppId={{15AB190D-5F97-4B46-A972-C78CDA430D5C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=QuentinPerou
AppPublisherURL=https://github.com/quentinperou
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\Crop PDF
DisableProgramGroupPage=yes
; Admin rights: needed to register the unsigned context-menu package (Add-AppxPackage -AllowUnsigned).
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\dist
OutputBaseFilename=CropPdf-Setup-{#AppVersion}
SetupIconFile=..\src\CropPdf\Assets\CropPdf.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
; Only ask for the language when Windows uses one the installer does not have.
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
english.StartMenuName=Crop PDF
french.StartMenuName=Recadrer un PDF
english.MenuLabel=Crop PDF
french.MenuLabel=Recadrer le PDF
english.MenuNotRegistered=The "Crop PDF" entry could not be added to the new Windows 11 context menu.%nIt is still available under "Show more options".
french.MenuNotRegistered=L'entrée « Recadrer le PDF » n'a pas pu être ajoutée au nouveau menu clic droit de Windows 11.%nElle reste disponible via « Afficher plus d'options ».
english.RuntimeMissing=Crop PDF needs the Microsoft ".NET 10 Desktop Runtime", which is not installed on this PC.%nInstall it now?
french.RuntimeMissing=Crop PDF a besoin du « .NET 10 Desktop Runtime » (Microsoft), absent de ce PC.%nL'installer maintenant ?
english.RuntimeInstalling=Installing the .NET 10 Desktop Runtime...
french.RuntimeInstalling=Installation du .NET 10 Desktop Runtime…
english.RuntimeFailed=The automatic installation failed. The download page will open:%nchoose ".NET Desktop Runtime 10" (x64).
french.RuntimeFailed=L'installation automatique a échoué. La page de téléchargement va s'ouvrir :%nchoisissez « .NET Desktop Runtime 10 » (x64).

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Excludes: "CropPdfMenu.dll"; Flags: ignoreversion recursesubdirs createallsubdirs
; Explorer may still have the DLL loaded during an update: it is then replaced at the next restart.
Source: "..\dist\app\CropPdfMenu.dll"; DestDir: "{app}"; Flags: ignoreversion restartreplace uninsrestartdelete
Source: "..\dist\CropPdf.Menu.msix"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{cm:StartMenuName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Classic menu entry: the whole menu on Windows 10, "Show more options" on Windows 11.
; MUIVerb points at the localized string table of the DLL (IDS_MENU_TITLE = 101), so the label follows
; each user's display language; the default value is a fallback in the installer's language.
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\CropPdf"; ValueType: string; ValueName: ""; ValueData: "{cm:MenuLabel}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\CropPdf"; ValueType: string; ValueName: "MUIVerb"; ValueData: "@{app}\CropPdfMenu.dll,-101"
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\CropPdf"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExe}"",0"
Root: HKLM; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\CropPdf\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""

[Code]
function IsWindows11: Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major > 10) or ((Version.Major = 10) and (Version.Build >= 22000));
end;

function RunPowerShell(const Command: String): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Command + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

{ Windows 11 new context menu: sparse package pointing at the install folder. }
procedure UnregisterContextMenu;
begin
  RunPowerShell('Get-AppxPackage -Name {#PackageName} | Remove-AppxPackage');
end;

procedure RegisterContextMenu;
begin
  { A line may not start with '[' in an .iss file, even inside [Code]. }
  if not RunPowerShell(Format('Add-AppxPackage -Path ''%s'' -ExternalLocation ''%s'' -AllowUnsigned', [
    ExpandConstant('{app}\CropPdf.Menu.msix'), ExpandConstant('{app}')])) then
    MsgBox(CustomMessage('MenuNotRegistered'), mbInformation, MB_OK);
end;

{ The app is framework-dependent: it needs the .NET 10 Desktop Runtime. }
function IsDesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

procedure EnsureDesktopRuntime;
var
  ResultCode: Integer;
begin
  if IsDesktopRuntimeInstalled then
    Exit;
  if MsgBox(CustomMessage('RuntimeMissing'), mbConfirmation, MB_YESNO) <> IDYES then
    Exit;
  WizardForm.StatusLabel.Caption := CustomMessage('RuntimeInstalling');
  if not Exec('winget.exe', 'install --id Microsoft.DotNet.DesktopRuntime.10 -e --silent --accept-source-agreements --accept-package-agreements',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or not IsDesktopRuntimeInstalled then
  begin
    MsgBox(CustomMessage('RuntimeFailed'), mbInformation, MB_OK);
    ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    { Update: drop the previous registration before the files are replaced. }
    if IsWindows11 then
      UnregisterContextMenu;
  end
  else if CurStep = ssPostInstall then
  begin
    EnsureDesktopRuntime;
    if IsWindows11 then
      RegisterContextMenu;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usUninstall) and IsWindows11 then
    UnregisterContextMenu;
end;