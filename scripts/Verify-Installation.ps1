<#
.SYNOPSIS
    Verifies the installed Pike.PythonClient64 provider: files, machine.config record and GAC entries.

.DESCRIPTION
    Read-only check: neither the files, nor machine.config, nor the GAC are changed, administrator rights are not
    required. The script verifies that the machine runs the build produced by this repository:
      - the install folder contains the provider, the installer and pythonnet assemblies;
      - the installed provider and pythonnet are the same files as the reference build (SHA256): the local build output
        bin\<Configuration>\net48, or the "lib" folder of the distribution ZIP (-ReferencePath);
      - the x64 machine.config contains the fully qualified provider record and the registered version matches the
        installed assembly (a Debug build installed by mistake is reported here);
      - the provider and pythonnet are visible in the global assembly cache, the provider is resolved from the GAC in a
        NEW process and the GAC copy is the same file as the installed one (a stale GAC entry is the usual reason for
        "the old code is used after the reinstall");
      - DbProviderFactories.GetFactory resolves the invariant name in a NEW process;
      - the installed assembly contains the polars reader (Pike.PythonClient64.FrameReaders.PolarsFrameReader).
    The exit code is 1 when a check fails.

.PARAMETER InstallPath
    Installed folder. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER ReferencePath
    Folder with the reference build the installed files are compared with. Default: bin\<Configuration>\net48 of this
    repository; the distribution ZIP passes its own "lib" folder here.

.PARAMETER Configuration
    Build configuration the installed files are compared with. Default is Release

.PARAMETER SkipFileComparison
    Do not compare the installed files with the reference build (installation built on another machine)

.EXAMPLE
    .\scripts\Verify-Installation.ps1

.EXAMPLE
    .\scripts\Verify-Installation.ps1 -InstallPath D:\temp\pike -SkipFileComparison

.EXAMPLE
    .\scripts\Verify-Installation.ps1 -ReferencePath 'C:\Downloads\Pike.PythonClient64-2.1.0\lib'
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'Pike\PythonClient64'),
    [string]$ReferencePath,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipFileComparison
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Windows PowerShell 5.1 is required: the x64 machine.config and the installed assembly are inspected with .NET Framework APIs'
}

if (-not [Environment]::Is64BitProcess) {
    throw '64-bit PowerShell is required: Pike.PythonClient64 is an x64 assembly'
}

$root = Split-Path -Parent $PSScriptRoot
# Reference build: the local build output by default, the distribution payload when the script runs from a ZIP
$runtimePath = if (-not [string]::IsNullOrWhiteSpace($ReferencePath)) {
    if (-not (Test-Path -LiteralPath $ReferencePath)) { throw "Reference path '$ReferencePath' is not found" }

    (Resolve-Path -LiteralPath $ReferencePath).ProviderPath
}
else {
    Join-Path $root "Pike.PythonClient64\bin\$Configuration\net48"
}

# Label used by the check names: the ZIP layout passes its own payload, the repository uses the build output
$referenceName = if (-not [string]::IsNullOrWhiteSpace($ReferencePath)) { 'the distribution payload' } else { "the $Configuration build" }
$invariant = 'Pike.PythonClient64.Data.PythonProviderFactory'
$xPath = "/configuration/system.data/DbProviderFactories/add[@invariant='$invariant']"

# The x64 machine.config lives next to the current runtime directory:
# <Framework64>\v4.0.30319\Config\machine.config (the same rule as DataProviderInstallationBase.Machine64Config)
$runtimeDirectory = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory().TrimEnd('\')
$machineConfig = Join-Path $runtimeDirectory 'Config\machine.config'

$providerFile = Join-Path $InstallPath 'Pike.PythonClient64.dll'
$installerFile = Join-Path $InstallPath 'Pike.PythonClient64.Install.dll'
$pythonRuntimeFile = Join-Path $InstallPath 'Python.Runtime.dll'

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

# The assemblies loaded by the current process would hide a stale GAC entry, so the GAC checks run in a new process.
# -EncodedCommand avoids argument quoting problems, the streams are redirected to files: a native stderr line must not
# leak into the error stream of this script (it would be rendered as a PowerShell error before the check result)
function Invoke-InFreshProcess([string]$command) {
    $shell = Join-Path $PSHOME 'powershell.exe'
    $outputFile = [IO.Path]::GetTempFileName()
    $errorFile = [IO.Path]::GetTempFileName()

    try {
        $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        Start-Process -FilePath $shell -NoNewWindow -Wait -PassThru `
            -ArgumentList @('-NoProfile', '-NonInteractive', '-EncodedCommand', $encodedCommand) `
            -RedirectStandardOutput $outputFile -RedirectStandardError $errorFile | Out-Null

        $output = @(Get-Content -LiteralPath $outputFile -ErrorAction SilentlyContinue) +
            @(Get-Content -LiteralPath $errorFile -ErrorAction SilentlyContinue)

        return (($output | Out-String).Trim())
    }
    finally {
        Remove-Item -LiteralPath $outputFile, $errorFile -Force -ErrorAction SilentlyContinue
    }
}

function Get-ProviderRecord {
    $xml = New-Object Xml.XmlDocument
    $xml.Load($machineConfig)

    return $xml.SelectSingleNode($xPath)
}

function Get-FileHashText([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) {
        return ''
    }

    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

$providerName = if (Test-Path -LiteralPath $providerFile) { [Reflection.AssemblyName]::GetAssemblyName($providerFile) } else { $null }
$providerFullName = if ($providerName) { $providerName.FullName } else { '' }
$providerVersion = if ($providerName) { $providerName.Version.ToString() } else { '' }
$pythonRuntimeFullName = if (Test-Path -LiteralPath $pythonRuntimeFile) { [Reflection.AssemblyName]::GetAssemblyName($pythonRuntimeFile).FullName } else { '' }

$record = $null
try {
    $record = Get-ProviderRecord
}
catch {
    Write-Warning "Unable to read '$machineConfig': $($_.Exception.Message)"
}

$registeredType = if ($record) { $record.GetAttribute('type') } else { '' }
$registeredVersion = if ($registeredType -match 'Version=([^,\s]+)') { $Matches[1] } else { '' }

if (Get-Process -Name EXCEL -ErrorAction SilentlyContinue) {
    Write-Warning 'Excel is running: close it before running install.ps1, the provider files and the GAC entries are replaced'
}

Write-Host ''
Write-Host "Provider folder:   $InstallPath" -ForegroundColor Cyan
Write-Host "machine.config:    $machineConfig" -ForegroundColor Cyan
Write-Host "Installed version: $providerVersion" -ForegroundColor DarkGray
Write-Host "Registered type:   $registeredType" -ForegroundColor DarkGray

Write-Host ''
Write-Host 'Files' -ForegroundColor Cyan

Assert-That 'install folder exists' { Test-Path -LiteralPath $InstallPath }
Assert-That 'Pike.PythonClient64.dll is installed' { Test-Path -LiteralPath $providerFile }
Assert-That 'Pike.PythonClient64.Install.dll is installed' { Test-Path -LiteralPath $installerFile }
Assert-That 'Python.Runtime.dll is installed' { Test-Path -LiteralPath $pythonRuntimeFile }

if ($SkipFileComparison) {
    Write-Host '  skip   comparison with the build output (-SkipFileComparison)' -ForegroundColor DarkGray
}
else {
    Assert-That "reference build exists ($runtimePath)" {
        Test-Path -LiteralPath (Join-Path $runtimePath 'Pike.PythonClient64.dll')
    }
    Assert-That "installed provider matches $referenceName (SHA256)" {
        $expected = Get-FileHashText (Join-Path $runtimePath 'Pike.PythonClient64.dll')
        $expected -and $expected -eq (Get-FileHashText $providerFile)
    }
    Assert-That "installed pythonnet matches $referenceName (SHA256)" {
        $expected = Get-FileHashText (Join-Path $runtimePath 'Python.Runtime.dll')
        $expected -and $expected -eq (Get-FileHashText $pythonRuntimeFile)
    }
}

Write-Host ''
Write-Host 'machine.config' -ForegroundColor Cyan

Assert-That 'provider record exists' { $null -ne $record }
Assert-That 'record contains the fully qualified type name' {
    $registeredType -like "*$invariant, Pike.PythonClient64, Version=*Culture=neutral, PublicKeyToken=*"
}
Assert-That 'registered version matches the installed assembly' {
    $registeredVersion -and $providerVersion -and $registeredVersion -eq $providerVersion
}

Write-Host ''
Write-Host 'Global assembly cache (checked in a new process)' -ForegroundColor Cyan

# The GAC copy is loaded, not the install folder: a stale GAC entry is what makes Excel run the old code
$gacState = ''
if ($providerFullName) {
    $gacState = Invoke-InFreshProcess @"
`$assembly = [Reflection.Assembly]::Load('$providerFullName')
'{0}|{1}|{2}' -f `$assembly.GlobalAssemblyCache, `$assembly.Location, (`$null -ne `$assembly.GetType('Pike.PythonClient64.FrameReaders.PolarsFrameReader'))
"@
}

$gacParts = $gacState -split '\|'
$gacLocation = if ($gacParts.Count -gt 1) { $gacParts[1] } else { '' }
$polarsType = if ($gacParts.Count -gt 2) { $gacParts[2] } else { '' }

Assert-That 'provider is visible in the GAC' { $gacParts[0] -eq 'True' }
Assert-That 'provider is resolved from the GAC folder' { $gacLocation -like '*\assembly\GAC_*' }
Assert-That 'GAC copy is the installed file (SHA256)' {
    $gacHash = Get-FileHashText $gacLocation
    $gacHash -and $gacHash -eq (Get-FileHashText $providerFile)
}
if (-not $SkipFileComparison) {
    Assert-That "GAC copy matches $referenceName (SHA256)" {
        $expected = Get-FileHashText (Join-Path $runtimePath 'Pike.PythonClient64.dll')
        $expected -and $expected -eq (Get-FileHashText $gacLocation)
    }
}
Assert-That 'installed build supports polars results' { $polarsType -eq 'True' }

$factoryState = Invoke-InFreshProcess @"
`$factory = [System.Data.Common.DbProviderFactories]::GetFactory('$invariant')
if (`$null -eq `$factory) { 'none' } else { '{0}|{1}' -f `$factory.GetType().FullName, `$factory.GetType().Assembly.Location }
"@

Assert-That 'DbProviderFactories resolves the invariant name in a new process' {
    $factoryState -like "$invariant|*"
}
Assert-That 'factory assembly is loaded from the GAC' {
    ($factoryState -split '\|')[1] -like '*\assembly\GAC_*'
}

$pythonRuntimeGac = ''
if ($pythonRuntimeFullName) {
    $pythonRuntimeGac = Invoke-InFreshProcess "[Reflection.Assembly]::Load('$pythonRuntimeFullName').GlobalAssemblyCache"
}
Assert-That 'pythonnet is visible in the GAC' { $pythonRuntimeGac.Trim() -eq 'True' }

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host ("FAILED: {0} of {1} installation checks" -f $script:failures.Count, $script:checks) -ForegroundColor Red
    $script:failures | ForEach-Object { Write-Host "  - $_" }
    Write-Host ''
    Write-Host 'Run install.cmd (distribution ZIP) or install.ps1 (elevated) and verify again.' -ForegroundColor Yellow
    exit 1
}

Write-Host ("All {0} installation checks passed" -f $script:checks) -ForegroundColor Green

