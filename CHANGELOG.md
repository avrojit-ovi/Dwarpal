# Changelog

All notable changes to Dwarpal are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[semantic versioning](https://semver.org/).

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

[1.0.0]: https://github.com/avrojit-ovi/Dwarpal/releases/tag/v1.0.0
