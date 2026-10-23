; Dwarpal installer — Inno Setup 6.
; Build with scripts\build-installer.ps1 (publishes first and downloads the pinned
; .NET 8 Desktop Runtime installer) or:
;   ISCC.exe /DAppVersion=1.0.2 installer\Dwarpal.iss

#define AppName "Dwarpal"
#define AppPublisher "Dwarpal"
#define AppExeName "Dwarpal.exe"
#define PublishDir "..\artifacts\publish"

#ifndef AppVersion
  #define AppVersion "1.0.2"
#endif

; Kept in sync with scripts\build-installer.ps1. The setup bundles this file so a
; clean machine can install the prerequisite without a browser or download page.
#ifndef RuntimeInstaller
  #define RuntimeInstaller "..\artifacts\prereq\windowsdesktop-runtime-8.0.32-win-x64.exe"
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
; Official Microsoft .NET 8 Desktop Runtime installer. dontcopy keeps it out of
; {app}; [Code] extracts it to {tmp} only when the runtime is actually missing.
Source: "{#RuntimeInstaller}"; DestDir: "{tmp}"; Flags: dontcopy
; Framework-dependent app payload.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; postinstall entries default to the original (non-elevated) user, so CreateProcess
; failed with error 740 for Dwarpal's requireAdministrator manifest. Inherit Setup's
; elevated token and use ShellExecute so the app starts without a second UAC prompt.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent runascurrentuser shellexec

[UninstallRun]
; Dwarpal creates these Task Scheduler entries when the user enables auto-start / watchdog.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN Dwarpal /F"; Flags: runhidden; RunOnceId: "DeleteDwarpalAutostart"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN DwarpalWatchdog /F"; Flags: runhidden; RunOnceId: "DeleteDwarpalWatchdog"

[Code]
const
  DesktopRuntimeKey =
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  DesktopRuntimeUrl = 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0';
  DesktopRuntimeFileName = 'windowsdesktop-runtime-8.0.32-win-x64.exe';

var
  RuntimePage: TOutputProgressWizardPage;

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

procedure InitializeWizard;
begin
  RuntimePage := CreateOutputProgressPage(
    'Installing prerequisites',
    'Dwarpal needs the .NET 8 Desktop Runtime (x64).');
end;

function InstallDesktopRuntime(var NeedsRestart: Boolean): String;
var
  ResultCode, Attempt: Integer;
  RuntimeInstaller: String;
begin
  Result := '';

  if not WizardSilent then
  begin
    RuntimePage.SetText(
      'Installing the .NET 8 Desktop Runtime (x64)...',
      'This can take a few minutes. The Microsoft installer runs silently.');
    RuntimePage.SetProgress(0, 100);
    RuntimePage.Show;
  end;

  try
    try
      ExtractTemporaryFile(DesktopRuntimeFileName);
      RuntimeInstaller := ExpandConstant('{tmp}\' + DesktopRuntimeFileName);

      if not WizardSilent then
        RuntimePage.SetProgress(10, 100);

      if not Exec(
           RuntimeInstaller,
           '/install /quiet /norestart',
           '',
           SW_HIDE,
           ewWaitUntilTerminated,
           ResultCode) then
      begin
        Result := 'Setup could not start the .NET 8 Desktop Runtime installer. ' +
          'Install it manually from ' + DesktopRuntimeUrl + ' and then run Setup again.';
        Exit;
      end;

      if not WizardSilent then
        RuntimePage.SetProgress(90, 100);

      if (ResultCode = 3010) or (ResultCode = 1641) then
        NeedsRestart := True
      else if ResultCode <> 0 then
      begin
        Result := 'The .NET 8 Desktop Runtime installer failed with exit code ' +
          IntToStr(ResultCode) + '. Install it manually from ' + DesktopRuntimeUrl +
          ' and then run Setup again.';
        Exit;
      end;
    except
      Result := 'Setup could not install the .NET 8 Desktop Runtime. ' +
        'Install it manually from ' + DesktopRuntimeUrl + ' and then run Setup again.';
      Exit;
    end;
  finally
    if not WizardSilent then
      RuntimePage.Hide;
  end;

  // The runtime installer can return before the new runtime is visible to the
  // registry/folder checks, so poll briefly before declaring failure.
  for Attempt := 1 to 10 do
  begin
    if HasDotNet8DesktopRuntime() then
      Exit;
    Sleep(1000);
  end;

  Result := 'The .NET 8 Desktop Runtime was installed but could not be detected. ' +
    'Restart Windows and run Setup again.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  Result := '';

  if HasDotNet8DesktopRuntime() then
    Exit;

  if not WizardSilent then
  begin
    if MsgBox(
         'Dwarpal needs the .NET 8 Desktop Runtime (x64).' + #13#10#13#10 +
         'Setup will install it automatically now. This can take a few minutes.' + #13#10#13#10 +
         'Continue?',
         mbConfirmation, MB_YESNO) <> IDYES then
    begin
      Result := 'Dwarpal cannot be installed without the .NET 8 Desktop Runtime (x64).';
      Exit;
    end;
  end;

  Result := InstallDesktopRuntime(NeedsRestart);
end;
