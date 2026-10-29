<#
.SYNOPSIS
    Installs the Pike.PythonClient64 ADO.NET provider (x64) and registers it in machine.config.

.DESCRIPTION
    Copies the build output into the target folder and registers the provider in the 64-bit machine.config.
    The registration is done by the provider itself (DataProviderInstallationBase), so machine.config is
    backed up as machine.config.bak-<utc> before the change. Administrator rights are required.

.PARAMETER InstallPath
    Target folder. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER Configuration
    Build configuration to install. Default is Release

.PARAMETER SkipMachineConfig
    Copies files only, machine.config is not changed

.EXAMPLE
    .\install.ps1

.EXAMPLE
    .\install.ps1 -Configuration Debug -SkipMachineConfig
#>
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'Pike\PythonClient64'),
    [string]$Configuration = 'Release',
    [switch]$SkipMachineConfig
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root "Pike.PythonClient64\bin\$Configuration"
if (-not (Test-Path $source)) {
    throw "Build output '$source' is not found. Build the solution first: dotnet build PythonClient.sln -c $Configuration"
}

$isAdministrator = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $SkipMachineConfig -and -not $isAdministrator) {
    throw 'Administrator rights are required to update machine.config. Run the script as administrator or use -SkipMachineConfig'
}

$files = @('Pike.PythonClient64.dll', 'Pike.PythonClient64.xml', 'Python.Runtime.dll', 'Python.Runtime.xml')

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
foreach ($file in $files) {
    $filePath = Join-Path $source $file
    if (-not (Test-Path $filePath)) {
        Write-Verbose "Skip '$file', it is not a part of the build output"
        continue
    }

    Copy-Item -LiteralPath $filePath -Destination $InstallPath -Force
    Write-Host "Copied $file"
}

if (-not $SkipMachineConfig) {
    $assemblyPath = Join-Path $InstallPath 'Pike.PythonClient64.dll'
    Add-Type -Path $assemblyPath

    $installation = New-Object Pike.PythonClient64.Install.DbProviderInstallation
    $machineConfig = [Pike.PythonClient64.Install.DbProviderInstallation]::Machine64Config

    $installation.RegisterProvider($machineConfig)
    Write-Host "Provider is registered in '$machineConfig'"
}

Write-Host "Installed to '$InstallPath'"
Write-Host 'Invariant name: Pike.PythonClient64.Data.PythonProviderFactory'
