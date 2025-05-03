# PythonClient
Ado.net x64 provider that uses python script as the datasource. This project is based on [pythonnet](https://github.com/pythonnet/pythonnet)

## Table of content
<!--TOC-->
  - [Quik example](#quik-example)
    - [C#](#c)
    - [Python script](#python-script)
  - [Python script text via command text](#python-script-text-via-command-text)
  - [Power Query](#power-query)
  - [Examples](#examples)
  - [Supported DataFrame column types](#supported-dataframe-column-types)
  - [Supported parameter types](#supported-parameter-types)
  - [Installation](#installation)
<!--/TOC-->

## Quik example
### C#
```C#
//Python script file for test
const string fileName = @"TestScript01.py";
var scriptFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsMain.Default.PythonScriptsFolder, fileName));
if (!scriptFile.Exists) throw new FileNotFoundException("Where is the script?", scriptFile.FullName);

//Setup python environment
var pythonDll = new FileInfo(SettingsMain.Default.PythonDllPath);
if (!pythonDll.Exists) throw new FileNotFoundException("Can't find python*.dll file", pythonDll.FullName);
if (pythonDll.DirectoryName == null)
    throw new DirectoryNotFoundException("Directory name of python*.dll file can't be null");

//Compose PATH variable
var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
var packages = Path.Combine(lib, "site-packages");
var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");

//Create connection string
var stringBuilder = new PythonConnectionStringBuilder
{
    File = scriptFile.FullName,
    PythonDll = pythonDll.FullName,
    PythonPath = string.Join(";", lib, dlls, packages, libraryBin)
};

var datatable = new DataTable();
using (var connection = new PythonConnection())
{
    connection.ConnectionString = stringBuilder.ConnectionString;
    connection.Open();
    using (var command = connection.CreateCommand())
    {
        //Set query command text. It will be passed to python "query" variable
        command.CommandText = "Hello from ADO.Net!";

        //Set query parameters. It will be passed to python "params" variable
        command.Parameters.Add(new PythonParameter { ParameterName = "bool", Value = true });
        command.Parameters.Add(new PythonParameter { ParameterName = "dt", Value = DateTime.Today });
        command.Parameters.Add(new PythonParameter { ParameterName = "double", Value = 1235.0 });
        command.Parameters.Add(new PythonParameter { ParameterName = "int", Value = 789 });
        command.Parameters.Add(new PythonParameter { ParameterName = "long", Value = 1024L });
        command.Parameters.Add(new PythonParameter { ParameterName = "string", Value = "String parameter" });

        /*
            * Python script must have "result" variable of type pandas DataFrame.
            * This variable data will be transferred to DbDataReader
            */
        using (var reader = command.ExecuteReader())
            datatable.Load(reader);
    }
}
```
### Python script
```Python
"""TestScript01."""
import numpy as np
import pandas as pd

query_text = globals()['query'] if 'query' in globals() else None
query_params = globals()['params'] if 'params' in globals() else None

result = pd.DataFrame({
    'StringColumn':		['Pike',	None,	'Amol'],
    'BoolColumn':		[True,		True,	False],
    'FloatColumn':		[123.456,	np.nan,	456.789],
    'IntColumn':		[123456,	456789,	789123],
    'TimeDeltaColumn':	[np.timedelta64(10, 'h'), None, np.timedelta64(12, 'h')],
    'DateTimeColumn':	[np.datetime64(30, 'Y'), None, np.datetime64(50, 'Y')]
})
```
## Python script text via command text
It is possible to use [DbCommand.CommandText](https://docs.microsoft.com/en-us/dotnet/api/system.data.common.dbcommand.commandtext?view=netframework-4.6.1) to set python script text (instead of py file):
```C#
//Python script text
const string scriptText = @"import pandas as pd

query_text = globals()['query'] if 'query' in globals() else None
query_params = globals()['params'] if 'params' in globals() else None

result = pd.DataFrame(
[[True, 99.0],
[True, 56.1],
[False, 73.2],
[False, 69.3]])";

//Setup python environment
var pythonDll = new FileInfo(SettingsMain.Default.PythonDllPath);
if (!pythonDll.Exists) throw new FileNotFoundException("Can't find python*.dll file", pythonDll.FullName);
if (pythonDll.DirectoryName == null)
    throw new DirectoryNotFoundException("Directory name of python*.dll file can't be null");

//Compose PATH variable
var lib = Path.Combine(pythonDll.DirectoryName, "Lib");
var dlls = Path.Combine(pythonDll.DirectoryName, "DLLs");
var packages = Path.Combine(lib, "site-packages");
var libraryBin = Path.Combine(pythonDll.DirectoryName, "Library", "bin");

//Create connection string
var stringBuilder = new PythonConnectionStringBuilder
{
    PythonDll = pythonDll.FullName,
    PythonPath = string.Join(";", lib, dlls, packages, libraryBin)
};

var datatable = new DataTable();
using (var connection = new PythonConnection())
{
    connection.ConnectionString = stringBuilder.ConnectionString;
    connection.Open();
    using (var command = connection.CreateCommand())
    {
        //In this case there is no "query" global variable in python
        command.CommandText = scriptText;

        //Set query parameters. It will be passed to python "params" variable
        command.Parameters.Add(new PythonParameter { ParameterName = "bool", Value = true });
        command.Parameters.Add(new PythonParameter { ParameterName = "dt", Value = DateTime.Today });
        command.Parameters.Add(new PythonParameter { ParameterName = "double", Value = 1235.0 });
        command.Parameters.Add(new PythonParameter { ParameterName = "int", Value = 789 });
        command.Parameters.Add(new PythonParameter { ParameterName = "long", Value = 1024L });
        command.Parameters.Add(new PythonParameter { ParameterName = "string", Value = "String parameter" });

        /*
            * Python script must have "result" variable of type pandas DataFrame.
            * This variable data will be transferred to DbDataReader
            */
        using (var reader = command.ExecuteReader())
            datatable.Load(reader);
    }
}
```
## Power Query
[Power Query M formula language](https://docs.microsoft.com/en-us/powerquery-m/) supports [AdoDotNet.Query](https://docs.microsoft.com/en-us/powerquery-m/adodotnet-query) and it's possible to use PythonClient as a datasource:
```F#
let
    query = AdoDotNet.Query(
        "Pike.PythonClient64.Data.PythonProviderFactory", 
        ConnectionString, 
        "Hello From Power Query!")
in
    query
```
and even pass Excel table using query text:
1. Commpress table data (Source) in Power Query:
```F#
let
    JsonSource = Json.FromValue(Source, TextEncoding.Utf8),
    CompressedValue = Binary.Compress(JsonSource, Compression.GZip),
    ToText = Binary.ToText(CompressedValue, BinaryEncoding.Base64)
in
    ToText
```
2. Transfer compressed data to script:
```F#
query = AdoDotNet.Query(
    "Pike.PythonClient64.Data.PythonProviderFactory", 
    ConnectionString, 
    QueryText)
```
3. Decode data in python script:
```Python
def decode_table(text: str)->pd.DataFrame:
    """
    Decode a base64 encoded string, decompress it using gzip,
    and then load it into a pandas DataFrame using json.loads.
    
    Parameters
    ----------
    text : str
        The string to be decoded.
    
    Returns
    -------
    pd.DataFrame
        The DataFrame containing the data from the string.
    """
    decoded = base64.b64decode(text)
    raw_data = gzip.decompress(decoded)
    json_data = json.loads(raw_data)
    return pd.DataFrame(json_data)
```
## Examples
* C# examples can be found [HERE](https://github.com/answering007/PythonClient/tree/master/Pike.PythonClient.Test/Tests)
* Excel examples can be found [HERE](https://github.com/answering007/PythonClient/tree/master/Pike.PythonClient.Test/Excel)

## Supported DataFrame column types
| Python | .NET |
| ----------- | ----------- |
| bool | bool |
| datetime64[ns] | DateTime |
| float64 | double |
| int32 | int |
| int64 | long |
| object | string |
| timedelta64[ns] | TimeSpan |

## Supported parameter types
| .NET | Python |
| ----------- | ----------- |
| bool | bool |
| DateTime | datetime64[ns] |
| double | float64 |
| int | int32 |
| long | int64 |
| string | object |

## Installation

1. Download latest [release](https://github.com/answering007/PythonClient/releases)
2. Extract files
3. Run **setup.exe** as administrator