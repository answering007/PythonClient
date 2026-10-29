<#
.SYNOPSIS
    Runs the integration tests against several Python / pandas combinations.

.DESCRIPTION
    Every combination gets its own virtual environment created with uv in
    <repository root>\.venv-matrix\<name>. The provider is built once, then the
    "Python" test category is executed for each combination with its own
    PYTHONNET_TEST_DLL / PYTHONPATH.

    The summary shows the tested combinations; the script fails when a supported
    combination fails. Combinations marked as expected-unsupported are reported but
    do not fail the run (for example Python 3.13 with pythonnet 3.0.5).

.PARAMETER Combination
    Names of the combinations to run. Default is all combinations

.PARAMETER Configuration
    Build configuration. Default is Debug

.PARAMETER TimeoutSeconds
    Timeout of a single test run. Default is 600

.PARAMETER SkipBuild
    Skip the build step

.EXAMPLE
    .\scripts\test\Invoke-VersionMatrix.ps1

.EXAMPLE
    .\scripts\test\Invoke-VersionMatrix.ps1 -Combination py39-pandas15,py311-pandas31 -Configuration Release
#>
[CmdletBinding()]
param(
    [string[]]$Combination,
    [string]$Configuration = 'Debug',
    [int]$TimeoutSeconds = 600,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PythonEnvironment.ps1')

$root = Get-RepositoryRoot
$matrixRoot = Join-Path $root '.venv-matrix'
$testRunner = Join-Path $PSScriptRoot 'Invoke-Tests.ps1'

# Supported versions: .NET Framework 4.8 x64 + pythonnet 3.0.5 (Python 3.7-3.12 is supported upstream)
$combinations = @(
    [pscustomobject]@{
        Name                = 'py39-pandas15'
        Python              = '3.9'
        Packages            = @('numpy==1.26.4', 'pandas==1.5.3')
        ExpectedUnsupported = $false
        Comment             = 'oldest supported Python with pandas 1.x'
    },
    [pscustomobject]@{
        Name                = 'py311-pandas22'
        Python              = '3.11'
        Packages            = @('numpy==1.26.4', 'pandas==2.2.3')
        ExpectedUnsupported = $false
        Comment             = 'pandas 2.x, nanosecond datetime resolution'
    },
    [pscustomobject]@{
        Name                = 'py311-pandas31'
        Python              = '3.11'
        Packages            = @('numpy', 'pandas')
        ExpectedUnsupported = $false
        Comment             = 'latest pandas (3.x, microsecond datetime resolution)'
    },
    [pscustomobject]@{
        Name                = 'py313-pandas31'
        Python              = '3.13'
        Packages            = @('numpy', 'pandas')
        ExpectedUnsupported = $false
        Comment             = 'Python 3.13 (verified, pythonnet 3.0.5 supports 3.7-3.13)'
    }
)

if ($Combination) {
    $combinations = @($combinations | Where-Object { $Combination -contains $_.Name })
    if ($combinations.Count -eq 0) {
        throw "No combination matches '$($Combination -join ', ')'"
    }
}

if (-not $SkipBuild) {
    Write-Host "Building the solution ($Configuration)..."
    & dotnet build (Join-Path $root 'PythonClient.sln') -c $Configuration --nologo -v:q
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
}

$results = @()

foreach ($item in $combinations) {
    Write-Host ''
    Write-Host ("=== {0}: Python {1}, {2}" -f $item.Name, $item.Python, ($item.Packages -join ', ')) -ForegroundColor Cyan
    Write-Host "    $($item.Comment)"

    $environmentPath = Join-Path $matrixRoot $item.Name
    $status = 'passed'
    $errorMessage = ''

    try {
        $null = New-PythonEnvironment -EnvironmentPath $environmentPath -PythonVersion $item.Python -Packages $item.Packages
        $info = Get-PythonRuntimeInfo -EnvironmentPath $environmentPath
        Write-Host "    python $($info.PythonVersion): $($info.PythonDll)"

        & $testRunner -Category Python -Configuration $Configuration -NoBuild -TimeoutSeconds $TimeoutSeconds `
            -PythonDll $info.PythonDll -SitePackages $info.SitePackages
    }
    catch {
        $status = 'failed'
        $errorMessage = $_.Exception.Message
        Write-Host "    $errorMessage" -ForegroundColor Red
    }

    $results += New-Object psobject -Property @{
        Name                = $item.Name
        Python              = $item.Python
        Packages            = ($item.Packages -join ' ')
        Status              = $status
        ExpectedUnsupported = $item.ExpectedUnsupported
        Error               = $errorMessage
    }
}

Write-Host ''
Write-Host 'Version matrix summary' -ForegroundColor Cyan
$results | Format-Table -AutoSize @(
    @{ Label = 'Combination'; Expression = { $_.Name } },
    @{ Label = 'Python'; Expression = { $_.Python } },
    @{ Label = 'Packages'; Expression = { $_.Packages } },
    @{ Label = 'Status'; Expression = { $_.Status } },
    @{ Label = 'Expected'; Expression = { if ($_.ExpectedUnsupported) { 'unsupported' } else { 'supported' } } }
) | Out-String | Write-Host

$unexpected = @($results | Where-Object { $_.Status -eq 'failed' -and -not $_.ExpectedUnsupported })
$unexpectedUnsupported = @($results | Where-Object { $_.Status -eq 'passed' -and $_.ExpectedUnsupported })
$expectedFailures = @($results | Where-Object { $_.Status -eq 'failed' -and $_.ExpectedUnsupported })

if ($unexpectedUnsupported.Count -gt 0) {
    Write-Host ("NOTE: {0} is expected to be unsupported by pythonnet 3.0.5 but passed" -f `
            ($unexpectedUnsupported.Name -join ', ')) -ForegroundColor Yellow
}

if ($expectedFailures.Count -gt 0) {
    Write-Host ("Expected failures: {0}" -f ($expectedFailures.Name -join ', ')) -ForegroundColor Yellow
}

if ($unexpected.Count -gt 0) {
    $unexpected | ForEach-Object { Write-Host ("FAILED: {0} ({1})" -f $_.Name, $_.Error) -ForegroundColor Red }
    throw "Version matrix failed for: $($unexpected.Name -join ', ')"
}

Write-Host 'Version matrix passed for all supported combinations' -ForegroundColor Green
exit 0
