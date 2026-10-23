# Contributing to Dwarpal

Thanks for taking the time to improve Dwarpal. This project is a Windows app locker, so changes that
touch credential handling, process suspension or the lock screen need extra care.

## Prerequisites

- Windows 10 21H2 / Windows 11 (x64)
- .NET 8 SDK
- Inno Setup 6 (only for installer work)
- Administrator rights to run the app; `app.manifest` requires elevation

## Build and test

```powershell
dotnet restore Dwarpal.sln
dotnet build Dwarpal.sln
dotnet test tests/Dwarpal.Core.Tests/Dwarpal.Core.Tests.csproj
```

Build the release assets when needed:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-installer.ps1 -Version 1.0.0
powershell -ExecutionPolicy Bypass -File scripts/build-portable.ps1 -Version 1.0.0
```

## What to change

- **Core logic goes in `Dwarpal.Core`** and needs unit tests. Behaviour such as hashing, lockout
  escalation, schedule boundaries, profile scoping and matching should stay UI-free and testable.
- **UI follows MVVM.** Views bind to view models; code-behind is limited to window chrome, dialogs
  and platform wiring. Match the existing navy/gold styles in `Themes/`.
- **No secrets in the repository, logs or tests.** Test credentials use fake values and temporary
  databases (`TestDatabase`).
- **Keep the dependency surface small.** The app intentionally ships with SQLite, Argon2id and a
  handful of UI packages; adding a dependency needs a clear reason.

## Code style

- `.editorconfig` defines indentation, encoding and line endings; run `dotnet format` on files you
  touch if your IDE does not apply it.
- File-scoped namespaces, nullable enabled, `_camelCase` private fields, `PascalCase` types/members.
- Comments explain *why*, not *what*; avoid commented-out code.
- Keep user-facing copy consistent with the existing tone (short English labels with occasional
  Bengali guidance in the UI).

## Pull requests

1. Open an issue first for larger features or behaviour changes.
2. Keep the change focused; separate refactors from fixes.
3. Add or update tests in `tests/Dwarpal.Core.Tests`.
4. Update the README, CHANGELOG and relevant docs.
5. Run the automated checks and the manual checks that cover your change:

```powershell
dotnet build Dwarpal.sln
dotnet test tests/Dwarpal.Core.Tests/Dwarpal.Core.Tests.csproj
```

For lock-screen, tray, camera, installer or hotkey changes, also exercise the relevant phase checklist
in the README on a real desktop (these cannot run headless in CI).

A good PR description covers the problem, the approach, the security impact and how you verified it.
Commit messages use the imperative mood, for example `Fix lockout countdown after resume`.

## Security issues

Do not open a public pull request or issue for a vulnerability. Follow [SECURITY.md](SECURITY.md).
