<#
.SYNOPSIS
    Installs the Pike.PythonClient64 ADO.NET provider (x64): copies the files, publishes the assemblies into the GAC
    and registers the provider in machine.config.

.DESCRIPTION
    The provider and its pythonnet dependency are strong-named, so they are installed into the global assembly cache:
    machine.config contains the fully qualified type name (Version/Culture/PublicKeyToken), and the runtime resolves
    an assembly outside the host application base (Power Query / Excel) only by a full name. Python.Runtime.dll must
    be in the GAC as well, because the dependencies of a GAC assembly are probed in the GAC, not in the install folder.
    The GAC is updated through System.EnterpriseServices.Internal.Publish, so no gacutil / Windows SDK is required.
    The registration is done by Pike.PythonClient64.Install.dll (DataProviderInstallationBase), which is a
    separate assembly: the runtime provider loaded by Excel/Power Query has no installer dependency.
    machine.config is backed up as machine.config.bak-<utc> before the change.
    Administrator rights and 64-bit Windows PowerShell 5.1 are required.

.PARAMETER InstallPath
    Target folder. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER Configuration
    Build configuration to install. Default is Release

.PARAMETER SkipMachineConfig
    Copies files and updates the GAC, machine.config is not changed

.PARAMETER SkipGac
    Copies files and updates machine.config, the GAC is not changed

.EXAMPLE
    .\scripts\install.ps1

.EXAMPLE
    .\scripts\install.ps1 -Configuration Debug -SkipMachineConfig -SkipGac
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:ProgramFiles 'Pike\PythonClient64'),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipMachineConfig,
    [switch]$SkipGac
)

$ErrorActionPreference = 'Stop'

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

# Runtime files first: the installer assembly references the provider assembly
$files = @(
    @{ Name = 'Pike.PythonClient64.dll'; Source = $runtimePath },
    @{ Name = 'Pike.PythonClient64.xml'; Source = $runtimePath },
    @{ Name = 'Python.Runtime.dll'; Source = $runtimePath },
    @{ Name = 'Python.Runtime.xml'; Source = $runtimePath },
    @{ Name = 'Pike.PythonClient64.Install.dll'; Source = $installBinPath },
    @{ Name = 'Pike.PythonClient64.Install.xml'; Source = $installBinPath }
)

# Assemblies published into the GAC: the provider and its runtime dependency.
# The install assembly is used by this script only, so it stays a plain file.
$gacFiles = @('Pike.PythonClient64.dll', 'Python.Runtime.dll')

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

try {
    if (-not $SkipGac) {
        $publish = New-GacPublisher

        foreach ($name in $gacFiles) {
            $filePath = Join-Path $InstallPath $name
            if (-not (Test-Path $filePath)) {
                Write-Verbose "Skip '$name', it is not a part of the build output"
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
            $filePath = Join-Path $InstallPath $name
            if (-not (Test-Path $filePath)) {
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
        Add-Type -Path (Join-Path $InstallPath 'Pike.PythonClient64.dll')
        Add-Type -Path (Join-Path $InstallPath 'Pike.PythonClient64.Install.dll')

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
                throw "The registered type is not resolved from the GAC (got '$location'). Check that '$InstallPath' contains the strong-named assemblies and rerun the script"
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

Write-Host "Installed to '$InstallPath'"
Write-Host 'Invariant name: Pike.PythonClient64.Data.PythonProviderFactory'
