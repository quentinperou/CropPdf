; Crop PDF installer (Inno Setup 6), in English and French (picked from the Windows display language).
; Built by scripts\build.ps1, which prepares dist\ first:
;   ISCC.exe /DAppVersion=1.0.0 installer\CropPdf.iss
; The app also runs it to install an update (src\CropPdf\Updater.cs): silent, closing the running app, /RELAUNCH=1.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

; Windows file versions are numbers only: 1.1.0 for a 1.1.0-beta.1 pre-release.
#if Pos("-", AppVersion) > 0
  #define AppFileVersion Copy(AppVersion, 1, Pos("-", AppVersion) - 1)
#else
  #define AppFileVersion AppVersion
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
AppSupportURL=https://github.com/quentinperou/CropPdf
AppUpdatesURL=https://github.com/quentinperou/CropPdf/releases
VersionInfoVersion={#AppFileVersion}
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
#ifdef UninstallLog
; Checking the uninstall (ISCC /DUninstallLog): the uninstaller always writes "Uninstall Log ....txt" in %TEMP%,
; also when started from Windows Settings, which passes no /LOG.
UninstallLogging=yes
#endif

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

[Run]
; Update installed from the app (silent, /RELAUNCH=1): start the app again, as the user, not elevated.
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser skipifnotsilent; Check: RelaunchRequested

[Code]
{ Set by the app when it installs an update (Updater.InstallerArguments). }
function RelaunchRequested: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

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

function SetEnvironmentVariable(Name, Value: String): BOOL;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';

procedure RegisterContextMenu;
begin
  { The paths reach PowerShell through environment variables (inherited by the child process), not inside the
    command text: a quote in the install folder (L'appli, or the typographic ’ that PowerShell also treats as a
    quote) would end a quoted string and break the command. }
  SetEnvironmentVariable('CROPPDF_MSIX', ExpandConstant('{app}\CropPdf.Menu.msix'));
  SetEnvironmentVariable('CROPPDF_LOCATION', ExpandConstant('{app}'));
  if not RunPowerShell('Add-AppxPackage -Path $env:CROPPDF_MSIX -ExternalLocation $env:CROPPDF_LOCATION -AllowUnsigned') then
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

{ What the app creates for the user who runs it, removed for the account that uninstalls: the settings
  (settings.json), a downloaded update installer, and the "Show in the Windows 11 context menu" setting.
  Updates do not uninstall, so they keep all this. Other accounts of the PC keep their own (not reachable here),
  as does the original user when the UAC prompt was answered with another administrator account. }
procedure DeleteUserData;
var
  Settings, Downloads: String;
begin
  { Logged with unins000.exe /LOG="<file>". }
  Settings := ExpandConstant('{localappdata}\CropPdf');
  Downloads := ExpandConstant('{%TEMP}\CropPdf-Update');
  Log(Format('Deleting user data: %s (exists: %d), %s (exists: %d), HKCU\Software\CropPdf', [Settings,
    Ord(DirExists(Settings)), Downloads, Ord(DirExists(Downloads))]));
  Log(Format('  settings deleted: %d', [Ord(DelTree(Settings, True, True, True))]));
  Log(Format('  downloads deleted: %d', [Ord(DelTree(Downloads, True, True, True))]));
  Log(Format('  registry key deleted: %d', [Ord(RegDeleteKeyIncludingSubkeys(HKCU, 'Software\CropPdf'))]));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  Log(Format('Uninstall step %d', [Ord(CurUninstallStep)]));
  if (CurUninstallStep = usUninstall) and IsWindows11 then
    UnregisterContextMenu
  else if CurUninstallStep = usPostUninstall then
    DeleteUserData;
end;