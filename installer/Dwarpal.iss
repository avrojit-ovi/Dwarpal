; Dwarpal installer — Inno Setup 6.
; Build with scripts\build-installer.ps1 (publishes first) or:
;   ISCC.exe /DAppVersion=1.0.0 installer\Dwarpal.iss

#define AppName "Dwarpal"
#define AppPublisher "Dwarpal"
#define AppExeName "Dwarpal.exe"
#define PublishDir "..\artifacts\publish"

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
; AppId must never change, otherwise upgrades install side by side.
AppId={{6E2C1B4A-9D3F-4A7E-9C1B-2F5A8D7E4C10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=Output
OutputBaseFilename=Dwarpal-Setup-{#AppVersion}
SetupIconFile=..\src\Dwarpal.App\Assets\Branding\dwarpal.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Framework-dependent publish output. The .NET 8 Desktop Runtime is checked in [Code].
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Dwarpal creates these Task Scheduler entries when the user enables auto-start / watchdog.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN Dwarpal /F"; Flags: runhidden; RunOnceId: "DeleteDwarpalAutostart"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN DwarpalWatchdog /F"; Flags: runhidden; RunOnceId: "DeleteDwarpalWatchdog"

[Code]
const
  DesktopRuntimeKey =
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  DesktopRuntimeUrl = 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0';

function HasDesktopRuntimeFolder(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;

  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec) then
  try
    repeat
      // $10 = FILE_ATTRIBUTE_DIRECTORY
      if (FindRec.Attributes and $10) <> 0 then
      begin
        Result := True;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function HasDotNet8DesktopRuntime(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;

  if RegGetValueNames(HKLM64, DesktopRuntimeKey, Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
    begin
      if Pos('8.', Names[I]) = 1 then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;

  // Fallback for installs where the registry value naming differs from the folder layout.
  Result := HasDesktopRuntimeFolder();
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  if HasDotNet8DesktopRuntime() then
    Exit;

  if WizardSilent then
  begin
    // Silent deployment: fail clearly instead of prompting on a headless machine.
    Result := False;
    Exit;
  end;

  if MsgBox(
       'Dwarpal needs the .NET 8 Desktop Runtime (x64).' + #13#10#13#10 +
       'Open the Microsoft download page now? Setup will close so you can install the runtime first.',
       mbConfirmation, MB_YESNO) = IDYES then
    ShellExec('open', DesktopRuntimeUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);

  Result := False;
end;
