<#
.SYNOPSIS
    Runs the provider smoke checks that do not require Python.

.DESCRIPTION
    Loads the built Pike.PythonClient64 assembly and validates:
      - connection string parsing (value case, quoting, keywords, validation);
      - KeyIndexCollection ordering and editing;
      - PythonParameterCollection contract (CopyTo, SetParameter, Insert, object overloads);
      - PythonParameter contract (null/DBNull, DbType, Direction, renaming);
      - column converters (datetime64/timedelta64 units, NaT, NaN, null);
      - SupportedTypes dtype registry;
      - machine.config paths.

    The script is a quick sanity check between test runs: the full coverage lives in the test project.

.PARAMETER Configuration
    Build configuration to check. Default is Debug

.EXAMPLE
    .\scripts\smoke\Test-ProviderSmoke.ps1

.EXAMPLE
    .\scripts\smoke\Test-ProviderSmoke.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assemblyPath = Join-Path $root "Pike.PythonClient64\bin\$Configuration\net48\Pike.PythonClient64.dll"

if (-not (Test-Path $assemblyPath)) {
    throw "Assembly '$assemblyPath' is not found, build the solution first: dotnet build PythonClient.sln -c $Configuration"
}

Add-Type -Path $assemblyPath

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

function New-Parameter([string]$name, $value) {
    $parameter = New-Object Pike.PythonClient64.Data.PythonParameter
    $parameter.ParameterName = $name
    $parameter.Value = $value
    return $parameter
}

function Get-ErrorType($errorRecord) {
    $exception = $errorRecord.Exception
    while ($exception.InnerException) { $exception = $exception.InnerException }

    return $exception.GetType().Name
}

Write-Host ''
Write-Host 'Connection string' -ForegroundColor Cyan
$connectionString = 'PYTHONDLL=C:\Py\python311.dll;PYTHONPATH="C:\Py\Lib;C:\Py\Lib\site-packages";FILE=C:\Scripts\Test.py'
$builder = [Pike.PythonClient64.Data.PythonConnectionStringBuilder]::Parse($connectionString)

Assert-That 'value case is preserved' { $builder.PythonDll -ceq 'C:\Py\python311.dll' }
Assert-That 'quoted PYTHONPATH is parsed' { $builder.PythonPath -ceq 'C:\Py\Lib;C:\Py\Lib\site-packages' }
Assert-That 'path components are split' { $builder.PythonPathComponents.Length -eq 2 }
Assert-That 'keywords are case-insensitive' { $builder['file'] -ceq $builder['FILE'] }
Assert-That 'round trip keeps values' {
    ([Pike.PythonClient64.Data.PythonConnectionStringBuilder]::Parse($builder.ConnectionString)).File -ceq $builder.File
}
Assert-That 'unknown keyword is rejected' {
    try { $builder['UNKNOWN'] = '1'; $false } catch { (Get-ErrorType $_) -eq 'KeyNotFoundException' }
}
Assert-That 'empty connection string means no settings' {
    ([Pike.PythonClient64.Data.PythonConnectionStringBuilder]::Parse('')).Count -eq 0
}
Assert-That 'PYTHONDLL is required' {
    try { (New-Object Pike.PythonClient64.Data.PythonConnectionStringBuilder).Validate(); $false }
    catch { (Get-ErrorType $_) -eq 'InvalidOperationException' }
}

Write-Host ''
Write-Host 'KeyIndexCollection' -ForegroundColor Cyan
$collection = New-Object 'Pike.PythonClient64.KeyIndexCollection[string,string]'
$collection.Add('a', '1'); $collection.Add('b', '2'); $collection.Add('c', '3')
$collection.Set('b', '22')
$collection.Insert(0, 'z', '0')
$collection.Insert(1, 'c', '33')
$collection.RenameKey('a', 'alpha')

Assert-That 'order is preserved' { (($collection.Keys) -join ',') -eq 'z,c,alpha,b' }
Assert-That 'values follow keys' { (($collection.Values) -join ',') -eq '0,33,1,22' }
Assert-That 'TryGetValue reports missing key' { -not $collection.TryGetValue('missing', [ref]$null) }
Assert-That 'Remove of missing key is ignored' { $collection.Remove('missing'); $collection.Count -eq 4 }


Write-Host ''
Write-Host 'PythonParameterCollection' -ForegroundColor Cyan
$parameters = New-Object Pike.PythonClient64.Data.PythonParameterCollection
$null = $parameters.Add((New-Parameter 'a' 1))
$null = $parameters.Add((New-Parameter 'b' 2))
$null = $parameters.Add((New-Parameter 'c' 3))
$parameters['b'] = New-Parameter 'b' 22
$parameters.Insert(1, (New-Parameter 'z' 0))
$target = New-Object 'object[]' 6
$parameters.CopyTo($target, 2)

Assert-That 'SetParameter replaces the value' { $parameters['b'].Value -eq 22 }
Assert-That 'SetParameter keeps the count' { $parameters.Count -eq 4 }
Assert-That 'Insert places the parameter at the index' { $parameters.IndexOf('z') -eq 1 }
Assert-That 'CopyTo writes items from the index' {
    $names = @()
    foreach ($item in $target) {
        if ($item -is [Pike.PythonClient64.Data.PythonParameter]) { $names += $item.ParameterName }
    }
    ($names -join ',') -eq 'a,z,b,c'
}
Assert-That 'Contains(object) is safe' { -not $parameters.Contains([object]'not a parameter') }
Assert-That 'IndexOf(object) is safe' { $parameters.IndexOf([object]'not a parameter') -eq -1 }
Assert-That 'enumerator returns parameters' {
    (@($parameters | ForEach-Object { $_.GetType().Name }) | Select-Object -Unique) -eq 'PythonParameter'
}
Assert-That 'IsSynchronized is false' { -not $parameters.IsSynchronized }

Write-Host ''
Write-Host 'PythonParameter' -ForegroundColor Cyan
$parameter = New-Object Pike.PythonClient64.Data.PythonParameter
$parameter.ParameterName = 'x'
$parameter.Value = $null
$nullValue = $parameter.Value

Assert-That 'null value is allowed' { $null -eq $nullValue -and $parameter.DbType -eq [System.Data.DbType]::String }
$parameter.Value = [System.DBNull]::Value
$parameter.Value = 'Pike'
Assert-That 'string size is the length' { $parameter.Size -eq 4 }
Assert-That 'DbType can be set' {
    $parameter.DbType = [System.Data.DbType]::Decimal
    $parameter.DbType -eq [System.Data.DbType]::Decimal
}
Assert-That 'Direction is validated' {
    try { $parameter.Direction = [System.Data.ParameterDirection]::Output; $false }
    catch { (Get-ErrorType $_) -eq 'NotSupportedException' }
}
Assert-That 'ParameterName is renameable' {
    $parameter.ParameterName = 'y'
    $parameter.ParameterName -eq 'y'
}
Assert-That 'IsNullable defaults to true' { (New-Object Pike.PythonClient64.Data.PythonParameter).IsNullable }

Write-Host ''
Write-Host 'Column converters' -ForegroundColor Cyan
$dateTimeConverter = New-Object Pike.PythonClient64.ColumnConverters.DateTimeConverter
$dateTimes = $dateTimeConverter.ConvertValues([object[]]@(0L, [long]1600000000000000000, [long]-315619200000000000, [long]::MinValue, $null))
Assert-That 'epoch is converted' { $dateTimes[0] -eq [datetime]'1970-01-01' }
Assert-That 'nanoseconds are converted' { $dateTimes[1] -eq [datetime]'2020-09-13 12:26:40' }
Assert-That 'dates before 1970 are supported' { $dateTimes[2] -eq [datetime]'1960-01-01' }
Assert-That 'NaT and null become DBNull' { $dateTimes[3] -eq [System.DBNull]::Value -and $dateTimes[4] -eq [System.DBNull]::Value }

$timeSpanConverter = New-Object Pike.PythonClient64.ColumnConverters.TimeSpanConverter
$timeSpans = $timeSpanConverter.ConvertValues([object[]]@([long]3600000000000, [long]::MinValue))
Assert-That 'timedelta is converted' { $timeSpans[0] -eq [timespan]::FromHours(1) }
Assert-That 'NaT timedelta becomes DBNull' { $timeSpans[1] -eq [System.DBNull]::Value }

$doubleConverter = New-Object Pike.PythonClient64.ColumnConverters.DoubleConverter
$doubles = $doubleConverter.ConvertValues([object[]]@(1.5, [double]::NaN, $null))
Assert-That 'NaN becomes DBNull' { $doubles[1] -eq [System.DBNull]::Value -and $doubles[0] -eq 1.5 }

$integerConverter = New-Object Pike.PythonClient64.ColumnConverters.IntegerConverter
$integers = $integerConverter.ConvertValues([object[]]@(1, [long]::MaxValue, $null))
Assert-That 'out of range int becomes DBNull' { $integers[1] -eq [System.DBNull]::Value -and $integers[0] -eq 1 }

Write-Host ''
Write-Host 'SupportedTypes' -ForegroundColor Cyan
Assert-That 'dtype parameters are removed' { [Pike.PythonClient64.SupportedTypes]::NormalizeDtype('datetime64[ns, UTC]') -eq 'datetime64' }
Assert-That 'nullable Int64 is supported' {
    $converter = $null
    [Pike.PythonClient64.SupportedTypes]::TryGetConverter('Int64', [ref]$converter) -and
        $converter -is [Pike.PythonClient64.ColumnConverters.LongConverter]
}
Assert-That 'unknown dtype is reported' {
    try { [Pike.PythonClient64.SupportedTypes]::GetConverter('complex128'); $false }
    catch { (Get-ErrorType $_) -eq 'NotSupportedException' }
}

Write-Host ''
Write-Host 'machine.config paths' -ForegroundColor Cyan
$installAssemblyPath = Join-Path $root "Pike.PythonClient64.Install\bin\$Configuration\Pike.PythonClient64.Install.dll"

if (-not (Test-Path $installAssemblyPath)) {
    Write-Host "  skip  '$installAssemblyPath' is not built, build the solution first" -ForegroundColor Yellow
}
else {
    Add-Type -Path $installAssemblyPath

    $machine64 = [Pike.PythonClient64.Install.DbProviderInstallation]::Machine64Config
    $machine32 = [Pike.PythonClient64.Install.DbProviderInstallation]::Machine32Config

    Assert-That 'x64 path points to Framework64' { $machine64.Replace('\', '/').Contains('/Framework64/') }
    Assert-That 'x32 path points to Framework' { $machine32.Replace('\', '/').Contains('/Framework/') }
    Assert-That 'x64 machine.config exists' { Test-Path $machine64 }
    Assert-That 'runtime assembly has no installer dependency' {
        $references = ([System.Reflection.Assembly]::LoadFrom($assemblyPath)).GetReferencedAssemblies() | ForEach-Object { $_.Name }
        -not ($references -contains 'System.Configuration.Install')
    }
}

Write-Host ''
if ($script:failures.Count -gt 0) {
    Write-Host ("FAILED: {0} of {1} checks" -f $script:failures.Count, $script:checks) -ForegroundColor Red
    $script:failures | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host ("All {0} smoke checks passed" -f $script:checks) -ForegroundColor Green
