<#
.SYNOPSIS
    Uninstalls the Pike.PythonClient64 ADO.NET provider (x64): removes the machine.config registration and the
    assemblies from the global assembly cache.

.DESCRIPTION
    The installation publishes the assemblies into the GAC only, so the script resolves them by their GAC folders
    (<GAC_64|GAC_MSIL>\<name>\v4.0_*) instead of a known installation path: the distribution archive (or the
    repository build output) is not needed to uninstall.
    The provider record is removed from the 64-bit machine.config first (machine.config is backed up before the
    change), then Pike.PythonClient64.dll, Python.Runtime.dll and Pike.PythonClient64.Install.dll are removed from the
    GAC. The result is verified: an assembly that is still resolvable is reported with the manual gacutil command.
    An optional -InstallPath folder (created by install.ps1 -KeepFiles) and the legacy 2.1.0 folder
    %ProgramFiles%\Pike\PythonClient64 are cleaned up when present.
    Administrator rights and 64-bit Windows PowerShell 5.1 are required.

.PARAMETER InstallPath
    Optional folder created by install.ps1 -KeepFiles; its provider files are removed

.PARAMETER SkipMachineConfig
    The GAC entries are removed, machine.config is not changed

.PARAMETER SkipGac
    machine.config is updated, the GAC is not changed

.EXAMPLE
    .\scripts\uninstall.ps1

.EXAMPLE
    .\scripts\uninstall.ps1 -SkipGac

.EXAMPLE
    .\scripts\uninstall.ps1 -InstallPath D:\temp\pike
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallPath,
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

$machineConfig = Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory().TrimEnd('\')) 'Config\machine.config'
$paths = Get-InstalledAssemblyPaths

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

if (-not $SkipMachineConfig) {
    if (-not $paths.Installer) {
        Write-Warning "The installer assembly is not published in the GAC, so '$machineConfig' is not changed. Remove the 'Pike.PythonClient64.Data.PythonProviderFactory' record manually or rerun install.ps1"
    }
    else {
        $installation = New-DbProviderInstallation -AssemblyPath $paths.Installer
        $installation.UnregisterProvider($machineConfig)
        Write-Host "Provider is unregistered from '$machineConfig'"
    }
}

$failures = @()

if (-not $SkipGac) {
    # System.EnterpriseServices (System.EnterpriseServices.Internal.Publish) is not loaded by default
    Add-Type -AssemblyName System.EnterpriseServices
    $publish = New-Object System.EnterpriseServices.Internal.Publish

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

# The installation does not copy files any more: only the legacy 2.1.0 folder and an explicit -KeepFiles folder remain
Remove-LegacyInstallFolder -Path $script:LegacyInstallPath -Quiet
if (-not [string]::IsNullOrWhiteSpace($InstallPath)) {
    Remove-LegacyInstallFolder -Path $InstallPath
}

Write-Host ''
if ($failures.Count -gt 0) {
    throw "Uninstall is not complete: $($failures -join ', ') is still published in the global assembly cache"
}

Write-Host 'Uninstall completed' -ForegroundColor Green
