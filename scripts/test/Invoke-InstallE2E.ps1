<#
.SYNOPSIS
    End-to-end check of the installation: GAC + machine.config registration, then uninstall.

.DESCRIPTION
    Runs scripts\install.ps1 into a temporary folder and verifies in a NEW process that
      - the machine.config record contains the fully qualified type name;
      - DbProviderFactories.GetFactory resolves the invariant name;
      - the factory assembly is loaded from the global assembly cache;
    then runs scripts\uninstall.ps1 and verifies that the record, the GAC entry and the files are gone.
    This is the automated counterpart of the "run install.ps1, then open Excel" manual step.

    Administrator rights and 64-bit Windows PowerShell 5.1 are required: machine.config and the GAC are changed
    and restored. The script refuses to run when the provider is already registered on the machine (-Force overrides).

.PARAMETER Configuration
    Build configuration to install. Default is Release

.PARAMETER InstallPath
    Temporary install folder. Default is %TEMP%\Pike.PythonClient64-e2e

.PARAMETER Force
    Run even when the provider is already registered on this machine

.EXAMPLE
    .\scripts\test\Invoke-InstallE2E.ps1 -Configuration Release

.EXAMPLE
    .\scripts\test\Invoke-InstallE2E.ps1 -InstallPath D:\temp\pike-e2e -Force
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$InstallPath = (Join-Path $env:TEMP 'Pike.PythonClient64-e2e'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Windows PowerShell 5.1 is required: the GAC is updated through System.EnterpriseServices.Internal.Publish'
}

if (-not [Environment]::Is64BitProcess) {
    throw '64-bit PowerShell is required: Pike.PythonClient64 is an x64 assembly'
}

$isAdministrator = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdministrator) {
    throw 'Administrator rights are required: machine.config and the global assembly cache are changed'
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$invariant = 'Pike.PythonClient64.Data.PythonProviderFactory'
$xPath = "/configuration/system.data/DbProviderFactories/add[@invariant='$invariant']"

# x64 machine.config: <Framework64>\v4.0.30319 -> <Framework64>\Config\machine.config
$runtimeDirectory = [IO.DirectoryInfo]([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory().TrimEnd('\'))
$machineConfig = Join-Path $runtimeDirectory.Parent.FullName 'Config\machine.config'

$script:failures = @()
$script:checks = 0

function Assert-That([string]$name, [scriptblock]$condition) {
    $script:checks++

    try {
        $result = & $condition
        if ($result) {
            Write-Host "  ok   $name"
        }
        else {
            Write-Host "  FAIL $name" -ForegroundColor Red
            $script:failures += $name
        }
    }
    catch {
        Write-Host "  FAIL $name ($($_.Exception.Message))" -ForegroundColor Red
        $script:failures += $name
    }
}

function Get-ProviderRecord {
    $xml = New-Object Xml.XmlDocument
    $xml.Load($machineConfig)

    return $xml.SelectSingleNode($xPath)
}

function Invoke-InFreshProcess([string]$command) {
    $shell = Join-Path $PSHOME 'powershell.exe'
    $output = & $shell -NoProfile -NonInteractive -Command $command 2>&1

    return ($output | Out-String).Trim()
}

if ($null -ne (Get-ProviderRecord) -and -not $Force) {
    throw "The provider is already registered in '$machineConfig'. Run scripts\uninstall.ps1 first or pass -Force"
}

try {
    Write-Host ''
    Write-Host "Installing '$InstallPath' ($Configuration)..." -ForegroundColor Cyan
    & (Join-Path $root 'scripts\install.ps1') -InstallPath $InstallPath -Configuration $Configuration

    $record = Get-ProviderRecord
    $registeredType = if ($record) { $record.GetAttribute('type') } else { '' }
    $pythonRuntimePath = Join-Path $InstallPath 'Python.Runtime.dll'
    $pythonRuntimeName = if (Test-Path $pythonRuntimePath) { [Reflection.AssemblyName]::GetAssemblyName($pythonRuntimePath).FullName } else { '' }
    $pythonRuntimeCheck = if ($pythonRuntimeName) { "[Reflection.Assembly]::Load('$pythonRuntimeName').GlobalAssemblyCache" } else { "'missing'" }

    Write-Host ''
    Write-Host 'Installed state' -ForegroundColor Cyan

    # A new process is required: install.ps1 already loaded the assemblies from the install folder
    $factory = Invoke-InFreshProcess @"
`$factory = [System.Data.Common.DbProviderFactories]::GetFactory('$invariant')
'{0}|{1}|{2}' -f `$factory.GetType().Assembly.GlobalAssemblyCache, `$factory.GetType().Assembly.Location, ($pythonRuntimeCheck)
"@

    Assert-That 'machine.config record contains the fully qualified name' {
        $registeredType -like "*$invariant, Pike.PythonClient64, Version=*PublicKeyToken=*"
    }
    Assert-That 'factory is resolved in a new process' { $factory -match '^(True|False)\|' }
    Assert-That 'factory assembly is loaded from the GAC' { $factory -match '^True\|.*\\assembly\\GAC_' }
    Assert-That 'pythonnet is loaded from the GAC' { $factory -match '\|True$' }

    Write-Host ''
    Write-Host "Uninstalling '$InstallPath'..." -ForegroundColor Cyan
    & (Join-Path $root 'scripts\uninstall.ps1') -InstallPath $InstallPath

    Write-Host ''
    Write-Host 'Uninstalled state' -ForegroundColor Cyan

    Assert-That 'machine.config record is removed' { $null -eq (Get-ProviderRecord) }
    Assert-That 'installed files are removed' { -not (Test-Path $InstallPath) }
    Assert-That 'type is no longer resolved from the GAC' {
        [string]::IsNullOrWhiteSpace((Invoke-InFreshProcess "[Type]::GetType('$registeredType')"))
    }
}
finally {
    # A failed run must not leave the machine registered: restore the original state
    if ($null -ne (Get-ProviderRecord)) {
        Write-Warning "The provider is still registered in '$machineConfig', cleaning up"
        & (Join-Path $root 'scripts\uninstall.ps1') -InstallPath $InstallPath
    }
}

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host ("FAILED: {0} of {1} checks" -f $script:failures.Count, $script:checks) -ForegroundColor Red
    $script:failures | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host ("All {0} installation e2e checks passed" -f $script:checks) -ForegroundColor Green
