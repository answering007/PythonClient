<#
.SYNOPSIS
    Builds the end-user distribution ZIP: payload, install/verify/uninstall wrappers, samples and checksums.

.DESCRIPTION
    Produces in the output folder (<repository root>\artifacts\dist by default):
      - Pike.PythonClient64-<version>\        staged layout (lib, scripts, samples, wrappers, README.txt, LICENSE);
      - Pike.PythonClient64-<version>.zip     the archive a user downloads from the GitHub release;
      - Pike.PythonClient64-<version>.zip.sha256
    The version is taken from Directory.Build.props (single source of truth), the payload comes from pack.ps1.

.PARAMETER Configuration
    Build configuration. Default is Release

.PARAMETER OutputPath
    Output folder. Default is <repository root>\artifacts\dist

.PARAMETER SkipBuild
    Do not run pack.ps1 (the artifacts\Pike.PythonClient64 payload must exist already)

.EXAMPLE
    .\scripts\build-distribution.ps1

.EXAMPLE
    .\scripts\build-distribution.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputPath,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $root 'artifacts\dist'
}

# The product version is defined once in Directory.Build.props
[xml]$buildProperties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props')
$version = $buildProperties.Project.PropertyGroup.Version |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Version is not found in Directory.Build.props"
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'pack.ps1') -Configuration $Configuration -OutputPath (Join-Path $root 'artifacts')
}

$payloadSource = Join-Path $root 'artifacts\Pike.PythonClient64'
if (-not (Test-Path -LiteralPath $payloadSource)) {
    throw "Deploy folder '$payloadSource' is not found. Run scripts\pack.ps1 first (or omit -SkipBuild)"
}

$name = "Pike.PythonClient64-$version"
$stagePath = Join-Path $OutputPath $name
$libPath = Join-Path $stagePath 'lib'
$scriptsPath = Join-Path $stagePath 'scripts'
$samplesPath = Join-Path $stagePath 'samples\power-query'

if (Test-Path -LiteralPath $stagePath) {
    Remove-Item -LiteralPath $stagePath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $libPath, $scriptsPath, $samplesPath | Out-Null

# Payload: the assemblies installed by scripts\install.ps1
Get-ChildItem -LiteralPath $payloadSource -File | Copy-Item -Destination $libPath -Force

# Installation scripts (install.cmd -> scripts\install.ps1 with -SourcePath "..\lib"); Installation.Common.ps1 is
# dot-sourced by all three scripts and carries the GAC lookup / legacy folder helpers
foreach ($script in @('install.ps1', 'uninstall.ps1', 'Verify-Installation.ps1', 'Installation.Common.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination $scriptsPath -Force
}

# .cmd wrappers and README.txt: elevation, Unblock-File, -ExecutionPolicy Bypass
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'dist') -File | Copy-Item -Destination $stagePath -Force

# README.txt: the version placeholder is replaced, the file stays ASCII/UTF-8 (no BOM)
$readmePath = Join-Path $stagePath 'README.txt'
if (Test-Path -LiteralPath $readmePath) {
    $readme = (Get-Content -LiteralPath $readmePath -Raw).Replace('{{VERSION}}', $version)
    [IO.File]::WriteAllText($readmePath, $readme, (New-Object Text.UTF8Encoding($false)))
}

Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $stagePath -Force

# Ready-made Power Query workbooks: real usage examples, the PYTHONDLL path is edited in Excel
$excelSamples = Join-Path $root 'Pike.PythonClient.Test\Excel'
if (Test-Path -LiteralPath $excelSamples) {
    Get-ChildItem -LiteralPath $excelSamples -Filter 'PowerQuery - *.xlsx' -File |
        Copy-Item -Destination $samplesPath -Force

    $samplesReadme = @'
Power Query / Excel examples
============================

Open a workbook, then edit the "Settings" table:
  - "Path to python dll" - full path to your python3xx.dll (x64);
  - "Python env path"    - your virtual environment (site-packages are added to sys.path);
  - "Python script file" - full path to the script (see Pike.PythonClient.Test\Python for the sources).

The provider must be installed first (install.cmd in the archive root) and Excel must be 64-bit.
'@
    [IO.File]::WriteAllText((Join-Path $samplesPath 'README.txt'), ($samplesReadme -replace "`r?`n", "`r`n") + "`r`n",
        (New-Object Text.UTF8Encoding($false)))
}

# SHA256 of every installed file: lets a user (and support) verify the payload before the installation
$checksums = foreach ($file in Get-ChildItem -LiteralPath $libPath -File | Sort-Object Name) {
    '{0}  lib/{1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash, $file.Name
}
[IO.File]::WriteAllText((Join-Path $stagePath 'SHA256SUMS.txt'), (($checksums -join "`r`n") + "`r`n"),
    (New-Object Text.UTF8Encoding($false)))

# Archive: the user extracts it and runs install.cmd
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
$zipPath = Join-Path $OutputPath "$name.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -LiteralPath $stagePath -DestinationPath $zipPath -CompressionLevel Optimal

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText("$zipPath.sha256", "$zipHash  $name.zip`r`n", (New-Object Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "Distribution '$name' is ready:" -ForegroundColor Green
Get-ChildItem -LiteralPath $zipPath, "$zipPath.sha256" | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "SHA256: $zipHash"
Write-Host ''
Write-Host 'Staged layout:' -ForegroundColor Cyan
Get-ChildItem -LiteralPath $stagePath -Recurse -File |
    ForEach-Object { '  ' + $_.FullName.Substring($stagePath.Length + 1) }
