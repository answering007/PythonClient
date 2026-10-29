<#
.SYNOPSIS
    Builds the Release artifacts and creates the NuGet package.

.DESCRIPTION
    Produces in the output folder (<repository root>\artifacts by default):
      - Pike.PythonClient64.<version>.nupkg and .snupkg (symbols);
      - Pike.PythonClient64\*.dll|*.xml|*.pdb — a flat folder for install.ps1 / manual deployment.

.PARAMETER Configuration
    Build configuration. Default is Release

.PARAMETER OutputPath
    Output folder. Default is <repository root>\artifacts

.EXAMPLE
    .\scripts\pack.ps1

.EXAMPLE
    .\scripts\pack.ps1 -OutputPath D:\build\drop
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $root 'artifacts'
}

$project = Join-Path $root 'Pike.PythonClient64\Pike.PythonClient64.csproj'
$binPath = Join-Path $root "Pike.PythonClient64\bin\$Configuration"

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

Write-Host "Packing $project ($Configuration)..."
& dotnet pack $project -c $Configuration -o $OutputPath --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE" }

# Flat folder used by scripts\install.ps1 and for manual deployment (Power Query / Excel)
$deployPath = Join-Path $OutputPath 'Pike.PythonClient64'
New-Item -ItemType Directory -Force -Path $deployPath | Out-Null

foreach ($file in @('Pike.PythonClient64.dll', 'Pike.PythonClient64.xml', 'Pike.PythonClient64.pdb', 'Python.Runtime.dll')) {
    $source = Join-Path $binPath $file
    if (-not (Test-Path $source)) {
        Write-Verbose "Skip '$file', it is not a part of the build output"
        continue
    }

    Copy-Item -LiteralPath $source -Destination $deployPath -Force
}

Write-Host ''
Write-Host "Artifacts in '$OutputPath':"
Get-ChildItem -LiteralPath $OutputPath | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
Get-ChildItem -LiteralPath $deployPath | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
