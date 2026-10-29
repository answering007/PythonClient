<#
.SYNOPSIS
    Creates a local Python virtual environment (uv) with numpy and pandas and prepares
    the environment variables used by the integration tests.

.DESCRIPTION
    The environment is created in <repository root>\.venv by default. After the script finishes,
    PYTHONNET_TEST_DLL and PYTHONPATH are set in the current session when the script is dot-sourced:

        . .\scripts\test\Initialize-PythonEnvironment.ps1

    Requirements: uv (https://docs.astral.sh/uv/) must be available in PATH.

.PARAMETER PythonVersion
    Python version to install/use. Default is 3.11

.PARAMETER EnvironmentPath
    Virtual environment folder. Default is <repository root>\.venv

.PARAMETER Packages
    Packages to install. Default is numpy, pandas

.EXAMPLE
    . .\scripts\test\Initialize-PythonEnvironment.ps1

.EXAMPLE
    .\scripts\test\Initialize-PythonEnvironment.ps1 -PythonVersion 3.12 -Packages numpy,pandas,polars
#>
[CmdletBinding()]
param(
    [string]$PythonVersion = '3.11',
    [string]$EnvironmentPath,
    [string[]]$Packages = @('numpy', 'pandas')
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($EnvironmentPath)) {
    $EnvironmentPath = Join-Path $root '.venv'
}

if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    throw 'uv is not found in PATH. Install it from https://docs.astral.sh/uv/'
}

if (-not (Test-Path (Join-Path $EnvironmentPath 'Scripts\python.exe'))) {
    Write-Host "Creating virtual environment '$EnvironmentPath' (python $PythonVersion)..."
    & uv venv $EnvironmentPath --python $PythonVersion
}

$python = Join-Path $EnvironmentPath 'Scripts\python.exe'
Write-Host "Installing packages: $($Packages -join ', ')"
& uv pip install --python $python @Packages

# Python home (python3xx.dll location) and site-packages used by the tests
$basePrefix = (& $python -c "import sys; print(sys.base_prefix)").Trim()
$sitePackages = (& $python -c "import sysconfig; print(sysconfig.get_paths()['purelib'])").Trim()

$dllName = 'python{0}.dll' -f ($PythonVersion -replace '\.', '')
$pythonDll = Join-Path $basePrefix $dllName

if (-not (Test-Path $pythonDll)) {
    $pythonDll = (Get-ChildItem -LiteralPath $basePrefix -Filter 'python3*.dll' |
        Where-Object { $_.Name -match '^python3\d+\.dll$' } |
        Select-Object -First 1).FullName

    if (-not $pythonDll) {
        throw "python3xx.dll is not found in '$basePrefix'"
    }
}

$env:PYTHONNET_TEST_DLL = $pythonDll
$env:PYTHONPATH = $sitePackages

Write-Host ''
Write-Host "PYTHONNET_TEST_DLL = $pythonDll"
Write-Host "PYTHONPATH         = $sitePackages"
Write-Host ''
Write-Host 'Environment is ready. Run the integration tests:'
Write-Host '    .\scripts\test\Invoke-Tests.ps1 -Category Python'
