# Security policy

Dwarpal is a local security tool: it stores credential hashes, gates process launches and keeps an
audit trail. Please do not open a public issue for a vulnerability.

## Supported versions

| Version | Supported |
| --- | --- |
| 1.0.x | ✅ |
| < 1.0 | ❌ |

Only the latest release on [GitHub Releases](https://github.com/avrojit-ovi/Dwarpal/releases) is
supported. Builds from `main` are development snapshots.

## Reporting a vulnerability

Use **Security → Report a vulnerability** in this repository
(<https://github.com/avrojit-ovi/Dwarpal/security/advisories/new>). The report stays private until a
fix is ready.

Please include:

- Dwarpal version and installer or portable build, Windows version and architecture.
- Steps to reproduce, expected vs actual behaviour, and whether elevation was involved.
- Relevant log excerpts from `%ProgramData%\Dwarpal\logs` with secrets and paths redacted.
- Any proof-of-concept code, kept minimal and defensive.

Do not include real PINs, passwords, recovery keys, selfies, or database files.

## In scope

- Credential bypasses: unlocking a protected app without the credential, or resuming a suspended
  process through a route the UI does not offer.
- Lockout, schedule, profile or panic-hotkey logic that can be tricked into skipping protection.
- Weaknesses in Argon2id usage, DPAPI storage, recovery-key rotation, CSV export or the installer
  (for example elevation or path handling mistakes).
- Denial of service that permanently prevents Dwarpal from starting after a normal configuration
  change.
- Leaks of secrets into logs, activity entries or crash output.

## Out of scope

These are documented design limits, not vulnerabilities:

- A local administrator, Safe Mode, a kernel debugger or physical access can bypass any user-mode
  locker. See the limitations list in the README.
- `DPAPI LocalMachine` means an administrator on that machine can decrypt stored hashes.
- SmartScreen / Smart App Control warnings for unsigned builds.
- Windows-level shortcuts (Win, Alt+Tab) are not blocked; the protected app itself stays suspended.
- A webcam that is missing or blocked by Windows privacy settings merely skips the intruder selfie.

## Response

Reports are acknowledged on a best-effort basis. A confirmed issue gets a fix and a coordinated
release; credit is given in the release notes unless you ask to stay anonymous. There is no bug
bounty.
