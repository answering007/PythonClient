<#
.SYNOPSIS
    Uninstalls the Pike.PythonClient64 ADO.NET provider (x64): removes the machine.config registration, the assemblies
    from the GAC and the installed files.

.DESCRIPTION
    The provider record is removed from the 64-bit machine.config (machine.config is backed up before the change),
    then Pike.PythonClient64.dll and Python.Runtime.dll are removed from the global assembly cache with
    System.EnterpriseServices.Internal.Publish (no gacutil / Windows SDK is required), and finally the installed files
    are deleted. The GAC must be cleaned even if the files are gone: a GAC assembly keeps being resolved by Excel
    after the folder is deleted.
    Administrator rights and 64-bit Windows PowerShell 5.1 are required.

.PARAMETER InstallPath
    Folder the provider was installed to. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER SkipMachineConfig
    Installed files are removed, machine.config is not changed

.PARAMETER SkipGac
    Installed files are removed, the GAC is not changed

.EXAMPLE
    .\scripts\uninstall.ps1
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'Pike\PythonClient64'),
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

$files = @(
    'Pike.PythonClient64.dll',
    'Pike.PythonClient64.xml',
    'Python.Runtime.dll',
    'Python.Runtime.xml',
    'Pike.PythonClient64.Install.dll',
    'Pike.PythonClient64.Install.xml'
)

if (-not $SkipMachineConfig) {
    $runtimeAssemblyPath = Join-Path $InstallPath 'Pike.PythonClient64.dll'
    $installAssemblyPath = Join-Path $InstallPath 'Pike.PythonClient64.Install.dll'
    if (-not (Test-Path $runtimeAssemblyPath) -or -not (Test-Path $installAssemblyPath)) {
        throw "Provider assemblies are not found in '$InstallPath', use -SkipMachineConfig to remove the files only"
    }

    Add-Type -Path $runtimeAssemblyPath
    Add-Type -Path $installAssemblyPath

    $installation = New-Object Pike.PythonClient64.Install.DbProviderInstallation
    $machineConfig = [Pike.PythonClient64.Install.DbProviderInstallation]::Machine64Config

    $installation.UnregisterProvider($machineConfig)
    Write-Host "Provider is unregistered from '$machineConfig'"
}

if (-not $SkipGac) {
    # System.EnterpriseServices (System.EnterpriseServices.Internal.Publish) is not loaded by default
    Add-Type -AssemblyName System.EnterpriseServices
    $publish = New-Object System.EnterpriseServices.Internal.Publish

    foreach ($name in @('Pike.PythonClient64.dll', 'Python.Runtime.dll')) {
        $filePath = Join-Path $InstallPath $name
        if (-not (Test-Path $filePath)) {
            # GacRemove takes a file path, so a deleted folder leaves an unreachable GAC entry
            Write-Warning "'$filePath' is not found, the GAC entry is not removed: reinstall and run the uninstall again or remove it with 'gacutil /u $([IO.Path]::GetFileNameWithoutExtension($name))'"
            continue
        }

        try {
            $publish.GacRemove($filePath)
            Write-Host "Removed from the GAC: $name"
        }
        catch {
            Write-Warning "Unable to remove '$name' from the GAC: $($_.Exception.Message)"
        }
    }
}

foreach ($file in $files) {
    $filePath = Join-Path $InstallPath $file
    if (-not (Test-Path $filePath)) {
        continue
    }

    Remove-Item -LiteralPath $filePath -Force
    Write-Host "Removed $file"
}

if (Test-Path $InstallPath) {
    $rest = Get-ChildItem -LiteralPath $InstallPath -Force
    if (-not $rest) {
        Remove-Item -LiteralPath $InstallPath -Force
        Write-Host "Removed '$InstallPath'"
    }
}

Write-Host 'Uninstall completed'
