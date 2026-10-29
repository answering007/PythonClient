<#
.SYNOPSIS
    Installs the Pike.PythonClient64 ADO.NET provider (x64) and registers it in machine.config.

.DESCRIPTION
    Copies the build output into the target folder and registers the provider in the 64-bit machine.config.
    The registration is done by Pike.PythonClient64.Install.dll (DataProviderInstallationBase), which is a
    separate assembly: the runtime provider loaded by Excel/Power Query has no installer dependency.
    machine.config is backed up as machine.config.bak-<utc> before the change. Administrator rights are required.

.PARAMETER InstallPath
    Target folder. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER Configuration
    Build configuration to install. Default is Release

.PARAMETER SkipMachineConfig
    Copies files only, machine.config is not changed

.EXAMPLE
    .\scripts\install.ps1

.EXAMPLE
    .\scripts\install.ps1 -Configuration Debug -SkipMachineConfig
#>
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'Pike\PythonClient64'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipMachineConfig
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
# net48 is the deployment target for Excel / Power Query
$runtimePath = Join-Path $root "Pike.PythonClient64\bin\$Configuration\net48"
$installBinPath = Join-Path $root "Pike.PythonClient64.Install\bin\$Configuration"

if (-not (Test-Path $runtimePath)) {
    throw "Build output '$runtimePath' is not found. Build the solution first: dotnet build PythonClient.sln -c $Configuration"
}

$isAdministrator = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $SkipMachineConfig -and -not $isAdministrator) {
    throw 'Administrator rights are required to update machine.config. Run the script as administrator or use -SkipMachineConfig'
}

# Runtime files first: the installer assembly references the provider assembly
$files = @(
    @{ Name = 'Pike.PythonClient64.dll'; Source = $runtimePath },
    @{ Name = 'Pike.PythonClient64.xml'; Source = $runtimePath },
    @{ Name = 'Python.Runtime.dll'; Source = $runtimePath },
    @{ Name = 'Python.Runtime.xml'; Source = $runtimePath },
    @{ Name = 'Pike.PythonClient64.Install.dll'; Source = $installBinPath },
    @{ Name = 'Pike.PythonClient64.Install.xml'; Source = $installBinPath }
)

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
foreach ($file in $files) {
    $filePath = Join-Path $file.Source $file.Name
    if (-not (Test-Path $filePath)) {
        Write-Verbose "Skip '$($file.Name)', it is not a part of the build output"
        continue
    }

    Copy-Item -LiteralPath $filePath -Destination $InstallPath -Force
    Write-Host "Copied $($file.Name)"
}

if (-not $SkipMachineConfig) {
    Add-Type -Path (Join-Path $InstallPath 'Pike.PythonClient64.dll')
    Add-Type -Path (Join-Path $InstallPath 'Pike.PythonClient64.Install.dll')

    $installation = New-Object Pike.PythonClient64.Install.DbProviderInstallation
    $machineConfig = [Pike.PythonClient64.Install.DbProviderInstallation]::Machine64Config

    $installation.RegisterProvider($machineConfig)
    Write-Host "Provider is registered in '$machineConfig'"
}

Write-Host "Installed to '$InstallPath'"
Write-Host 'Invariant name: Pike.PythonClient64.Data.PythonProviderFactory'
