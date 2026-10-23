Dwarpal — portable build
=========================

This folder is the no-installer build of Dwarpal: run Dwarpal.exe and accept the UAC prompt.
Dwarpal needs the .NET 8 Desktop Runtime (x64) and Windows 10 21H2 / Windows 11.

What to know
------------
- Data is NOT stored next to the EXE. Dwarpal keeps its vault in %ProgramData%\Dwarpal
  (SQLite database, rolling logs and DPAPI-encrypted intruder selfies), shared with an installed
  copy on the same machine. Moving this folder does not move the data.
- Keep the folder at a stable path. Auto-start and the watchdog Task Scheduler entries point at
  the EXE path; re-enable both in Settings after moving or copying the folder.
- Exit from the tray (credential required) before deleting the folder, so suspended protected apps
  are resumed and the scheduled tasks are removed.
- Windows shortcuts (Win, Alt+Tab) are not blocked, and Safe Mode bypasses a user-mode locker.

Removing all data
-----------------
1. Tray -> Exit and enter the credential.
2. Open Settings and turn off "Start Dwarpal when I sign in" and the watchdog.
3. If you enabled them manually, delete the Task Scheduler entries named "Dwarpal" and
   "DwarpalWatchdog".
4. Delete %ProgramData%\Dwarpal. This erases the credential, recovery hash, profiles, schedules,
   activity log and selfies permanently.
5. Delete this folder.

The portable build is unsigned, so SmartScreen may warn. See the repository README for details.
