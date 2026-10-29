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
    . .\scripts\test\Initialize-PythonEnvironment.ps1 -PythonVersion 3.12 -Packages numpy,pandas,polars
#>
[CmdletBinding()]
param(
    [string]$PythonVersion = '3.11',
    [string]$EnvironmentPath,
    [string[]]$Packages = @('numpy', 'pandas')
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PythonEnvironment.ps1')

if ([string]::IsNullOrWhiteSpace($EnvironmentPath)) {
    $EnvironmentPath = Join-Path (Get-RepositoryRoot) '.venv'
}

$null = New-PythonEnvironment -EnvironmentPath $EnvironmentPath -PythonVersion $PythonVersion -Packages $Packages
$info = Set-PythonTestEnvironment -EnvironmentPath $EnvironmentPath

Write-Host ''
Write-Host "Python             = $($info.PythonVersion)"
Write-Host "PYTHONNET_TEST_DLL = $($info.PythonDll)"
Write-Host "PYTHONPATH         = $($info.SitePackages)"
Write-Host ''
Write-Host 'Environment is ready. Run the integration tests:'
Write-Host '    .\scripts\test\Invoke-Tests.ps1 -Category Python'
