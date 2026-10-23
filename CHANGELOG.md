# Changelog

All notable changes to Dwarpal are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[semantic versioning](https://semver.org/).

## [1.0.1] — 2026-10-23

Bug-fix release focused on the lock/unlock and recovery flows.

### Fixed

- **PIN unlock now closes the lock window immediately** and resumes every suspended process in the
  app's session. The submit button, physical Enter key and Enter from any focused control all work.
- **Multi-process apps share one lock screen.** Chrome/Discord-style child processes join the
  existing session instead of opening a new window, and the whole session resumes on unlock.
- **Lock screen is a compact window again** (520 × 720, normal Z-order, taskbar button) instead of a
  topmost full-screen overlay, so the taskbar and other apps remain usable while an app is locked.
- **Recovery reset clears the lockout countdown.** A recovery-key reset now immediately re-enables
  the credential input instead of leaving the old cooldown timer blocking the new PIN/password.
- **Protection stays active at the startup gate.** The engine starts before the startup unlock/setup
  window, and the gate can no longer be closed to bypass protection; Windows shutdown still closes
  it cleanly.
- **Auto-start task path is quoted**, so `schtasks /TR` works when Dwarpal is installed under
  `Program Files`.
- **Settings toggles revert their visual state** when Task Scheduler or selfie settings fail.
- **Dashboard active profile** is loaded from the database instead of showing a hard-coded value;
  protected-app count now reflects enabled apps.
- **Editing an inactive profile no longer locks running apps** from the active profile.
- **Optional screen capture and icon extraction can no longer leave a target process suspended** if
  GDI/icon handling fails while the lock window is being created.
- **Profile checkbox selection count, picker search selection, schedule/app toggle failures and
  unlock-screen recovery messages** now reflect their real state.

### Build

- Installer bundles the .NET 8 Desktop Runtime (x64) instead of sending the user to a download page.
- 87 tests (one new recovery/lockout regression test).

## [1.0.0] — 2026-10-23

First complete release.

### Added

- Protected-app selection from installed programs, Start-menu shortcuts or a manual `.exe` browse,
  with WMI process-start detection, `NtSuspendProcess`/`NtResumeProcess` and a full-screen lock
  screen per app.
- PIN (4–8 digits) or password (8–64 characters with a letter and a digit) credentials, Argon2id
  hashing, DPAPI-protected storage, recovery-key rotation and a setup wizard.
- Auto re-lock after background/minimise or system idle, with a configurable timeout (`Never` to
  60 minutes) and grace period, editable live from Settings.
- Escalating lockout (five failures → 30 s, doubling to 15 min), a live countdown on every
  credential surface and persistent state across restarts.
- Intruder selfies on the third failure and each lockout, DPAPI-encrypted and viewable from the
  activity log; the Settings toggle can disable capture.
- Searchable activity log with success/failure markers, selfie viewer and CSV export guarded against
  formula injection.
- Profiles: named app sets with one active at a time and an app picker.
- Protection windows per weekday with overnight support, a 20-second boundary watcher and live
  active/paused state.
- Panic hotkey `Ctrl + Alt + L` that locks every running protected app, ignoring the schedule.
- Sign-in auto-start, tray/background running, single-instance guard, credential-gated exit and a
  Task Scheduler watchdog.
- Process DACL hardening (deny `PROCESS_TERMINATE` to `BUILTIN\Users`).
- Inno Setup 6 installer with a .NET 8 Desktop Runtime prerequisite check, Start-menu/desktop
  shortcuts, scheduled-task cleanup on uninstall and retained vault data.
- Portable ZIP build, with an optional self-contained runtime bundle.
- GitHub Actions CI for pushes and pull requests, and a tag-triggered packaging workflow that builds
  and attaches the installer and portable ZIP to a release.
- 86 unit tests covering hashing, policy, recovery, database migrations, protected apps, auto
  re-lock, lockout, selfie storage, profiles and schedules.

### Security

- Argon2id at 64 MiB / t=3 / p=4, DPAPI `LocalMachine` for stored hashes and selfies, constant-time
  comparisons and restrictive ACLs on `%ProgramData%\Dwarpal`.
- No plaintext PIN, password or recovery key is written to disk or logs.

### Documentation

- README with architecture, build, installer, portable, manual checks, limitations and a release
  checklist; security policy, contribution guide, architecture notes and release notes.

[1.0.1]: https://github.com/avrojit-ovi/Dwarpal/releases/tag/v1.0.1
[1.0.0]: https://github.com/avrojit-ovi/Dwarpal/releases/tag/v1.0.0
