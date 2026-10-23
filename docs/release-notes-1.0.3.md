# Dwarpal v1.0.3

Dwarpal 1.0.3 is a bug-fix round driven by live testing of the running app. Every fix was
reproduced first and re-verified after the change.

## Fixes

### System apps like File Explorer can now be locked

Always-running apps never fire process-start events — opening a File Explorer window reuses the
already-running shell process — so Dwarpal's process watch never saw a "launch" and never locked
them. The engine now also locks a protected app when it **comes to the foreground** with no lock
or in-use session, so system apps and other already-running programs are protected the same way
as freshly launched ones. After an unlock the app stays usable until its auto re-lock timeout, as
before.

### Tray Exit always shows the credential prompt

The exit confirmation window previously opened with no owner, no topmost flag and an
owner-relative start position. Behind another window (or a lock screen) it was invisible, so
"Exit Dwarpal…" looked like it closed or ignored the password. The prompt is now topmost, centered
on the current screen and activated on open — it cannot be missed, and cancelling it still leaves
Dwarpal running.

### Lock window stays above the guarded app

The lock window (520 × 720) is now topmost. This deliberately changes the 1.0.1 "normal Z-order"
decision: when other windows covered the lock window, a locked app looked usable and unprotected.
The window stays compact, so the taskbar and other applications remain usable while an app is
locked.

### Smaller fixes

- Engine disposal no longer resumes locked processes twice (and no longer writes a bogus
  "Resumed" activity entry during shutdown).
- Adding a protected app no longer reports "add failed" when the app was saved and only the
  post-add lock refresh failed; the refresh error is logged instead.

## Install

- **Installer:** `Dwarpal-Setup-1.0.3.exe` — per-machine, elevated. The bundled .NET 8 Desktop
  Runtime (x64) is installed automatically when missing.
- **Portable:** `Dwarpal-1.0.3-portable.zip` — extract anywhere and run `Dwarpal.exe`. Data still
  lives in `%ProgramData%\Dwarpal`.

Verify downloads with `SHA256SUMS.txt` attached to the release.

## Verification

- `dotnet build Dwarpal.sln --configuration Release` passes with 0 warnings and 0 errors.
- 87 Core unit tests pass.
- A live behaviour probe (real engine, real lock screen, real credential prompt, temp data dir)
  confirmed each fix before and after: add-time lock, PIN unlock/resume, foreground-triggered
  locking of an already-running app, and prompt visibility. It is kept out of the shipped product.
- The release workflow builds and attaches both artifacts with checksums.

## Known limitations

- The binaries are not code-signed. Windows SmartScreen may warn on first run.
- Foreground-triggered locking reacts to foreground *changes*: if an app is already in the
  foreground when Dwarpal starts, it locks on the next switch back to it.
- Windows shortcuts (Win, Alt+Tab) are not blocked, and Safe Mode bypasses a user-mode locker.
