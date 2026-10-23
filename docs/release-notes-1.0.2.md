# Dwarpal v1.0.2

Dwarpal 1.0.2 fixes the setup error that appeared immediately after installation:

> Unable to execute file: `C:\Program Files\Dwarpal\Dwarpal.exe`
> CreateProcess failed; code 740. The requested operation requires elevation.

## Fix

Dwarpal's executable declares `requireAdministrator`. Inno Setup's post-install entries run with the
original, non-elevated user token by default, so starting Dwarpal failed with error 740. The setup
now uses `runascurrentuser` to inherit Setup's elevated token and `shellexec` to honor the manifest,
so the app opens immediately after installation without a second UAC prompt.

There are no application behaviour changes in this release; it is the same 1.0.1 app with a corrected
installer.

## Install

- **Installer:** `Dwarpal-Setup-1.0.2.exe` — per-machine, elevated. The bundled .NET 8 Desktop
  Runtime (x64) is installed automatically when missing.
- **Portable:** `Dwarpal-1.0.2-portable.zip` — extract anywhere and run `Dwarpal.exe`. Data still
  lives in `%ProgramData%\Dwarpal`.

Verify downloads with `SHA256SUMS.txt` attached to the release.

## Verification

- `dotnet build Dwarpal.sln --configuration Release` passes with 0 warnings and 0 errors.
- 87 Core unit tests pass.
- The installer compiles with Inno Setup 6 and the release workflow builds/attaches both artifacts.

## Known limitations

- The binaries are not code-signed. Windows SmartScreen may warn on first run.
- The installer is larger than 1.0.0 because it now bundles the .NET 8 Desktop Runtime so setup can
  install the prerequisite without a browser download.
