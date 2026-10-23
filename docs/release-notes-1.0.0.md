# Dwarpal v1.0.0

First complete release of Dwarpal, a Windows app locker that stands in front of the programs you
choose with a full-screen lock screen, a PIN or password, and an audit trail.

> Unsigned build: Windows SmartScreen may warn and Smart App Control can block it. See
> [Code signing](https://github.com/avrojit-ovi/Dwarpal#code-signing) before rolling it out widely.

## Highlights

**Locking**
- Pick protected apps from installed programs / Start-menu shortcuts, or browse to an `.exe`.
- WMI process-start watch, `NtSuspendProcess` / `NtResumeProcess`, and a full-screen topmost lock
  screen per app (virtual-screen sized, blurred snapshot, Alt+F4/Escape blocked).
- Auto re-lock after background/minimise or system idle: `Never`, 1, 2, 5, 10, 15, 30, 60 minutes
  with a 0–60 s grace period; changes apply live.
- Panic hotkey **Ctrl + Alt + L** locks every running protected app instantly, including outside a
  scheduled protection window.

**Credential and tamper resistance**
- Argon2id (64 MiB, t=3, p=4) PIN/password hashing, DPAPI (LocalMachine) at rest, recovery key with
  rotation.
- Escalating lockout: 5 failures → 30 s, doubling per lockout to 15 min; persisted across restarts
  and enforced on every credential surface.
- Intruder selfies on the 3rd failure and each lockout, DPAPI-encrypted, viewable from the log.
- Elevated app, single-instance guard, tray/background running, credential-gated exit, a Task
  Scheduler watchdog and a `BUILTIN\Users` deny-terminate DACL.

**Control and visibility**
- Profiles: named app sets; exactly one active at a time (or *All protected apps*).
- Protection windows: weekday + start/end minute windows, overnight wrap, enable/disable, live
  “active now / paused now” state; no window means 24/7.
- Activity log with search, success/failure markers, selfie viewer and UTF-8 CSV export (formula
  injection guarded).
- Settings: auto re-lock, grace, sign-in auto-start, watchdog, selfie capture, DACL status, panic
  hotkey registration status.

**Packaging**
- Inno Setup 6 installer: per-machine elevated install, Start-menu/desktop shortcuts, files-in-use
  handling, uninstall entry, scheduled-task cleanup, data retained on uninstall.
- Setup checks for the .NET 8 Desktop Runtime (x64) and offers the Microsoft download page when it
  is missing; silent installs abort instead of prompting.
- `scripts/build-installer.ps1` publishes a version-stamped `win-x64` build and compiles
  `Dwarpal-Setup-<version>.exe`.
- `scripts/build-portable.ps1` produces `Dwarpal-<version>-portable.zip` with a readme covering the
  data location, removal steps and scheduled-task cleanup; `-SelfContained` optionally bundles the
  runtime.
- `.github/workflows/release.yml` runs tests and builds both artifacts on a `v*` tag, then attaches
  them to the GitHub release.

## Requirements

- Windows 10 21H2 / Windows 11, x64
- .NET 8 **Desktop** Runtime (the installer is framework-dependent)
- Administrator rights (UAC prompt on every launch)
- A webcam if you want intruder selfies

## Verification

- `dotnet build Dwarpal.sln`: 0 warnings, 0 errors.
- `dotnet test tests/Dwarpal.Core.Tests/...`: **86 passed, 0 failed**.
- `installer/Output/Dwarpal-Setup-1.0.0.exe` compiled successfully with Inno Setup 6 (7.3 MiB).
- `artifacts/Dwarpal-1.0.0-portable.zip` built successfully (8.1 MB, 19 files) and was verified to
  contain `Dwarpal.exe`, its runtime config and `README-portable.txt`.
- The CI workflow could not be executed here (no remote/Actions); its YAML was reviewed manually.
- Manual UI checks are listed per phase in the README; they require an interactive elevated session
  (lock screen, camera, hotkey, tray) and were not run headless here.

## Known issues

- Binaries are unsigned: SmartScreen may warn, and Smart App Control / WDAC can block the app until
  it is signed with a trusted certificate.
- Safe Mode and an administrator with physical access can bypass any user-mode locker.
- The lock screen is one window spanning the virtual screen; mixed-DPI multi-monitor setups may
  scale the backdrop slightly.
- Windows/Alt+Tab are not blocked; the protected app stays suspended, but other windows remain
  visible.
- Schedule boundaries are polled every 20 s (up to ~20 s late).
- Panic hotkey is fixed at Ctrl+Alt+L; a conflicting app makes registration fail (shown in
  Settings).
- Uninstall keeps `%ProgramData%\Dwarpal` by design; delete it manually to wipe the vault.
