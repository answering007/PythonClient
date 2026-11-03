<#
.SYNOPSIS
    Shared helpers for the installation scripts: GAC lookup and the legacy install folder.

.DESCRIPTION
    Dot-sourced by install.ps1, uninstall.ps1 and Verify-Installation.ps1; the distribution ZIP ships it in scripts\.
    The provider is installed into the global assembly cache only (no copy in %ProgramFiles%), so the uninstall
    resolves the published assemblies by their GAC folders instead of a known installation path.
#>

# Assemblies published into the GAC. $script:GacAssemblyFiles keeps the order used by the installation output.
$script:ProviderAssemblyName = 'Pike.PythonClient64'
$script:InstallerAssemblyName = 'Pike.PythonClient64.Install'
$script:PythonRuntimeAssemblyName = 'Python.Runtime'

# Legacy 2.1.0 layout: the assemblies were copied here; the current installation publishes them into the GAC only
$script:LegacyInstallPath = Join-Path $env:ProgramFiles 'Pike\PythonClient64'

# Files the provider may have left in the legacy folder (and only they are removed there)
$script:InstalledFileNames = @(
    'Pike.PythonClient64.dll',
    'Pike.PythonClient64.xml',
    'Pike.PythonClient64.pdb',
    'Python.Runtime.dll',
    'Python.Runtime.xml',
    'Pike.PythonClient64.Install.dll',
    'Pike.PythonClient64.Install.xml'
)

<#
.SYNOPSIS
    Finds the published assembly in the GAC.

.DESCRIPTION
    A GAC folder is <GAC_64|GAC_MSIL>\<name>\v4.0_<version>__<public key token>\<name>.dll, so the assembly does not
    need to be loaded to get its path. The x64 and the MSIL roots are both checked.

.PARAMETER Name
    Simple assembly name, e.g. Pike.PythonClient64

.PARAMETER FileName
    File name inside the version folder. Default is <Name>.dll

.OUTPUTS
    System.String — full path to the GAC file or $null when the assembly is not published.
#>
function Get-GacAssemblyPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [string]$FileName
    )

    if ([string]::IsNullOrWhiteSpace($FileName)) { $FileName = "$Name.dll" }

    $roots = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_64'),
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL')
    )

    foreach ($root in $roots) {
        $versionRoot = Join-Path $root $Name
        if (-not (Test-Path -LiteralPath $versionRoot)) { continue }

        $candidate = Get-ChildItem -LiteralPath $versionRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object -Property Name -Descending |
            ForEach-Object { Join-Path $_.FullName $FileName } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1

        if ($candidate) { return $candidate }
    }

    return $null
}

<#
.SYNOPSIS
    Returns the GAC paths of the three published assemblies.

.OUTPUTS
    Hashtable with Provider, Installer and PythonRuntime keys (a missing assembly maps to $null).
#>
function Get-InstalledAssemblyPaths {
    return [ordered]@{
        Provider      = Get-GacAssemblyPath -Name $script:ProviderAssemblyName
        Installer     = Get-GacAssemblyPath -Name $script:InstallerAssemblyName
        PythonRuntime = Get-GacAssemblyPath -Name $script:PythonRuntimeAssemblyName
    }
}

<#
.SYNOPSIS
    Checks whether the provider record exists in machine.config.

.DESCRIPTION
    Read-only check used by uninstall.ps1: when the record and the GAC entries are absent, there is nothing to remove
    and the distribution payload is not needed.

.PARAMETER MachineConfig
    Full path to machine.config

.PARAMETER Invariant
    Provider invariant name

.OUTPUTS
    System.Boolean
#>
function Test-ProviderRecord {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$MachineConfig,
        [Parameter(Mandatory = $true)][string]$Invariant
    )

    if (-not (Test-Path -LiteralPath $MachineConfig)) { return $false }

    $xml = New-Object Xml.XmlDocument
    $xml.Load($MachineConfig)
    $node = $xml.SelectSingleNode("/configuration/system.data/DbProviderFactories/add[@invariant='$Invariant']")

    return $null -ne $node
}

<#
.SYNOPSIS
    Creates the DbProviderInstallation instance from the published installer assembly.

.DESCRIPTION
    The assembly is loaded by its identity, so it is taken from the GAC (the installation publishes it there).
    The provider dependency resolves from the GAC as well.

.PARAMETER AssemblyPath
    Full path to Pike.PythonClient64.Install.dll (GAC path or the distribution payload).

.OUTPUTS
    Pike.PythonClient64.Install.DbProviderInstallation instance.
#>
function New-DbProviderInstallation {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$AssemblyPath
    )

    if (-not (Test-Path -LiteralPath $AssemblyPath)) {
        throw "Installer assembly '$AssemblyPath' is not found"
    }

    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($AssemblyPath).FullName
    $assembly = [Reflection.Assembly]::Load($assemblyName)
    $type = $assembly.GetType('Pike.PythonClient64.Install.DbProviderInstallation', $true)

    return [Activator]::CreateInstance($type)
}

<#
.SYNOPSIS
    Removes the legacy install folder of the 2.1.0 development builds (and an optional -KeepFiles folder).

.DESCRIPTION
    Only the known provider files are deleted; a folder that still contains foreign files is left in place with a
    warning, and an empty folder is removed. New installations publish the assemblies into the GAC only.

.PARAMETER Path
    Folder to clean. Default is %ProgramFiles%\Pike\PythonClient64

.PARAMETER Quiet
    Do not print a message when the folder does not exist
#>
function Remove-LegacyInstallFolder {
    [CmdletBinding()]
    param(
        [string]$Path = $script:LegacyInstallPath,
        [switch]$Quiet
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        if (-not $Quiet) { Write-Verbose "Nothing to clean in '$Path'" }
        return
    }

    $removed = 0
    foreach ($name in $script:InstalledFileNames) {
        $file = Join-Path $Path $name
        if (-not (Test-Path -LiteralPath $file)) { continue }

        Remove-Item -LiteralPath $file -Force
        $removed++
    }

    $rest = @(Get-ChildItem -LiteralPath $Path -Force)
    if ($rest.Count -eq 0) {
        Remove-Item -LiteralPath $Path -Force
        Write-Host "Removed the install folder '$Path'"
        return
    }

    if ($removed -gt 0) { Write-Host "Removed $removed provider file(s) from '$Path'" }
    Write-Warning "Folder '$Path' contains files that are not part of the provider ($($rest.Name -join ', ')); it is left in place"
}
