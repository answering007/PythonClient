<#
.SYNOPSIS
    Uninstalls the Pike.PythonClient64 ADO.NET provider (x64): removes the machine.config registration
    and the installed files.

.DESCRIPTION
    The provider record is removed from the 64-bit machine.config (machine.config is backed up before the change),
    then the installed files are deleted. Administrator rights are required.

.PARAMETER InstallPath
    Folder the provider was installed to. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER SkipMachineConfig
    Installed files are removed, machine.config is not changed

.EXAMPLE
    .\scripts\uninstall.ps1
#>
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'Pike\PythonClient64'),
    [switch]$SkipMachineConfig
)

$ErrorActionPreference = 'Stop'

$isAdministrator = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $SkipMachineConfig -and -not $isAdministrator) {
    throw 'Administrator rights are required to update machine.config. Run the script as administrator or use -SkipMachineConfig'
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
