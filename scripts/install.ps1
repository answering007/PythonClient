<#
.SYNOPSIS
    Installs the Pike.PythonClient64 ADO.NET provider (x64): publishes the assemblies into the GAC and registers
    the provider in machine.config.

.DESCRIPTION
    The provider and its pythonnet dependency are strong-named, so they are installed into the global assembly cache:
    machine.config contains the fully qualified type name (Version/Culture/PublicKeyToken), and the runtime resolves
    an assembly outside the host application base (Power Query / Excel) only by a full name. Python.Runtime.dll must
    be in the GAC as well, because the dependencies of a GAC assembly are probed in the GAC, not in the app folder.
    The GAC is updated through System.EnterpriseServices.Internal.Publish, so no gacutil / Windows SDK is required.
    The registration is done by Pike.PythonClient64.Install.dll (DataProviderInstallationBase), which is a separate
    assembly without a dependency in the runtime provider: it is loaded from the payload and is NOT published into the
    GAC, so the GAC keeps the runtime assemblies only. uninstall.ps1 therefore runs from the extracted archive (its
    "lib" folder is passed as -SourcePath), where the same installer assembly is available.
    No files are copied to %ProgramFiles% any more (2.1.0 kept a copy there): the GAC is the single source of truth,
    and an existing legacy folder is cleaned up by this script.
    machine.config is backed up as machine.config.bak-<utc> before the change.
    Administrator rights and 64-bit Windows PowerShell 5.1 are required.

.PARAMETER InstallPath
    Diagnostic copy folder, used only together with -KeepFiles. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER Configuration
    Build configuration to install. Default is Release

.PARAMETER SourcePath
    Folder with the distribution payload (Pike.PythonClient64.dll/.xml, Python.Runtime.dll/.xml,
    Pike.PythonClient64.Install.dll/.xml). When omitted, the local build output bin\<Configuration> is used.
    The distribution ZIP (scripts\build-distribution.ps1) passes its own "lib" folder here.

.PARAMETER KeepFiles
    Also copy the assemblies to -InstallPath (diagnostics/support). Off by default: the GAC entries are enough
    for Excel / Power Query and for .NET hosts that resolve the provider by its full name.

.PARAMETER PythonDll
    Optional full path to python3xx.dll used by the prerequisite report (the installation itself does not need it)

.PARAMETER SkipPrerequisiteCheck
    Do not report the machine prerequisites (Windows/.NET/Excel/Python) before the installation

.PARAMETER SkipMachineConfig
    The GAC is updated, machine.config is not changed

.PARAMETER SkipGac
    machine.config is updated, the GAC is not changed

.EXAMPLE
    .\scripts\install.ps1

.EXAMPLE
    .\scripts\install.ps1 -Configuration Debug -SkipMachineConfig -SkipGac

.EXAMPLE
    .\scripts\install.ps1 -SourcePath 'C:\Downloads\Pike.PythonClient64-2.1.0\lib'

.EXAMPLE
    .\scripts\install.ps1 -SourcePath 'C:\Downloads\Pike.PythonClient64-2.1.0\lib' -KeepFiles -InstallPath D:\temp\pike
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'Pike\PythonClient64'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$SourcePath,
    [switch]$KeepFiles,
    [string]$PythonDll,
    [switch]$SkipPrerequisiteCheck,
    [switch]$SkipMachineConfig,
    [switch]$SkipGac
)

$ErrorActionPreference = 'Stop'

# GAC lookup and the legacy install folder helpers (the same file is used by uninstall.ps1 and Verify-Installation.ps1)
. (Join-Path $PSScriptRoot 'Installation.Common.ps1')

if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Windows PowerShell 5.1 is required: the GAC is updated through System.EnterpriseServices.Internal.Publish, which is not available in PowerShell 7+'
}

if (-not [Environment]::Is64BitProcess) {
    throw '64-bit PowerShell is required: Pike.PythonClient64 is an x64 assembly'
}

# Windows PowerShell 5.1 host used for the checks that must run in a clean process
$powershellPath = Join-Path $PSHOME 'powershell.exe'

$root = Split-Path -Parent $PSScriptRoot
# net48 is the deployment target for Excel / Power Query
$runtimePath = Join-Path $root "Pike.PythonClient64\bin\$Configuration\net48"
$installBinPath = Join-Path $root "Pike.PythonClient64.Install\bin\$Configuration"

# The distribution ZIP keeps every assembly in one 'lib' folder, so the same script serves both layouts
if (-not [string]::IsNullOrWhiteSpace($SourcePath)) {
    if (-not (Test-Path -LiteralPath $SourcePath)) {
        throw "Source path '$SourcePath' is not found"
    }

    $runtimePath = (Resolve-Path -LiteralPath $SourcePath).ProviderPath
    $installBinPath = $runtimePath
    Write-Host "Payload: '$runtimePath'"
}

if (-not (Test-Path $runtimePath)) {
    throw "Build output '$runtimePath' is not found. Build the solution first: dotnet build PythonClient.sln -c $Configuration"
}

$isAdministrator = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $SkipMachineConfig -and -not $isAdministrator) {
    throw 'Administrator rights are required to update machine.config. Run the script as administrator or use -SkipMachineConfig'
}

if (-not $SkipGac -and -not $isAdministrator) {
    throw 'Administrator rights are required to update the global assembly cache. Run the script as administrator or use -SkipGac'
}

# --- Prerequisites ----------------------------------------------------------------------------------
# The provider installs neither Python nor .NET Framework, so the machine is only reported here and the
# installation is never blocked by a missing optional component

function Test-PortableExecutableX64([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) { return $false }

    try {
        $stream = [IO.File]::OpenRead($path)
        try {
            $reader = New-Object IO.BinaryReader($stream)
            $stream.Position = 0x3C
            $peOffset = $reader.ReadInt32()
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) { return $false }   # 'PE\0\0' signature
            return $reader.ReadUInt16() -eq 0x8664                        # IMAGE_FILE_MACHINE_AMD64
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Get-ExcelPath {
    $key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe'
    $item = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
    if ($null -eq $item) { return $null }

    if ($item.'(default)') { return $item.'(default)' }
    if ($item.Path) { return $item.Path }

    return $null
}

function Find-PythonDll {
    if (-not [string]::IsNullOrWhiteSpace($PythonDll)) { return $PythonDll }

    $patterns = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python3*\python3*.dll'),
        'C:\Python3*\python3*.dll',
        (Join-Path $env:USERPROFILE 'anaconda3\python3*.dll'),
        (Join-Path $env:USERPROFILE 'miniconda3\python3*.dll'),
        'C:\ProgramData\Anaconda3\python3*.dll'
    )

    $candidates = foreach ($pattern in $patterns) {
        Get-ChildItem -Path $pattern -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^python3\d+\.dll$' }
    }

    $found = $candidates |
        Sort-Object -Property @{ Expression = { $_.VersionInfo.FileVersion } } -Descending |
        Select-Object -First 1

    if ($found) { return $found.FullName }

    return $null
}

$script:prerequisitesFailed = 0

function Write-Prerequisite([string]$name, [scriptblock]$condition, [string]$hint) {
    $ok = $false
    try {
        $ok = [bool](& $condition)
    }
    catch {
        $ok = $false
    }

    if ($ok) {
        Write-Host "  ok   $name"
        return
    }

    Write-Host "  WARN $name" -ForegroundColor Yellow
    if (-not [string]::IsNullOrWhiteSpace($hint)) {
        Write-Host "       $hint" -ForegroundColor DarkGray
    }

    $script:prerequisitesFailed++
}

if (-not $SkipPrerequisiteCheck) {
    Write-Host ''
    Write-Host 'Prerequisites' -ForegroundColor Cyan

    Write-Prerequisite 'Windows x64' { [Environment]::Is64BitOperatingSystem } `
        'Pike.PythonClient64 is an x64 provider.'
    Write-Prerequisite 'Windows PowerShell 5.1 (64-bit)' {
        $PSVersionTable.PSEdition -eq 'Desktop' -and [Environment]::Is64BitProcess
    } 'The GAC is updated through System.EnterpriseServices.Internal.Publish, which exists only in Windows PowerShell 5.1.'
    Write-Prerequisite '.NET Framework 4.8' {
        $release = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue).Release
        $release -ge 528040
    } 'Windows 10 1903+ and Windows 11 already ship .NET Framework 4.8.'
    Write-Prerequisite 'Excel x64 (Power Query / Excel scenario)' {
        $platform = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration' -ErrorAction SilentlyContinue).Platform
        if ($platform) { return $platform -eq 'x64' }

        return Test-PortableExecutableX64 (Get-ExcelPath)
    } '32-bit Excel cannot load the provider; .NET-only hosts are not affected.'

    $python = Find-PythonDll
    if ($python) {
        Write-Host "  ok   Python found: $python"

        $pythonExe = Join-Path (Split-Path -Parent $python) 'python.exe'
        Write-Prerequisite 'numpy and pandas are importable' {
            if (-not (Test-Path -LiteralPath $pythonExe)) { return $false }

            & $pythonExe -c 'import numpy, pandas' 2>$null | Out-Null
            return $LASTEXITCODE -eq 0
        } "Run: & '$pythonExe' -m pip install numpy pandas"
    }
    else {
        Write-Host '  WARN Python 3.x x64 is not found' -ForegroundColor Yellow
        Write-Host '       The provider does not install Python: install it and point PYTHONDLL to python3xx.dll' -ForegroundColor DarkGray
        $script:prerequisitesFailed++
    }

    if ($script:prerequisitesFailed -gt 0) {
        Write-Host ''
        Write-Host "Warnings: $($script:prerequisitesFailed). See the Requirements section of README.md." -ForegroundColor Yellow
    }
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

# Assemblies published into the GAC: the provider and its runtime dependency. The installer assembly is NOT published:
# it is loaded from the payload by install.ps1 / uninstall.ps1 only, so the GAC keeps runtime assemblies only
$gacFiles = @('Pike.PythonClient64.dll', 'Python.Runtime.dll')

# Source file of every published assembly: the payload folder (ZIP "lib" or the build output), not an install folder
$gacSources = @{
    'Pike.PythonClient64.dll'         = Join-Path $runtimePath 'Pike.PythonClient64.dll'
    'Python.Runtime.dll'              = Join-Path $runtimePath 'Python.Runtime.dll'
    'Pike.PythonClient64.Install.dll' = Join-Path $installBinPath 'Pike.PythonClient64.Install.dll'
}

$gacInstalled = @()

# System.EnterpriseServices (System.EnterpriseServices.Internal.Publish) is not loaded by default
function New-GacPublisher {
    Add-Type -AssemblyName System.EnterpriseServices

    return New-Object System.EnterpriseServices.Internal.Publish
}

function Remove-GacAssembly([Object]$publisher, [string]$path) {
    if (-not (Test-Path $path)) {
        return
    }

    try {
        $publisher.GacRemove($path)
    }
    catch {
        Write-Warning "Unable to remove '$path' from the GAC: $($_.Exception.Message)"
    }
}

# The GAC entries are the installation: files are copied only when the diagnostics copy is requested
if ($KeepFiles) {
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
}

if ($SkipGac -and -not $SkipMachineConfig) {
    Write-Warning 'The provider is registered in machine.config while -SkipGac leaves the assemblies out of the GAC: the record will not resolve until they are published.'
}

try {
    if (-not $SkipGac) {
        $publish = New-GacPublisher

        foreach ($name in $gacFiles) {
            $filePath = $gacSources[$name]
            if (-not $filePath -or -not (Test-Path $filePath)) {
                Write-Verbose "Skip '$name', it is not a part of the payload"
                continue
            }

            # The same assembly identity is overwritten, so the call is idempotent
            $publish.GacRemove($filePath)
            $publish.GacInstall($filePath)
            $gacInstalled += $filePath
            Write-Host "Installed into the GAC: $name"
        }

        # GacInstall does not fail when the GAC is not writable, so the result is verified in a new process
        foreach ($name in $gacFiles) {
            $filePath = $gacSources[$name]
            if (-not $filePath -or -not (Test-Path $filePath)) {
                continue
            }

            $assemblyFullName = [Reflection.AssemblyName]::GetAssemblyName($filePath).FullName
            $inGac = & $powershellPath -NoProfile -NonInteractive -Command `
                "[Reflection.Assembly]::Load('$assemblyFullName').GlobalAssemblyCache"

            if ($inGac -ne 'True') {
                throw "'$assemblyFullName' is not visible in the global assembly cache after GacInstall (got '$inGac'). Run the script as administrator"
            }

            Write-Host "Verified in the GAC: $assemblyFullName"
        }
    }

    if (-not $SkipMachineConfig) {
        $providerSource = $gacSources['Pike.PythonClient64.dll']
        $installerSource = $gacSources['Pike.PythonClient64.Install.dll']
        if (-not (Test-Path -LiteralPath $providerSource) -or -not (Test-Path -LiteralPath $installerSource)) {
            throw "The payload must contain '$providerSource' and '$installerSource'"
        }

        Add-Type -Path $providerSource
        Add-Type -Path $installerSource

        $installation = New-Object Pike.PythonClient64.Install.DbProviderInstallation
        $machineConfig = [Pike.PythonClient64.Install.DbProviderInstallation]::Machine64Config

        $installation.RegisterProvider($machineConfig)
        Write-Host "Provider is registered in '$machineConfig'"
        Write-Host "Registered type: $($installation.QualifiedName)"

        if (-not $SkipGac) {
            # Self-check in a new process: the assemblies loaded above would hide a broken GAC registration
            $location = & $powershellPath -NoProfile -NonInteractive -Command `
                "[Type]::GetType('$($installation.QualifiedName)').Assembly.Location"

            if (-not $location -or $location -notlike '*\assembly\GAC_*') {
                throw "The registered type is not resolved from the GAC (got '$location'). Check that the payload contains the strong-named assemblies and rerun the script"
            }

            Write-Host "Verified: the provider is loaded from '$location'"
        }
    }
}
catch {
    # Roll back the GAC changes made by this run, so a half-installed machine is not left behind
    if ($gacInstalled.Count -gt 0) {
        $publish = New-GacPublisher
        foreach ($path in $gacInstalled) {
            Remove-GacAssembly $publish $path
        }
    }

    throw
}

# The installer assembly of the intermediate GAC layout is not a runtime dependency: remove it if it is still published
if (-not $SkipGac) {
    $leftoverInstaller = Get-GacAssemblyPath -Name $script:InstallerAssemblyName
    if ($leftoverInstaller) {
        try {
            $publish = New-GacPublisher
            $publish.GacRemove($leftoverInstaller)
            Write-Host "Removed the installer assembly from the GAC: $($script:InstallerAssemblyName) (runtime assemblies only)"
        }
        catch {
            Write-Warning "Unable to remove '$($script:InstallerAssemblyName)' from the GAC: $($_.Exception.Message)"
        }
    }
}

# The 2.1.0 layout copied the assemblies to %ProgramFiles%: the new installation publishes them into the GAC only,
# so the legacy folder is cleaned up (unless the diagnostic copy is requested exactly there)
$legacyPath = $script:LegacyInstallPath
$keepsLegacyPath = $KeepFiles -and [string]::Equals(
    [IO.Path]::GetFullPath($InstallPath), [IO.Path]::GetFullPath($legacyPath), [StringComparison]::OrdinalIgnoreCase)

if (-not $SkipGac -and -not $keepsLegacyPath) {
    try {
        Remove-LegacyInstallFolder -Path $legacyPath -Quiet
    }
    catch {
        Write-Warning "The legacy install folder '$legacyPath' could not be cleaned: $($_.Exception.Message)"
    }
}

Write-Host ''
if ($SkipGac -or $SkipMachineConfig) {
    Write-Host 'Installation finished with skipped steps' -ForegroundColor Yellow
    if ($SkipGac) { Write-Host '  GAC: not changed (-SkipGac)' }
    if ($SkipMachineConfig) { Write-Host '  machine.config: not changed (-SkipMachineConfig)' }
}
else {
    Write-Host 'Installed into the global assembly cache and registered in machine.config' -ForegroundColor Green
}
if ($KeepFiles) { Write-Host "Diagnostic copy: '$InstallPath'" }
Write-Host 'Invariant name: Pike.PythonClient64.Data.PythonProviderFactory'
Write-Host ''
Write-Host 'Next steps' -ForegroundColor Cyan
Write-Host '  1. Restart Excel: Power Query keeps the provider inside the Excel process.'
Write-Host '  2. Connection string (Power Query / AdoDotNet.Query):'
Write-Host '       PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH="C:\Python311\Lib;C:\Python311\Lib\site-packages";FILE=C:\Scripts\Report.py'
Write-Host '  3. Check the installation at any time: .\scripts\Verify-Installation.ps1'
Write-Host '  4. Uninstall: .\scripts\uninstall.ps1'
