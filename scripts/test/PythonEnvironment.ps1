<#
.SYNOPSIS
    Helper functions to create Python environments with uv and to configure the integration tests.

.DESCRIPTION
    The file is dot-sourced by the other test scripts:

        . "$PSScriptRoot\PythonEnvironment.ps1"

    Functions:
        New-PythonEnvironment   - creates a virtual environment and installs the packages
        Get-PythonRuntimeInfo   - returns PythonDll, SitePackages and Version of the environment
        Set-PythonTestEnvironment - sets PYTHONNET_TEST_DLL and PYTHONPATH for the current session
#>

function Get-RepositoryRoot {
    return (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
}

function Assert-UvIsAvailable {
    if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
        throw 'uv is not found in PATH. Install it from https://docs.astral.sh/uv/'
    }
}

function New-PythonEnvironment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$EnvironmentPath,
        [string]$PythonVersion = '3.11',
        [string[]]$Packages = @('numpy', 'pandas')
    )

    Assert-UvIsAvailable

    $python = Join-Path $EnvironmentPath 'Scripts\python.exe'
    if (-not (Test-Path $python)) {
        Write-Host "Creating virtual environment '$EnvironmentPath' (python $PythonVersion)..."
        & uv venv $EnvironmentPath --python $PythonVersion
        if ($LASTEXITCODE -ne 0) { throw "uv venv failed with exit code $LASTEXITCODE" }
    }

    Write-Host "Installing packages: $($Packages -join ', ')"
    & uv pip install --python $python @Packages
    if ($LASTEXITCODE -ne 0) { throw "uv pip install failed with exit code $LASTEXITCODE" }

    return $python
}

function Get-PythonRuntimeInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$EnvironmentPath
    )

    $python = Join-Path $EnvironmentPath 'Scripts\python.exe'
    if (-not (Test-Path $python)) {
        throw "'$python' is not found, create the environment with New-PythonEnvironment"
    }

    $runtime = (& $python -c "import sys, sysconfig; print(sys.base_prefix); print(sysconfig.get_paths()['purelib']); print('%d.%d' % sys.version_info[:2])") -split "`r?`n"
    $basePrefix = $runtime[0].Trim()
    $sitePackages = $runtime[1].Trim()
    $version = $runtime[2].Trim()

    $dllName = 'python{0}.dll' -f ($version -replace '\.', '')
    $pythonDll = Join-Path $basePrefix $dllName

    if (-not (Test-Path $pythonDll)) {
        $pythonDll = (Get-ChildItem -LiteralPath $basePrefix -Filter 'python3*.dll' -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^python3\d+\.dll$' } |
            Select-Object -First 1).FullName
    }

    if (-not $pythonDll) {
        throw "python3xx.dll is not found in '$basePrefix'"
    }

    return New-Object psobject -Property @{
        PythonVersion = $version
        PythonDll     = $pythonDll
        SitePackages  = $sitePackages
    }
}

function Set-PythonTestEnvironment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$EnvironmentPath
    )

    $info = Get-PythonRuntimeInfo -EnvironmentPath $EnvironmentPath

    $env:PYTHONNET_TEST_DLL = $info.PythonDll
    $env:PYTHONPATH = $info.SitePackages

    return $info
}
