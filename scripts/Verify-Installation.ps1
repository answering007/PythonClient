<#
.SYNOPSIS
    Verifies the installed Pike.PythonClient64 provider: GAC entries, machine.config record and, optionally, a
    diagnostics copy of the files.

.DESCRIPTION
    Read-only check: neither the files, nor machine.config, nor the GAC are changed, administrator rights are not
    required. The installation publishes the assemblies into the global assembly cache only, so the GAC is inspected
    first:
      - Pike.PythonClient64.dll and Python.Runtime.dll are published in the GAC, the provider is resolved from the GAC in
        a NEW process and the installer assembly (Pike.PythonClient64.Install) is NOT published: the GAC keeps runtime
        assemblies only, the registration code is taken from the distribution payload;
      - the registered type in the x64 machine.config is fully qualified and its version matches the installed assembly;
      - DbProviderFactories.GetFactory resolves the invariant name in a NEW process;
      - the installed build contains the polars reader (Pike.PythonClient64.FrameReaders.PolarsFrameReader);
      - the GAC copy is the same file as the reference build (SHA256: bin\<Configuration>\net48 of this repository, or
        the "lib" folder of the distribution ZIP with -ReferencePath);
      - the legacy 2.1.0 folder (%ProgramFiles%\Pike\PythonClient64) of the development builds is gone;
      - with -InstallPath: the diagnostics copy (-KeepFiles) contains the same files as the reference build.
    The exit code is 1 when a check fails.

.PARAMETER InstallPath
    Optional diagnostics folder created by install.ps1 -KeepFiles. Without it the folder checks are skipped: the
    installation does not copy files any more.

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
    .\scripts\Verify-Installation.ps1 -ReferencePath 'C:\Downloads\Pike.PythonClient64-2.2.0\lib'

.EXAMPLE
    .\scripts\Verify-Installation.ps1 -InstallPath D:\temp\pike -ReferencePath D:\temp\pike\lib
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallPath,
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

# GAC lookup and the legacy install folder helpers (the same file is used by install.ps1 and uninstall.ps1)
. (Join-Path $PSScriptRoot 'Installation.Common.ps1')

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

# The installation publishes the assemblies into the GAC only; -InstallPath enables the -KeepFiles folder checks
$paths = Get-InstalledAssemblyPaths
$checkFolder = -not [string]::IsNullOrWhiteSpace($InstallPath)
$providerFile = if ($checkFolder) { Join-Path $InstallPath 'Pike.PythonClient64.dll' } else { '' }
$installerFile = if ($checkFolder) { Join-Path $InstallPath 'Pike.PythonClient64.Install.dll' } else { '' }
$pythonRuntimeFile = if ($checkFolder) { Join-Path $InstallPath 'Python.Runtime.dll' } else { '' }

# Identity for the fresh-process checks: the published assembly, otherwise the reference build
$referenceProvider = Join-Path $runtimePath 'Pike.PythonClient64.dll'
$referencePythonRuntime = Join-Path $runtimePath 'Python.Runtime.dll'
$providerAssemblyPath = if ($paths.Provider) { $paths.Provider } elseif (Test-Path -LiteralPath $referenceProvider) { $referenceProvider } else { '' }
$pythonRuntimeAssemblyPath = if ($paths.PythonRuntime) { $paths.PythonRuntime } elseif (Test-Path -LiteralPath $referencePythonRuntime) { $referencePythonRuntime } else { '' }

$providerName = if ($providerAssemblyPath) { [Reflection.AssemblyName]::GetAssemblyName($providerAssemblyPath) } else { $null }
$providerFullName = if ($providerName) { $providerName.FullName } else { '' }
$providerVersion = if ($providerName) { $providerName.Version.ToString() } else { '' }
$pythonRuntimeFullName = if ($pythonRuntimeAssemblyPath) { [Reflection.AssemblyName]::GetAssemblyName($pythonRuntimeAssemblyPath).FullName } else { '' }

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
    Write-Warning 'Excel is running: close it before running install.ps1, the GAC entries and machine.config are replaced'
}

Write-Host ''
$providerLocation = if ($paths.Provider) { $paths.Provider } else { 'not found in the GAC' }
Write-Host "Provider in GAC:   $providerLocation" -ForegroundColor Cyan
Write-Host "machine.config:    $machineConfig" -ForegroundColor Cyan
Write-Host "Installed version: $providerVersion" -ForegroundColor DarkGray
Write-Host "Registered type:   $registeredType" -ForegroundColor DarkGray

Write-Host ''
Write-Host 'Reference build' -ForegroundColor Cyan

if ($SkipFileComparison) {
    Write-Host '  skip   comparison with the reference build (-SkipFileComparison)' -ForegroundColor DarkGray
}
else {
    Assert-That "reference build exists ($runtimePath)" {
        (Test-Path -LiteralPath $referenceProvider) -and (Test-Path -LiteralPath $referencePythonRuntime)
    }
}

Write-Host ''
Write-Host 'Global assembly cache (checked in a new process)' -ForegroundColor Cyan

Assert-That 'installer assembly is not published in the GAC (runtime assemblies only)' { $null -eq $paths.Installer }

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
if (-not $SkipFileComparison) {
    Assert-That "GAC copy matches $referenceName (SHA256)" {
        $expected = Get-FileHashText $referenceProvider
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
Write-Host 'machine.config' -ForegroundColor Cyan

Assert-That 'provider record exists' { $null -ne $record }
Assert-That 'record contains the fully qualified type name' {
    $registeredType -like "*$invariant, Pike.PythonClient64, Version=*Culture=neutral, PublicKeyToken=*"
}
Assert-That 'registered version matches the installed assembly' {
    $registeredVersion -and $providerVersion -and $registeredVersion -eq $providerVersion
}

Write-Host ''
Write-Host 'Legacy layout' -ForegroundColor Cyan

Assert-That "legacy install folder is removed ($script:LegacyInstallPath)" {
    -not (Test-Path -LiteralPath $script:LegacyInstallPath)
}

Write-Host ''
if ($checkFolder) {
    Write-Host "Diagnostics copy ($InstallPath)" -ForegroundColor Cyan

    Assert-That 'diagnostics folder exists' { Test-Path -LiteralPath $InstallPath }
    Assert-That 'Pike.PythonClient64.dll is copied' { Test-Path -LiteralPath $providerFile }
    Assert-That 'Pike.PythonClient64.Install.dll is copied' { Test-Path -LiteralPath $installerFile }
    Assert-That 'Python.Runtime.dll is copied' { Test-Path -LiteralPath $pythonRuntimeFile }

    if (-not $SkipFileComparison) {
        Assert-That "copied provider matches $referenceName (SHA256)" {
            $expected = Get-FileHashText $referenceProvider
            $expected -and $expected -eq (Get-FileHashText $providerFile)
        }
        Assert-That "copied pythonnet matches $referenceName (SHA256)" {
            $expected = Get-FileHashText $referencePythonRuntime
            $expected -and $expected -eq (Get-FileHashText $pythonRuntimeFile)
        }
    }
}
else {
    Write-Host 'Diagnostics copy' -ForegroundColor Cyan
    Write-Host '  skip   no -InstallPath: the installation publishes the GAC entries only' -ForegroundColor DarkGray
}

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host ("FAILED: {0} of {1} installation checks" -f $script:failures.Count, $script:checks) -ForegroundColor Red
    $script:failures | ForEach-Object { Write-Host "  - $_" }
    Write-Host ''
    Write-Host 'Run install.cmd (distribution ZIP) or install.ps1 (elevated) and verify again.' -ForegroundColor Yellow
    exit 1
}

Write-Host ("All {0} installation checks passed" -f $script:checks) -ForegroundColor Green

