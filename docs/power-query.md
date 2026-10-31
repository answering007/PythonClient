# Power Query / Excel

## Prerequisites

1. 64-bit Excel (the provider and Python are x64-only, 32-bit Excel cannot load it).
2. Python 3.7–3.13 x64 with `numpy` and `pandas`.
3. The provider is installed into the GAC and registered in `machine.config` (elevated 64-bit Windows PowerShell 5.1):

```powershell
dotnet build PythonClient.sln -c Release
.\scripts\install.ps1
```

4. Restart Excel so the process reads the updated `machine.config`.

`install.ps1` puts `Pike.PythonClient64.dll` and `Python.Runtime.dll` into `GAC_64` and writes the fully qualified type
name into `machine.config`. Both steps are required: Excel probes neither the GAC without a full name nor the
`%ProgramFiles%\Pike\PythonClient64` folder.

## Basic query

```fsharp
let
    ConnectionString = "PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH=""C:\Python311\Lib;C:\Python311\Lib\site-packages""",
    Query = "import pandas as pd
result = pd.DataFrame({'Value': [1, 2, 3]})",
    Source = AdoDotNet.Query("Pike.PythonClient64.Data.PythonProviderFactory", ConnectionString, Query)
in
    Source
```

`AdoDotNet.Query` passes the third argument to the provider as the command text: it is executed as a script and is
also available inside the script as the `query` variable.

## Passing an Excel table to the script

1. Compress the table data in Power Query:

```fsharp
let
    JsonSource = Json.FromValue(Source, TextEncoding.Utf8),
    CompressedValue = Binary.Compress(JsonSource, Compression.GZip),
    ToText = Binary.ToText(CompressedValue, BinaryEncoding.Base64)
in
    ToText
```

2. Pass the text as the query:

```fsharp
AdoDotNet.Query(
    "Pike.PythonClient64.Data.PythonProviderFactory",
    ConnectionString,
    QueryText)
```

3. Decode it in the script:

```python
import base64
import gzip
import json
import pandas as pd

def decode_table(text: str) -> pd.DataFrame:
    decoded = base64.b64decode(text)
    raw_data = gzip.decompress(decoded)
    json_data = json.loads(raw_data)
    return pd.DataFrame(json_data)

result = decode_table(globals()['query'])
```

The same payload can be decoded into polars, without pandas:

```python
import base64
import gzip
import json
import polars as pl

raw_data = gzip.decompress(base64.b64decode(globals()['query']))
result = pl.DataFrame(json.loads(raw_data))
result = result.with_columns((pl.col('Value') * 13).alias('Value'))
```

## polars result

`result` may be a `polars.DataFrame`: the provider detects the frame type and reads it directly, `to_pandas()` is not
needed. All polars temporal types (`Date`, `Datetime` in `ns`/`us`/`ms`, `Duration`, `Time`, time zone aware values),
`Categorical`/`Enum`, `Decimal`, `Binary` and `Null` are supported; `LazyFrame` must be materialized in the script
(`result = <lazy>.collect()`). See the table in `README.md`.

## Using a script file

`FILE=C:\Scripts\Report.py` in the connection string makes the provider execute the file; the command text is passed
to the script as `query` (the "Transfer table to script" workbook relies on this):

```python
import pandas as pd

query_text = globals()['query'] if 'query' in globals() else None
result = pd.DataFrame({'Query': [query_text]})
```

An empty command text means "no query": `query` is not defined, so a script that requires it raises an error.

## Notes

- The script must assign a `pandas.DataFrame` or a `polars.DataFrame` to `result`.
- The `AdoDotNet` connector sets `CommandTimeout` on the command; the provider accepts and ignores it (a python script
  cannot be interrupted reliably), so limit the execution time inside the script.
- One Python interpreter per Excel process: `PythonDataProvider.ShutdownOnLastClose = false` keeps it alive between
  refreshes (recommended for interactive scenarios).
- Errors are reported as provider exceptions and appear in the Power Query error pane; see the troubleshooting table
  in `README.md`.
- Ready-made workbooks: `Pike.PythonClient.Test\Excel\PowerQuery - 01..04`.
