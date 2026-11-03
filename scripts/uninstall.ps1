<#
.SYNOPSIS
    Uninstalls the Pike.PythonClient64 ADO.NET provider (x64): removes the machine.config registration and the
    assemblies from the global assembly cache.

.DESCRIPTION
    The installation publishes only the runtime assemblies into the GAC (Pike.PythonClient64 and Python.Runtime) and
    keeps no files in %ProgramFiles%, so the script resolves them by their GAC folders instead of a known installation
    path. The provider record is removed from the 64-bit machine.config (a backup is created) by
    Pike.PythonClient64.Install.dll, which is NOT published in the GAC: the assembly is taken from the distribution
    payload, so the uninstall runs from the extracted archive (uninstall.cmd) or with an explicit -SourcePath (the
    repository build output is found automatically).
    The result is verified: an assembly that is still resolvable is reported with the manual gacutil command, and the
    script exits with code 1. An optional -InstallPath folder (created by install.ps1 -KeepFiles) and the legacy 2.1.0
    folder %ProgramFiles%\Pike\PythonClient64 are cleaned up when present.
    Administrator rights and 64-bit Windows PowerShell 5.1 are required.

.PARAMETER SourcePath
    Folder with Pike.PythonClient64.Install.dll (the "lib" folder of the distribution archive or the build output).
    Default: "..\lib" next to the scripts folder (distribution layout), otherwise
    Pike.PythonClient64.Install\bin\<Configuration> of the repository.

.PARAMETER InstallPath
    Optional folder created by install.ps1 -KeepFiles; its provider files are removed

.PARAMETER Configuration
    Build configuration used to locate the installer assembly in the repository. Default is Release

.PARAMETER SkipMachineConfig
    The GAC entries are removed, machine.config is not changed

.PARAMETER SkipGac
    machine.config is updated, the GAC is not changed

.EXAMPLE
    .\scripts\uninstall.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\uninstall.ps1 -SourcePath lib

.EXAMPLE
    .\scripts\uninstall.ps1 -SkipGac
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$SourcePath,
    [string]$InstallPath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipMachineConfig,
    [switch]$SkipGac
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Windows PowerShell 5.1 is required: the GAC is updated through System.EnterpriseServices.Internal.Publish, which is not available in PowerShell 7+'
}

if (-not [Environment]::Is64BitProcess) {
    throw '64-bit PowerShell is required: Pike.PythonClient64 is an x64 assembly'
}

$isAdministrator = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $SkipMachineConfig -and -not $isAdministrator) {
    throw 'Administrator rights are required to update machine.config. Run the script as administrator or use -SkipMachineConfig'
}

if (-not $SkipGac -and -not $isAdministrator) {
    throw 'Administrator rights are required to update the global assembly cache. Run the script as administrator or use -SkipGac'
}

# GAC lookup and the legacy install folder helpers (the same file is used by install.ps1 and Verify-Installation.ps1)
. (Join-Path $PSScriptRoot 'Installation.Common.ps1')

<#
.SYNOPSIS
    Finds Pike.PythonClient64.Install.dll: the registration code that is not published in the GAC.

.DESCRIPTION
    The distribution archive keeps it in "lib" next to the scripts folder, the repository in the build output.
#>
function Resolve-InstallerAssemblyPath {
    [CmdletBinding()]
    param(
        [string]$SourcePath,
        [string]$Configuration
    )

    $fileName = 'Pike.PythonClient64.Install.dll'
    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $candidates = @()

    if (-not [string]::IsNullOrWhiteSpace($SourcePath)) {
        $candidates += (Join-Path $SourcePath $fileName)
    }

    # distribution layout: <archive>\scripts\uninstall.ps1 and <archive>\lib
    $candidates += (Join-Path (Split-Path -Parent $PSScriptRoot) "lib\$fileName")
    # repository layout
    $candidates += (Join-Path $repositoryRoot "Pike.PythonClient64.Install\bin\$Configuration\$fileName")
    $candidates += (Join-Path $repositoryRoot "Pike.PythonClient64\bin\$Configuration\net48\$fileName")

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) { continue }
        if (-not (Test-Path -LiteralPath $candidate)) { continue }

        return (Resolve-Path -LiteralPath $candidate).ProviderPath
    }

    return $null
}

$invariant = 'Pike.PythonClient64.Data.PythonProviderFactory'
$machineConfig = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory().TrimEnd('\')) 'Config\machine.config'
$paths = Get-InstalledAssemblyPaths
$recordExists = Test-ProviderRecord -MachineConfig $machineConfig -Invariant $invariant
$anythingInstalled = $recordExists -or $paths.Provider -or $paths.PythonRuntime -or $paths.Installer

Write-Host ''
Write-Host 'Published assemblies' -ForegroundColor Cyan
foreach ($entry in $paths.GetEnumerator()) {
    if ($entry.Value) {
        Write-Host ("  {0,-14} {1}" -f $entry.Key, $entry.Value)
    }
    else {
        Write-Host ("  {0,-14} not found in the GAC" -f $entry.Key)
    }
}

if (-not $anythingInstalled) {
    Write-Host ''
    Write-Host 'Nothing to uninstall: the provider is not registered and no assembly is published in the GAC' -ForegroundColor Green
}
else {
    if (-not $SkipMachineConfig) {
        if (-not $recordExists) {
            Write-Host ''
            Write-Host "machine.config: the provider record is absent in '$machineConfig'"
        }
        else {
            $installerPath = Resolve-InstallerAssemblyPath -SourcePath $SourcePath -Configuration $Configuration
            if (-not $installerPath) {
                throw "Pike.PythonClient64.Install.dll is not found. Run uninstall.cmd from the extracted distribution archive or pass -SourcePath <folder with lib>"
            }

            $installation = New-DbProviderInstallation -AssemblyPath $installerPath
            $installation.UnregisterProvider($machineConfig)
            Write-Host "Provider is unregistered from '$machineConfig' (registration code: '$installerPath')"
        }
    }

    $failures = @()

    if (-not $SkipGac) {
        # System.EnterpriseServices (System.EnterpriseServices.Internal.Publish) is not loaded by default
        Add-Type -AssemblyName System.EnterpriseServices
        $publish = New-Object System.EnterpriseServices.Internal.Publish

        # The installer assembly is listed for the intermediate GAC layout: the GAC keeps runtime assemblies only
        $targets = @(
            @{ Name = $script:ProviderAssemblyName; Path = $paths.Provider },
            @{ Name = $script:PythonRuntimeAssemblyName; Path = $paths.PythonRuntime },
            @{ Name = $script:InstallerAssemblyName; Path = $paths.Installer }
        )

        foreach ($target in $targets) {
            if (-not $target.Path) { continue }

            try {
                # GacRemove takes a file path; the GAC copy path works as well as an installation folder path
                $publish.GacRemove($target.Path)
                Write-Host "Removed from the GAC: $($target.Name)"
            }
            catch {
                Write-Warning "Unable to remove '$($target.Name)' from the GAC: $($_.Exception.Message)"
            }
        }

        # A loaded assembly stays in the GAC until the using processes exit, so the result is verified
        foreach ($target in $targets) {
            $remaining = Get-GacAssemblyPath -Name $target.Name
            if (-not $remaining) { continue }

            $failures += $target.Name
            Write-Warning "'$($target.Name)' is still published in the GAC ('$remaining'). Close Excel / Visual Studio and rerun the script, or remove it with 'gacutil /u $($target.Name)'"
        }
    }

    if ($failures.Count -gt 0) {
        throw "Uninstall is not complete: $($failures -join ', ') is still published in the global assembly cache"
    }

    Write-Host ''
    Write-Host 'Uninstall completed' -ForegroundColor Green
}

# The installation does not copy files any more: only the legacy 2.1.0 folder and an explicit -KeepFiles folder remain.
# %ProgramFiles% needs administrator rights, so the cleanup is skipped for an unelevated run (e.g. -SkipGac -SkipMachineConfig)
if ($isAdministrator) {
    try {
        Remove-LegacyInstallFolder -Path $script:LegacyInstallPath -Quiet
        if (-not [string]::IsNullOrWhiteSpace($InstallPath)) {
            Remove-LegacyInstallFolder -Path $InstallPath
        }
    }
    catch {
        Write-Warning "The install folders could not be cleaned: $($_.Exception.Message)"
    }
}
else {
    Write-Verbose 'Skipping the install folder cleanup: administrator rights are required'
}
