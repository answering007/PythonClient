# Installation

## Prerequisites

| Component | Requirement |
|---|---|
| Windows | 64-bit (the provider is x64-only) |
| .NET | **.NET Framework 4.8** — included in Windows 10 1903+ and Windows 11 |
| Excel | 64-bit, only for the Power Query / Excel scenario (32-bit Excel cannot load the provider) |
| Python | **3.9 – 3.13 x64** with `numpy` and `pandas` (`polars` is optional); the provider does **not** install Python — [python-environment.md](python-environment.md) builds such an environment with uv |
| Rights | administrator: the assemblies are published into the GAC and the provider is registered in `machine.config` |

## Install from a GitHub release (recommended)

1. Open <https://github.com/answering007/PythonClient/releases/latest> and download
   `Pike.PythonClient64-<version>.zip` (plus `.zip.sha256` if you want to verify the download).
2. Extract the archive to a local folder. Do not run the files from inside the ZIP.
3. Run `install.cmd` and confirm the UAC prompt:

```text
Pike.PythonClient64-2.2.0\
  install.cmd  verify.cmd  uninstall.cmd
  lib\        Pike.PythonClient64.dll/.xml/.pdb, Python.Runtime.dll,
              Pike.PythonClient64.Install.dll/.xml
  scripts\    install.ps1, uninstall.ps1, Verify-Installation.ps1, Installation.Common.ps1
  samples\power-query\   ready-made workbooks
  samples\Python\        scripts run by the workbooks (TestScript01..04.py, TestModule.py)
```

`install.cmd` unblocks the extracted files (Mark-of-the-Web), requests elevation, reports the prerequisites and calls
`scripts\install.ps1 -SourcePath lib`. The script publishes the two runtime assemblies into the GAC —
`Pike.PythonClient64` (`GAC_64`) and `Python.Runtime` (pythonnet, `GAC_MSIL`) — and writes the fully qualified provider
record into the x64 `machine.config` (a backup `machine.config.bak-<utc>` is created). The registration code
(`Pike.PythonClient64.Install.dll`) is **not** published: it is loaded from `lib\` of this archive, so keep the archive
until the provider is uninstalled. **No files are copied to `%ProgramFiles%`**: the GAC is the only installation
location. The result is verified in a new process: the registered type must be loaded from `...\assembly\GAC_*`.

4. Restart Excel: Power Query keeps the provider inside the Excel process.
5. Check the installation at any time with `verify.cmd` (read-only, no administrator rights).

## Silent / unattended installation

```powershell
install.cmd -NoPause
# or directly, from the extracted archive:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install.ps1 -SourcePath lib
```

Advanced switches of `install.ps1`: `-KeepFiles` (+ `-InstallPath <dir>`, a diagnostics copy of the assemblies),
`-SkipGac`, `-SkipMachineConfig`, `-SkipPrerequisiteCheck`, `-PythonDll <path to python3xx.dll>` (used only by the
prerequisite report). `install.ps1` is idempotent: the same assembly identity is replaced, the `machine.config` record
is updated in place, and an old `%ProgramFiles%\Pike\PythonClient64` folder of the legacy 2.1.0 development builds is
cleaned up.

## Install from source (developers)

```powershell
dotnet build PythonClient.sln -c Release
.\scripts\install.ps1          # copies bin\Release\net48, updates the GAC and machine.config
.\scripts\Verify-Installation.ps1
.\scripts\uninstall.ps1
```

Requirements: .NET SDK, elevated **64-bit Windows PowerShell 5.1**
(`System.EnterpriseServices.Internal.Publish` does not exist in PowerShell 7), `gacutil`/Windows SDK are not needed.
The distribution ZIP for a release is built by `scripts\build-distribution.ps1` (version taken from
`Directory.Build.props`).

## What is installed

| Item | Location |
|---|---|
| `Pike.PythonClient64` (provider) | `GAC_64` |
| `Python.Runtime` (pythonnet dependency) | GAC (`GAC_MSIL`) |
| `Pike.PythonClient64.Install` (registration code used by the scripts) | **not installed** — it is loaded from the distribution `lib\` only |
| provider record, invariant `Pike.PythonClient64.Data.PythonProviderFactory` | x64 `machine.config` → `configuration/system.data/DbProviderFactories` |

Nothing is copied to `%ProgramFiles%`: the GAC is the single source of truth, so there are no duplicated files that can
diverge from the published assemblies, and it contains runtime assemblies only. `-KeepFiles` (with `-InstallPath`)
creates a diagnostics copy of the files when support needs them next to a known path; it is not used by the runtime.

The record alone is not enough: both runtime assemblies must be in the GAC, otherwise the runtime cannot resolve the
type outside the Excel folder. `AssemblyVersion` is fixed for the whole 2.x line, so an upgrade replaces the same GAC
entry and the existing `machine.config` record stays valid.

## Verify

```powershell
.\scripts\Verify-Installation.ps1                       # repository: compared with bin\Release
powershell -File scripts\Verify-Installation.ps1 -ReferencePath lib   # distribution ZIP

# diagnostics copy created by install.ps1 -KeepFiles:
.\scripts\Verify-Installation.ps1 -InstallPath D:\temp\pike -ReferencePath D:\temp\pike\lib
```

13 checks by default: the two GAC entries (and that the installer assembly is **not** published), the provider resolved
by `DbProviderFactories` in a new process, the `machine.config` record and its version, the GAC copy compared with the
reference build (SHA256), the polars reader in the installed build and the absence of the legacy `%ProgramFiles%`
folder. `-InstallPath` adds the diagnostics-copy checks (folder and file hashes). Exit code `1` with the failing check
names; no administrator rights required.

## Uninstall

```powershell
uninstall.cmd                # from the extracted distribution archive (elevated)
.\scripts\uninstall.ps1      # repository (elevated)
```

The registration code (`Pike.PythonClient64.Install.dll`) is **not** published in the GAC, so the uninstall takes it
from the payload: `uninstall.cmd` passes its own `lib\` folder (`-SourcePath`), and the repository layout is detected
automatically. Keep the archive until the provider is uninstalled. `Pike.PythonClient64` and `Python.Runtime` are
removed from the GAC first, then the `machine.config` record (backup) — the registration code depends on the provider
assembly, so it is loaded only after the GAC copies are gone. The result is verified. An
assembly that is still loaded by a running process stays in the GAC until that process exits — the script reports it and
exits with `1`, so close Excel / Visual Studio and run it again. An optional `-InstallPath` folder and the legacy
`%ProgramFiles%\Pike\PythonClient64` folder are cleaned up when present; when nothing is installed the script reports
"Nothing to uninstall".

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `Administrator rights are required` | confirm the UAC prompt; the GAC and `machine.config` cannot be changed otherwise |
| `... cannot be loaded because running scripts is disabled` | use `install.cmd` / `verify.cmd`: they call Windows PowerShell with `-ExecutionPolicy Bypass` and unblock the files |
| `Can't find python*.dll file` | `PYTHONDLL` in the connection string must point to an existing 64-bit `python3xx.dll` |
| `ImportError: No module named pandas` (or numpy) | install the packages into the Python used by the connection string: `python -m pip install numpy pandas`; a uv-based environment is described in [python-environment.md](python-environment.md) |
| Excel still runs the old build | restart Excel, then `verify.cmd`: a stale GAC entry or a Debug build installed by mistake is reported there |
| `DbProviderFactories.GetFactory` fails | the provider is not registered (run `install.cmd`) or 32-bit Excel is used |
| `ConfigurationErrorsException: ... InvariantName ...` | the record exists twice (machine.config and the application `app.config`): keep one, or add `<remove invariant="..." />` before `<add>` |
