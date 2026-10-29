<#
.SYNOPSIS
    Builds the solution and runs the tests with a timeout, printing failed tests.

.DESCRIPTION
    Uses vstest.console.exe from the latest Visual Studio installation (vswhere).
    Test categories:
        Unit   - parser/collection/converter tests, Python is not required;
        Python - integration tests, PYTHONNET_TEST_DLL must be set
                 (see scripts\test\Initialize-PythonEnvironment.ps1);
        All    - both categories.

    When the test host does not finish within -TimeoutSeconds, it is killed and the script fails,
    so a hung Python interpreter does not block the build.

.PARAMETER Category
    Test category: Unit, Python or All. Default is Unit

.PARAMETER Configuration
    Build configuration. Default is Debug

.PARAMETER Filter
    Custom TestCaseFilter, overrides -Category

.PARAMETER TimeoutSeconds
    Test run timeout. Default is 900

.PARAMETER NoBuild
    Skip the build step

.EXAMPLE
    .\scripts\test\Invoke-Tests.ps1

.EXAMPLE
    . .\scripts\test\Initialize-PythonEnvironment.ps1
    .\scripts\test\Invoke-Tests.ps1 -Category Python -TimeoutSeconds 300

.EXAMPLE
    .\scripts\test\Invoke-Tests.ps1 -Filter 'FullyQualifiedName~ColumnTypesTests' -NoBuild
#>
[CmdletBinding()]
param(
    [ValidateSet('Unit', 'Python', 'All')]
    [string]$Category = 'Unit',
    [string]$Configuration = 'Debug',
    [string]$Filter,
    [int]$TimeoutSeconds = 900,
    [switch]$NoBuild,
    [string]$PythonDll = $env:PYTHONNET_TEST_DLL,
    [string]$SitePackages = $env:PYTHONPATH
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$solution = Join-Path $root 'PythonClient.sln'

if (-not [string]::IsNullOrWhiteSpace($PythonDll)) {
    $env:PYTHONNET_TEST_DLL = $PythonDll
}

if (-not [string]::IsNullOrWhiteSpace($SitePackages)) {
    $env:PYTHONPATH = $SitePackages
}

# vstest.console.exe from the latest Visual Studio
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe is not found at '$vswhere'. Visual Studio 2019+ is required"
}

$visualStudio = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $visualStudio) {
    throw 'Visual Studio installation with MSBuild is not found'
}

$vstest = Join-Path $visualStudio 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
if (-not (Test-Path $vstest)) {
    throw "vstest.console.exe is not found at '$vstest'"
}

$testAssembly = Join-Path $root "Pike.PythonClient.Test\bin\$Configuration\Pike.PythonClient.Test.dll"

if (-not $NoBuild) {
    Write-Host "Building $solution ($Configuration)..."
    & dotnet build $solution -c $Configuration --nologo -v:q
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
}

if (-not (Test-Path $testAssembly)) {
    throw "Test assembly '$testAssembly' is not found, build the solution first"
}

if ([string]::IsNullOrWhiteSpace($Filter)) {
    $Filter = switch ($Category) {
        'Unit' { 'TestCategory=Unit' }
        'Python' { 'TestCategory=Python' }
        default { '' }
    }
}

if ($Category -eq 'Python' -or $Category -eq 'All') {
    if ([string]::IsNullOrWhiteSpace($env:PYTHONNET_TEST_DLL)) {
        Write-Warning 'PYTHONNET_TEST_DLL is not set, the Python tests will be reported as inconclusive'
    }
}

$resultsFolder = Join-Path $root 'TestResults'
$logFile = Join-Path $env:TEMP "pike-tests-$([Guid]::NewGuid().ToString('N')).log"

$arguments = @($testAssembly, "/logger:trx")
if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $arguments += "/TestCaseFilter:$Filter"
}

Write-Host "Running tests (filter: '$Filter', timeout: $TimeoutSeconds s)..."
$process = Start-Process -FilePath $vstest -ArgumentList $arguments -WorkingDirectory $root -NoNewWindow -PassThru `
    -RedirectStandardOutput $logFile -RedirectStandardError "$logFile.err"

if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Get-Process testhost, testhost.x86 -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host ''
    Get-Content $logFile -Tail 40 -ErrorAction SilentlyContinue
    throw "Test run exceeded $TimeoutSeconds seconds and was terminated. The Python interpreter may have hung"
}

# Extended failure details from the trx report
$trx = Get-ChildItem $resultsFolder -Recurse -Filter '*.trx' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1

if (-not $trx) {
    Get-Content $logFile -Tail 30 -ErrorAction SilentlyContinue
    throw 'Test report is not found, the test run did not produce any result'
}

$xml = [xml](Get-Content $trx.FullName)
$failed = @($xml.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' })
foreach ($test in $failed) {
    Write-Host ''
    Write-Host "FAILED: $($test.testName)" -ForegroundColor Red
    $message = $test.Output.ErrorInfo.Message
    if ($message) { Write-Host "  $message" }
    $stack = $test.Output.ErrorInfo.StackTrace
    if ($stack) { ($stack -split "`n" | Select-Object -First 5) | ForEach-Object { Write-Host "  $_" } }
}

$summary = $xml.TestRun.ResultSummary.Counters
$skipped = [int]$summary.total - [int]$summary.passed - [int]$summary.failed
Write-Host ''
Write-Host ("Total: {0}, Passed: {1}, Failed: {2}, Skipped/Inconclusive: {3}" -f `
    $summary.total, $summary.passed, $summary.failed, $skipped)

if ($failed.Count -gt 0) {
    throw "Tests failed: $($failed.Count)"
}

Write-Host 'All tests passed'
