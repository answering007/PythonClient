# Installation

## Prerequisites

| Component | Requirement |
|---|---|
| Windows | 64-bit (the provider is x64-only) |
| .NET | **.NET Framework 4.8** — included in Windows 10 1903+ and Windows 11 |
| Excel | 64-bit, only for the Power Query / Excel scenario (32-bit Excel cannot load the provider) |
| Python | **3.9 – 3.13 x64** with `numpy` and `pandas` (`polars` is optional); the provider does **not** install Python |
| Rights | administrator: the assemblies are published into the GAC and the provider is registered in `machine.config` |

## Install from a GitHub release (recommended)

1. Open <https://github.com/answering007/PythonClient/releases/latest> and download
   `Pike.PythonClient64-<version>.zip` (plus `.zip.sha256` if you want to verify the download).
2. Extract the archive to a local folder. Do not run the files from inside the ZIP.
3. Run `install.cmd` and confirm the UAC prompt:

```text
Pike.PythonClient64-2.1.0\
  install.cmd  verify.cmd  uninstall.cmd
  lib\        Pike.PythonClient64.dll/.xml/.pdb, Python.Runtime.dll,
              Pike.PythonClient64.Install.dll/.xml
  scripts\    install.ps1, uninstall.ps1, Verify-Installation.ps1
  samples\power-query\   ready-made workbooks
```

`install.cmd` unblocks the extracted files (Mark-of-the-Web), requests elevation, reports the prerequisites and calls
`scripts\install.ps1 -SourcePath lib`. The script copies the six files to `%ProgramFiles%\Pike\PythonClient64`,
publishes `Pike.PythonClient64.dll` and `Python.Runtime.dll` into the GAC (`GAC_64`) and writes the fully qualified
provider record into the x64 `machine.config` (a backup `machine.config.bak-<utc>` is created). The result is verified
in a new process: the registered type must be loaded from `...\assembly\GAC_*`.

4. Restart Excel: Power Query keeps the provider inside the Excel process.
5. Check the installation at any time with `verify.cmd` (read-only, no administrator rights).

## Silent / unattended installation

```powershell
install.cmd -NoPause
# or directly, from the extracted archive:
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install.ps1 -SourcePath lib
```

Advanced switches of `install.ps1`: `-InstallPath <dir>`, `-SkipGac`, `-SkipMachineConfig`,
`-SkipPrerequisiteCheck`, `-PythonDll <path to python3xx.dll>` (used only by the prerequisite report).
`install.ps1` is idempotent: the same assembly identity is replaced, the `machine.config` record is updated in place.

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
| `Pike.PythonClient64.dll`, `.xml`, `Pike.PythonClient64.Install.dll`, `.xml`, `Python.Runtime.dll`, `.xml` | `%ProgramFiles%\Pike\PythonClient64` |
| `Pike.PythonClient64` (provider) and `Python.Runtime` (pythonnet) | `GAC_64` |
| provider record, invariant `Pike.PythonClient64.Data.PythonProviderFactory` | x64 `machine.config` → `configuration/system.data/DbProviderFactories` |

The record alone is not enough: both assemblies must be in the GAC, otherwise the runtime cannot resolve the type
outside the Excel folder. `AssemblyVersion` is fixed for the whole 2.x line, so an upgrade replaces the same GAC entry
and the existing `machine.config` record stays valid.

## Verify

```powershell
.\scripts\Verify-Installation.ps1                       # repository: compared with bin\Release
powershell -File scripts\Verify-Installation.ps1 -ReferencePath lib   # distribution ZIP
```

18 checks: installed files vs the reference build (SHA256), `machine.config` record and its version, GAC entries,
the GAC copy of the provider, the type resolved by `DbProviderFactories` in a new process and the presence of the
polars reader in the installed build. Exit code `1` with the failing check names; no administrator rights required.

## Uninstall

```powershell
uninstall.cmd                # distribution ZIP (elevated)
.\scripts\uninstall.ps1      # repository (elevated)
```

The `machine.config` record is removed first, then the GAC entries, then the files. Run the uninstall **before**
deleting the installation folder: `GacRemove` needs the file path, a deleted folder leaves an unreachable GAC entry
(the script prints a warning with the manual `gacutil` command in that case).

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `Administrator rights are required` | confirm the UAC prompt; the GAC and `machine.config` cannot be changed otherwise |
| `... cannot be loaded because running scripts is disabled` | use `install.cmd` / `verify.cmd`: they call Windows PowerShell with `-ExecutionPolicy Bypass` and unblock the files |
| `Can't find python*.dll file` | `PYTHONDLL` in the connection string must point to an existing 64-bit `python3xx.dll` |
| `ImportError: No module named pandas` (or numpy) | install the packages into the Python used by the connection string: `python -m pip install numpy pandas` |
| Excel still runs the old build | restart Excel, then `verify.cmd`: a stale GAC entry or a Debug build installed by mistake is reported there |
| `DbProviderFactories.GetFactory` fails | the provider is not registered (run `install.cmd`) or 32-bit Excel is used |
| `ConfigurationErrorsException: ... InvariantName ...` | the record exists twice (machine.config and the application `app.config`): keep one, or add `<remove invariant="..." />` before `<add>` |
