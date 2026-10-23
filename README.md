# Dwarpal — Windows App Locker

[![CI](https://github.com/avrojit-ovi/Dwarpal/actions/workflows/ci.yml/badge.svg)](https://github.com/avrojit-ovi/Dwarpal/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/avrojit-ovi/Dwarpal?sort=semver)](https://github.com/avrojit-ovi/Dwarpal/releases/latest)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0b1f33)](#requirements)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](#requirements)
[![License](https://img.shields.io/badge/license-Proprietary-lightgrey)](LICENSE)

<img src="src/Dwarpal.App/Assets/Branding/dwarpal-logo-512.png" width="220" alt="Dwarpal logo — navy shield, gold torana and padlock">

Dwarpal is a Windows app locker in the spirit of a phone app locker: pick the programs you want to
protect, and Dwarpal stands in front of them with a full-screen lock screen, a PIN/password, and an
audit trail.

**Download v1.0.0:** [installer](https://github.com/avrojit-ovi/Dwarpal/releases/download/v1.0.0/Dwarpal-Setup-1.0.0.exe)
· [portable ZIP](https://github.com/avrojit-ovi/Dwarpal/releases/download/v1.0.0/Dwarpal-1.0.0-portable.zip)
· [checksums](https://github.com/avrojit-ovi/Dwarpal/releases/download/v1.0.0/SHA256SUMS.txt)
· [all releases](https://github.com/avrojit-ovi/Dwarpal/releases/latest)
· [release notes](docs/release-notes-1.0.0.md)

**Jump to:** [Requirements](#requirements) · [Build](#build-and-test) ·
[Installer](#build-the-installer) · [Portable](#build-the-portable-zip) ·
[First run](#first-run) · [Architecture](docs/ARCHITECTURE.md) ·
[Security design](#security-design-phase-1) · [Roadmap](#roadmap) ·
[Limitations](#known-limitations-honest-list)

> **Status: v1.0.0 — all planned phases complete.** Credentials, encrypted storage, setup wizard,
> recovery flow, protected-app selection, WMI process watch, suspend/resume, the full-screen lock
> screen, auto re-lock, settings, sign-in auto-start, tray/background running, a Task Scheduler
> watchdog, single-instance enforcement, process DACL hardening, escalating lockout, encrypted
> intruder selfies, the searchable activity log with CSV export, profiles, protection-window
> schedules, the panic hotkey and an Inno Setup installer are implemented. The build is not code
> signed yet — see [Code signing](#code-signing) before distributing.

---

## Solution layout

```
Dwarpal.sln
src/
  Dwarpal.Core/       Shared domain logic (no UI)
    Applications/     Installed app scanner + .lnk shortcut resolver
    Data/             SQLite database, migrations, repositories
    Models/           Credential, activity and protected-app models
    Monitoring/       WMI process start watcher, foreground/minimize monitor, idle detector
    Processes/        NtSuspendProcess / NtResumeProcess wrapper
    Security/         Argon2id hashing, policy, recovery keys, DPAPI, matching
    Services/         Activity logging
  Dwarpal.App/        WPF UI (MVVM, CommunityToolkit.Mvvm)
    Assets/Branding/  Logo/icon SVG sources, rendered PNGs and the multi-size .ico
    Services/         Composition root, protection engine, auto-start, icons, screen capture
    Themes/           Deep-navy + gold design system
    Views/            Setup, unlock, recovery, dashboard, protected apps, profiles, schedule,
                      activity, settings, lock screen
    ViewModels/       MVVM view models
    Controls/         Reusable PIN pad
  Dwarpal.Service/    Windows service host (privileged heartbeat; interactive watchdog uses Task Scheduler)
tests/
  Dwarpal.Core.Tests/ xUnit tests for hashing, policy, recovery, database, profiles, schedules
installer/
  Dwarpal.iss        Inno Setup 6 script (install, shortcuts, uninstall, task cleanup)
  Output/            Local build output (git-ignored)
dist/
  Dwarpal-Setup-1.0.0.exe / Dwarpal-1.0.0-portable.zip / SHA256SUMS.txt
scripts/
  build-installer.ps1 / build-portable.ps1
.github/
  workflows/         CI (push/PR) and tag-triggered packaging
  ISSUE_TEMPLATE/    Bug and feature forms
docs/
  ARCHITECTURE.md / release-notes-1.0.0.md / portable-readme.txt
```

## Requirements

- Windows 10 21H2 / Windows 11 (x64)
- .NET 8 SDK (build), .NET 8 Desktop Runtime (run)
- Administrator rights (the app manifest requires elevation)
- Inno Setup 6 (builds the installer only)
- No extra runtime dependencies (SQLite and Argon2id are bundled)

## Build and test

```powershell
dotnet restore Dwarpal.sln
dotnet build Dwarpal.sln
dotnet test tests/Dwarpal.Core.Tests/Dwarpal.Core.Tests.csproj
```

Run the app (accept the UAC prompt):

```powershell
.\src\Dwarpal.App\bin\Debug\net8.0-windows\Dwarpal.exe
```

Because `app.manifest` declares `requireAdministrator`, launching the EXE directly is the supported
way to run it. Running `dotnet Dwarpal.dll` bypasses the manifest and is not supported.

## Build the installer

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version 1.0.0
```

The script publishes a framework-dependent `win-x64` build to `artifacts\publish`, compiles
`installer\Dwarpal.iss` with Inno Setup 6 and writes
`installer\Output\Dwarpal-Setup-1.0.0.exe`. If Inno Setup is not on the default path, install it from
[jrsoftware.org](https://jrsoftware.org/isdl.php). `-DotnetPath` can point at a specific `dotnet.exe`.

You can also compile the script directly after publishing:

```powershell
ISCC.exe /DAppVersion=1.0.0 installer\Dwarpal.iss
```

### What the installer does

- Installs to `%ProgramFiles%\Dwarpal` (per-machine, elevation required), with Start-menu and
  optional desktop shortcuts, and a `Dwarpal` entry in **Apps & features**.
- Ships the framework-dependent WPF app. Setup checks for the **.NET 8 Desktop Runtime (x64)** in
  the registry and in `Program Files\dotnet\shared`, and offers the Microsoft download page when it
  is missing; silent installs abort instead of prompting.
- Aborts or asks to close a running Dwarpal through Inno's Restart Manager / files-in-use handling.
  Exit Dwarpal from the tray before upgrading so suspended protected apps are resumed cleanly.
- On uninstall, removes the `Dwarpal` (auto-start) and `DwarpalWatchdog` Task Scheduler entries if
  they exist. It deliberately **keeps** `%ProgramData%\Dwarpal` (credential hash, settings, activity
  log, selfies) so an uninstall/reinstall does not lose the vault; delete that folder manually to
  wipe it.
- Silent install for managed machines: `Dwarpal-Setup-1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES`.

## Build the portable ZIP

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-portable.ps1 -Version 1.0.0
```

This publishes the same app to `artifacts\portable\Dwarpal-1.0.0-portable` and packages
`artifacts\Dwarpal-1.0.0-portable.zip` with a `README-portable.txt`. Add `-SelfContained` to bundle
the .NET runtime (larger ZIP, no Desktop Runtime prerequisite).

The portable build does **not** make the vault portable: data still lives in
`%ProgramData%\Dwarpal`. The bundled readme explains how to remove it and how to clean up the
scheduled tasks after moving the folder.

Versioned release copies are committed in [`dist/`](dist/) with `SHA256SUMS.txt`, so the repository
is usable without downloading a release. Build output stays in `installer/Output` and `artifacts/`
and is git-ignored.

## Continuous integration

[`.github/workflows/release.yml`](.github/workflows/release.yml) runs on Windows for every `v*` tag
(or manually via **workflow_dispatch** with a version input): restore → build → the 86 tests →
install Inno Setup via Chocolatey → build the installer → build the portable ZIP → generate
`SHA256SUMS.txt` → upload both artifacts → publish the GitHub release using
`docs/release-notes-<version>.md`. CI is green on `main`, and this workflow produced the published
[v1.0.0 release](https://github.com/avrojit-ovi/Dwarpal/releases/tag/v1.0.0). Add the release-notes
file for a version before tagging it.

## First run

1. Setup wizard opens (no credential exists yet).
2. Choose **PIN** (4–8 digits) or **Password** (8–64 chars, at least one letter and one digit).
3. Confirm the secret. It is hashed with Argon2id; the plaintext is never written to disk.
4. Copy or print the generated **recovery key**. It is shown exactly once and stored only as a hash.
5. Open the dashboard. Next launch asks for the credential before opening the app.

Forgot the credential? Use **Forgot PIN or password?** on the unlock screen, enter the recovery key,
and set a new credential. A fresh recovery key is issued and the old one stops working.

## Where data lives

| Path | Contents |
| --- | --- |
| `%ProgramData%\Dwarpal\dwarpal.db` | SQLite database (settings, credential, activity log) |
| `%ProgramData%\Dwarpal\logs\dwarpal-YYYYMMDD.log` | Serilog rolling file logs (14 days) |
| `%ProgramData%\Dwarpal\selfies\` | Intruder snapshots, DPAPI-encrypted (`.bin`, not viewable outside Dwarpal) |

The folder ACL is restricted to **SYSTEM** and **Administrators**; standard users cannot read the
database or delete the logs. `ProgramData` is used instead of `%LocalAppData%` because the service
runs as `LocalSystem` and must share the same database.

## Security design (Phase 1)

- **Argon2id** (64 MiB, t=3, p=4) with 16-byte random salt and 32-byte output. Encoded as
  `$argon2id$v=19$m=65536,t=3,p=4$<salt>$<hash>` so cost parameters can be raised later without
  invalidating existing credentials.
- **DPAPI (LocalMachine scope)** encrypts credential and recovery hashes before they are stored.
  Stealing the SQLite file alone does not permit offline brute force on another machine.
- **Recovery key**: 160-bit random, Crockford base32 (`XXXX-XXXX-…`, eight groups), hashed like a
  password. Only the hash is stored; the key itself is shown once during setup.
- **Constant-time comparison** for hash verification; no secret is logged or stored in activity
  entries.
- **Activity log** records setup, unlock success/failure and recovery attempts in SQLite and Serilog.
- **Policy enforcement** on setup, change and recovery (PIN digits only, password letter + digit).

## What Phase 7 delivers

- **Inno Setup installer** (`installer/Dwarpal.iss`) with a stable `AppId`, per-machine elevated
  install, Start-menu/desktop shortcuts, uninstall entry, files-in-use handling, a **.NET 8 Desktop
  Runtime prerequisite check** with a download prompt, and automatic removal of the `Dwarpal` /
  `DwarpalWatchdog` scheduled tasks.
- **One-command release build** (`scripts/build-installer.ps1`): publish `win-x64` without PDBs,
  version-stamp the binary, compile the setup and fail loudly if any output is missing.
- **Portable ZIP** (`scripts/build-portable.ps1`): the same publish packaged with a
  `README-portable.txt` (data location, removal steps, scheduled-task cleanup); `-SelfContained`
  optionally bundles the runtime.
- **CI release workflow** (`.github/workflows/release.yml`): tag-triggered Windows build that runs
  the tests, installs Inno Setup, builds both artifacts and attaches them to a GitHub release.
- **Release artifacts verified locally:** `installer/Output/Dwarpal-Setup-1.0.0.exe` (7.3 MiB,
  framework-dependent) and `artifacts/Dwarpal-1.0.0-portable.zip` (8.4 MiB, 19 files) were built
  successfully; the ZIP contains `Dwarpal.exe`, its runtime config and the portable readme.
- **Final UI consistency pass:** every sidebar page is live (no disabled placeholders left), the
  unused disabled-nav style is gone, and all theme resources used by the new pages were checked
  against `Themes/Colors.xaml` / `Themes/Controls.xaml`.
- **Final documentation:** installer/portable prerequisites, silent-install flags, uninstall
  data-retention behaviour, CI usage, release checklist, an updated limitations list and
  [`docs/release-notes-1.0.0.md`](docs/release-notes-1.0.0.md).
- **86 unit tests** still pass; `dotnet build` is clean with 0 warnings.

## What Phase 6 delivers

- **Profiles.** Named sets of protected apps (`Work`, `Gaming`, …) stored in schema v4 tables.
  Exactly one profile is active at a time, or **All protected apps** when none is selected. While a
  profile is active only its apps lock; everything else opens normally. Apps that are already
  running are locked as soon as a profile that contains them is activated.
- **Protection windows (schedule).** Any number of windows per weekday/Monday–Sunday with a start
  and end time. Overnight windows (`22:00–02:00`) wrap past midnight and the end belongs to the next
  day. Outside every enabled window the engine stops locking new launches and stops auto re-locks;
  already-visible lock screens stay up until a valid credential is entered. With no enabled window
  protection is on 24/7 (the default).
- **Window boundary watcher.** Every 20 seconds the engine compares the schedule state; when a
  window opens it locks already-running protected apps and writes an activity entry, and when a
  window closes it records that new launches stay unlocked.
- **Panic hotkey `Ctrl + Alt + L`.** A message-only window registers the chord with `RegisterHotKey`,
  so it works while Dwarpal is hidden in the tray. It locks every running protected app immediately,
  ignoring the schedule. A chord another app already owns is reported in Settings instead of
  crashing; the rest of the app keeps working.
- **New pages** for Profiles and Schedule in the sidebar, following the existing navy/gold design
  system, with the current schedule state shown live (`Protection is active now` / `paused now` and
  the time until the next window).
- **10 new tests** (86 total) covering profile CRUD, unique names, cascade cleanup, active-profile
  scoping, schedule validation, daytime boundaries, overnight wrap and the next-window helper.

## What Phase 5 delivers

- **Escalating lockout.** Five wrong credentials trigger a cooldown (30 s, doubling on every repeat
  up to 15 minutes). The counter and cooldown timestamp live in SQLite, so restarting the app does
  not reset them, and a correct secret is rejected while the cooldown runs.
- **Live countdown on every credential surface** — unlock window, full-screen lock screen and the
  settings credential prompt disable input and show `Locked out · try again in Ns` until it clears.
- **Intruder selfie.** On the third failed attempt and on every lockout, Dwarpal takes one webcam
  JPEG, encrypts it with DPAPI (`LocalMachine`) and stores it under `selfies\`. A missing camera,
  denied privacy permission or capture error is logged and skipped — unlock never blocks on it.
- **Activity log page.** Searchable, newest-first table of setup, unlock, lock, recovery, settings,
  tamper and error events with success/failure markers, app names and selfie buttons that open the
  decrypted snapshot in a viewer. Export the current view to CSV (UTF-8 with BOM, formula-injection
  guarded) from the page header.
- **Settings → Failed attempts** toggle for intruder selfie capture, persisted immediately and
  recorded in the audit log.
- **8 new tests** (76 total) covering selfie encryption round-trip, path-escape rejection, capture on
  the third attempt/lockout, activity rows carrying the selfie path, and capture failure not
  breaking the unlock flow.

## What Phase 4 delivers

- **Tray/background running.** Closing the dashboard hides it; the protection engine and every lock
  screen keep running. Reopen from the tray icon, double-click, or by launching `Dwarpal.exe` again.
- **Single-instance guard.** A mutex plus activation event means a second launch reopens the hidden
  window instead of starting a second protection engine.
- **Watchdog.** A Task Scheduler task (`DwarpalWatchdog`) runs `Dwarpal.exe --watchdog` every minute
  with highest privileges. If the main instance's mutex is gone, it relaunches the app; otherwise it
  exits immediately. It is recreated on startup when enabled and removed on a clean exit.
- **Process DACL hardening.** On startup Dwarpal adds a `BUILTIN\Users` deny ACE for
  `PROCESS_TERMINATE`, so an unelevated Task Manager / `taskkill` cannot end the guard process.
  Set `DWARPAL_DISABLE_DACL=1` before launch to skip it during development.
- **Credential-gated exit.** Tray → Exit asks for the PIN/password; a failure leaves Dwarpal running.
  Graceful exits remove the watchdog task, kills leave it in place so the app returns.
- **Settings → Tamper protection** shows DACL status and the watchdog toggle, and the activity log
  records `Tamper` events for both.

> The watchdog deliberately uses Task Scheduler rather than the Windows service: a session-0 service
> cannot show the interactive lock screen. `Dwarpal.Service` remains the privileged heartbeat host.

## What Phase 3 delivers

- **Auto re-lock** after a configurable timeout once an unlocked protected app goes to the
  background (minimize, focus loss) **or the PC sits idle** (system-wide keyboard/mouse idle time).
- **Grace period** after unlock so the first moments in the app never trigger an immediate re-lock.
- **Settings page** with steppers for the re-lock timeout (`Never`, 1, 2, 5, 10, 15, 30, 60 min) and
  grace (0–60 s). Changes save immediately and reload the live engine without a restart.
- **Sign-in auto-start** through a Windows Task Scheduler entry with highest privileges, plus an
  accurate on/off state read back from the scheduler.
- **Per-app timeout override** in the schema/service layer (`AutoRelockMinutes`), ready for the
  profile editor in Phase 6.
- **Brand identity**: the supplied shield/trishul/torana artwork is embedded as the EXE and window
  icon, and used in the sidebar, setup and unlock screens.
- Activity log entries for every auto re-lock and settings change.

## What Phase 2 delivers

- **Protected app selection** with a searchable picker built from Registry Uninstall keys and Start
  Menu shortcuts (icons included), plus manual `.exe` browse.
- **WMI `Win32_ProcessStartTrace` watcher** (needs admin, which the manifest already requires).
- **NtSuspendProcess / NtResumeProcess** process control; apps are frozen while locked.
- **Full-screen topmost lock screen** on the virtual screen with blurred screen snapshot, app icon,
  name, PIN pad / password entry, Escape and Alt+F4 blocked.
- **Adding an app that is already running locks it immediately** (same process name or path).
- **Per-app sessions**: multiple protected apps can be locked at once; each resumes only after a
  valid credential. A process that exits while locked closes its lock screen automatically.
- **Activity events** for lock shown, suspend failure, unlock/resume and process exit.
- **Schema v2 migration** (`ProtectedApps`) upgrades existing Phase 1 databases in place.

## What Phase 1 delivers

- 3-project solution with shared Core, WPF app and Windows service host.
- SQLite schema with a `user_version`-based migration path.
- Setup wizard (credential → recovery key → done) with copy and print.
- Unlock window with a numeric PIN pad (mouse + keyboard) and a password mode.
- Recovery flow with recovery-key rotation.
- Dashboard shell showing credential type, recovery status, today's failed unlocks and recent
  activity read from SQLite.
- Dark navy/gold theme, rounded window chrome, fade-in transitions.
- 86 unit tests covering hashing, policy, recovery keys, credential lifecycle, database queries,
  protected-app storage, app matching, auto re-lock decisions, lockout escalation, selfie storage,
  profiles, schedule windows and the v1 → v4 migrations.
- Service host that initializes the database and emits a heartbeat (watchdog arrives in Phase 4).

## Roadmap

| Phase | Scope |
| --- | --- |
| 1 ✅ | Solution, Core, SQLite, setup wizard, credential, recovery, dashboard shell |
| 2 ✅ | Process detection, suspend/resume, full-screen lock screen, app selection |
| 3 ✅ | Auto re-lock (background/inactivity), grace period, settings page, auto-start |
| 4 ✅ | Tamper protection: tray/background, single instance, Task Scheduler watchdog, DACL terminate block |
| 5 ✅ | Lockout, intruder selfie, activity log UI + CSV export |
| 6 ✅ | Profiles, schedules, panic hotkey |
| 7 ✅ | Theme polish, Inno Setup installer, final README, GitHub release prep |

## Phase 7 manual check

1. Build the setup with `scripts\build-installer.ps1` and the ZIP with
   `scripts\build-portable.ps1`; confirm both artifacts appear.
2. On a VM **without** the .NET 8 Desktop Runtime, run the setup: it should offer the Microsoft
   download page and abort cleanly; `/VERYSILENT` should fail without prompting.
3. Run it on a clean Windows 10/11 x64 VM **with the .NET 8 Desktop Runtime installed**: it should
   install to `Program Files\Dwarpal`, create shortcuts, and launch the setup wizard.
4. Enable auto-start and the watchdog in Settings, then uninstall from **Apps & features**; check
   Task Scheduler no longer lists `Dwarpal` or `DwarpalWatchdog`, and that
   `%ProgramData%\Dwarpal` still exists.
5. Reinstall: the existing credential/vault should still unlock (data retained).
6. Reboot and confirm no leftover app files prevent a fresh install; test `/VERYSILENT` on a
   scratch VM if you deploy it.

## Phase 6 manual check

1. **Protected Apps** → add two safe apps (Notepad and Calculator). Open **Profiles**, create
   `Work`, choose apps, activate it. Only the chosen app should lock from now on; the other opens
   normally. Switch to **Use all apps** and confirm both lock again.
2. Open **Schedule** and add a window that covers the current time (`00:00–23:30`, every day).
   Launch a protected app: it locks. Now add an end time a minute away, wait for it to pass and
   launch the app again: it should open normally, and the state card should read *Protection is
   paused now*.
3. Create an overnight window (`22:00–02:00`, one day) and verify the state card treats the next
   morning (before 02:00) as inside the window.
4. While a protected app is running, press **Ctrl + Alt + L**: its screen should lock immediately
   even if the schedule says paused. Settings → **Panic hotkey** should show *Active*.
5. Restart Dwarpal; the selected profile and windows persist (schema v4 migration).

## Phase 5 manual check

1. Open **Activity Log** from the sidebar: setup and unlock events appear newest-first; search for an
   app name and confirm the list filters; export to CSV and open it in a spreadsheet.
2. Lock a protected app and enter a wrong PIN three times: a **Selfie** button should appear on the
   new failure entry. Open it — the snapshot shows the person at the webcam.
3. Fail two more times: the lock screen disables input and counts down from ~30 s; the correct PIN is
   rejected until the countdown ends. The activity log also shows the lockout entry with a selfie.
4. Restart Dwarpal during a lockout and confirm the countdown is still running.
5. Settings → **Failed attempts** → turn selfie capture off, fail three times again and confirm no
   new selfie is stored.

## Phase 4 manual check

1. Open the dashboard and click the window close button: it should disappear to the tray, and the
   tray balloon should say protection is still running. Reopen with the tray icon.
2. Launch `Dwarpal.exe` again: the existing hidden window should come forward (no second process).
3. Settings → **Tamper protection** → enable **Restart Dwarpal if it is closed**; sign out or wait a
   minute, then check Task Scheduler for `DwarpalWatchdog`.
4. End the Dwarpal process from an unelevated Task Manager: it should fail (DACL). Use the tray Exit
   with the correct credential instead — the watchdog task must be gone afterwards.
5. Kill the process with an elevated tool (or `taskkill /F` as admin): the watchdog should relaunch
   Dwarpal within a minute, showing the unlock screen. This is the honest limit of a user-mode DACL.

## Phase 3 manual check

1. Open **Settings** and set auto re-lock to **1 minute**, grace to **5 seconds**.
2. Unlock a protected app, then switch to another window or leave the PC idle for a minute.
3. The lock screen should return on its own; the app stays suspended until the credential is entered.
4. Set auto re-lock to **Never** and confirm the app no longer re-locks.
5. Toggle **Start Dwarpal when I sign in**, sign out/in, and confirm the app starts elevated.

## Phase 2 manual check

1. Open **Protected Apps → + Add app**, pick something safe like Notepad or Calculator, and add it.
2. Launch that app: within a moment Dwarpal suspends it and covers the screen with the lock screen.
3. Enter the wrong PIN (app stays suspended), then the right PIN (app resumes and keeps its state).
4. Add a protected app while it is already running: it should lock immediately.
5. Disable or remove it from the list, then relaunch and confirm it opens normally.

## Known limitations (honest list)

- **Safe Mode / offline boot** can bypass any user-mode locker. A kernel minifilter driver would be
  required to close that gap; Dwarpal does not ship one.
- A local administrator can stop the service, replace binaries or reset the machine. Phase 4 raises
  the bar (DACL deny-terminate, dual watchdog) but cannot defeat an administrator with physical
  access.
- DPAPI `LocalMachine` means any administrator on that machine can decrypt the stored hashes.
- A 4-digit PIN has a small keyspace. Argon2id and DPAPI make offline attacks expensive but not
  impossible if the machine is compromised.
- The lock screen is a single topmost window spanning the virtual screen. On mixed-DPI
  multi-monitor setups the screenshot may be scaled slightly; per-monitor overlays are planned.
- Windows-level shortcuts (Win, Alt+Tab) are not blocked. The protected app itself stays suspended,
  so switching away does not reveal it — but other windows remain visible.
- Closing Dwarpal while an app is locked resumes that app (suspended processes are cleaned up on
  exit). Phase 4's watchdog and DACL hardening close this bypass.
- **Smart App Control** (or WDAC/AppLocker policy) can block unsigned builds from running at all.
  Code signing is required for distribution on such machines; see *Code signing* below.
- The DACL blocks unelevated `PROCESS_TERMINATE`. A local administrator with `SeDebugPrivilege` or
  `WRITE_DAC` can remove it; user-mode code cannot defeat an administrator with physical access.
- The watchdog checks once a minute, so a killed app can be absent for up to a minute. Suspended
  protected apps resume when the process dies; they re-lock only when relaunched.
- Intruder selfies need a webcam and camera access for desktop apps (Settings → Privacy → Camera).
  Photo capture also fails while another app holds the camera exclusively; the failure is skipped
  and logged, never shown as an unlock error.
- Lockout timing is client-side. Clearing `%ProgramData%\Dwarpal\dwarpal.db` resets the cooldown,
  which only an administrator can do.
- Schedule boundaries are polled every 20 seconds, so a window can open up to ~20 s late. The poll
  only runs while Dwarpal is alive; a missed boundary is re-evaluated on the next tick.
- Closing a protection window does not force-unlock an app whose lock screen is already showing. It
  stays suspended until the credential is entered — a deliberate safety choice.
- Profiles select apps only; the global auto re-lock timeout still applies to all of them (a
  per-profile timeout is not implemented yet).
- The panic hotkey is fixed at `Ctrl + Alt + L`. If another app owns that chord the registration
  fails and Settings shows why; there is no hotkey remapping UI yet.
- Both auto-start and watchdog tasks point at the current EXE path. The installer's uninstaller
  removes them, but after moving a portable build you must re-enable both in Settings.
- The installer is framework-dependent: **.NET 8 Desktop Runtime must be installed first**. Setup
  detects it and links to the Microsoft download page, but does not download or bundle the runtime.
  Use `scripts/build-portable.ps1 -SelfContained` for a runtime-bundled portable ZIP; the installer
  script has no self-contained switch yet.
- Setup and binaries are **unsigned**, so SmartScreen/Defender may warn and Smart App Control can
  block the app outright (see *Code signing*).
- The installer keeps `%ProgramData%\Dwarpal` on uninstall by design, and the portable ZIP stores
  its data there too. Wiping the vault is a manual step (exit from the tray, then delete that
  folder); there is no "remove all data" checkbox or portable-data mode yet.
- `Dwarpal.Service` is a heartbeat host and is not installed by the setup. Interactive protection
  runs in the elevated app plus the Task Scheduler watchdog, because session-0 services cannot show
  the lock screen.
- The published v1.0.0 binaries are unsigned and have only been smoke-tested on the build machine;
  CI builds and tests every push but does not install the package on a clean VM. See *Code signing*
  and the checklist below.

## Release checklist

1. Bump `<Version>` in `src/Dwarpal.App/Dwarpal.App.csproj` (and pass the same `-Version` to the
   script) so the EXE, installer name and Apps & features entry agree.
2. `dotnet test tests/Dwarpal.Core.Tests/Dwarpal.Core.Tests.csproj` — all 86 tests green.
3. `powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version <version>` and
   record the SHA-256 of `installer\Output\Dwarpal-Setup-<version>.exe`.
4. `powershell -ExecutionPolicy Bypass -File scripts\build-portable.ps1 -Version <version>` and
   record the SHA-256 of `artifacts\Dwarpal-<version>-portable.zip`.
5. Copy both artifacts into `dist/`, refresh `dist/SHA256SUMS.txt`
   (`cd dist && sha256sum *.exe *.zip > SHA256SUMS.txt`) and update `dist/README.md` for the version.
6. Smoke-test install, first-run setup, unlock, profile switch, schedule window, panic hotkey and
   uninstall on a clean x64 VM with the .NET 8 Desktop Runtime. Repeat from the portable ZIP.
7. Sign `Dwarpal.exe` and the setup (see *Code signing*), then re-hash.
8. Add `docs/release-notes-<version>.md`, commit, tag `v<version>` and push to `origin`. The tag
   runs `.github/workflows/release.yml`, which tests the solution, rebuilds both artifacts,
   generates `SHA256SUMS.txt` and publishes the GitHub release from the versioned notes. When code
   signing is added, sign inside that workflow before the artifacts are uploaded.

## Branding assets

| File | Purpose |
| --- | --- |
| `dwarpal-icon.svg` / `dwarpal-logo.svg` | Source artwork (shield mark and stacked wordmark) |
| `dwarpal-mark-512.png` | Transparent 512×512 mark used in the UI |
| `dwarpal-logo-512.png` | 512×720 stacked logo used in docs |
| `dwarpal.ico` | 16/20/24/32/40/48/64/128/256 px Windows icon (EXE + windows) |

## Code signing

Windows Smart App Control only allows apps with good reputation or a trusted signature. Builds from
this repository are unsigned, so a machine with Smart App Control enforced will block `Dwarpal.exe`
with an *Application Control policy* error. Options for distribution: sign with a trusted
code-signing certificate (and build reputation), or use a machine where Smart App Control is off.
Do not disable Smart App Control casually: Windows cannot re-enable it without a reset.

## Project

- [Contributing](CONTRIBUTING.md) — build, style and pull-request expectations.
- [Security policy](SECURITY.md) — private vulnerability reporting and scope.
- [Architecture](docs/ARCHITECTURE.md) — components, lifecycle, data model and threat boundaries.
- [Changelog](CHANGELOG.md) — release history.

## License

Proprietary. Copyright © 2026 Avrojit Chowdhury. All rights reserved; see [LICENSE](LICENSE) for the
full terms. For licensing enquiries, contact the repository owner.
