# Release artifacts

Prebuilt binaries for **Dwarpal 1.0.1**, byte-for-byte identical to the assets on the matching
[GitHub release](https://github.com/avrojit-ovi/Dwarpal/releases/tag/v1.0.1). They are committed so
the repository is usable without a release download; the release remains the source of truth.

| File | What it is |
| --- | --- |
| `Dwarpal-Setup-1.0.1.exe` | Inno Setup installer (per-machine, elevated). Bundles the .NET 8 Desktop Runtime (x64) and installs it silently when missing. |
| `Dwarpal-1.0.1-portable.zip` | Portable build: extract anywhere and run `Dwarpal.exe`. Requires the .NET 8 Desktop Runtime; data still lives in `%ProgramData%\Dwarpal`. |
| `SHA256SUMS.txt` | SHA-256 checksums for both artifacts. |

## Requirements

- Windows 10 21H2 / Windows 11, x64
- .NET 8 **Desktop** Runtime
- Administrator rights (UAC prompt)
- A webcam only if you want intruder selfies

## Verify a download

```powershell
Get-FileHash .\Dwarpal-Setup-1.0.1.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

Both hashes must match. The binaries are unsigned, so SmartScreen may warn; see
[Code signing](../README.md#code-signing).

## Rebuilding

Artifacts are produced by `scripts/build-installer.ps1` and `scripts/build-portable.ps1`; the
installer lands in `installer/Output`, the ZIP in `artifacts/`. Copy the versioned files here and
refresh `SHA256SUMS.txt` when preparing a release. Do not edit the binaries.
