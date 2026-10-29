# Connection strings

## Keys

| Key | Required | Value |
|---|---|---|
| `PYTHONDLL` | yes | full path to `python3xx.dll` (x64) |
| `PYTHONPATH` | no | additional `sys.path` entries separated by `;` |
| `FILE` | no | full path to the Python script file |

Keywords are case-insensitive, values are case-sensitive. Unsupported keyword raises `KeyNotFoundException`,
malformed string raises `ArgumentException`.

## Quoting

`DbConnectionStringBuilder` splits the string by `;`, so a value that contains `;` must be quoted.
`PythonConnectionStringBuilder.ConnectionString` quotes values automatically:

```csharp
var builder = new PythonConnectionStringBuilder
{
    PythonDll = @"C:\Python311\python311.dll",
    PythonPath = @"C:\Python311\Lib;C:\Python311\Lib\site-packages",
    File = @"C:\Scripts\Report.py"
};

// PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH="C:\Python311\Lib;C:\Python311\Lib\site-packages";FILE=C:\Scripts\Report.py
var connectionString = builder.ConnectionString;
```

Power Query (M):

```fsharp
"PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH=""C:\Python311\Lib;C:\Python311\Lib\site-packages"";FILE=C:\Scripts\Report.py"
```

## Examples

Script file:

```
PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH="C:\Python311\Lib;C:\Python311\Lib\site-packages";FILE=C:\Scripts\Report.py
```

Command text as a script (no `FILE`):

```
PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH="C:\Python311\Lib;C:\Python311\Lib\site-packages"
```

Anaconda:

```
PYTHONDLL=C:\Users\User\anaconda3\python311.dll;PYTHONPATH="C:\Users\User\anaconda3\Lib;C:\Users\User\anaconda3\Lib\site-packages;C:\Users\User\anaconda3\DLLs;C:\Users\User\anaconda3\Library\bin"
```

## Defaults

`PythonDataProvider.DefaultPathComponents` returns `Lib`, `DLLs`, `Lib\site-packages`, `Library\bin` next to
`python3xx.dll` plus the entries of the `PYTHONHOME` and `PYTHONPATH` environment variables. Non-existed directories
are filtered out, duplicates are removed. The components are inserted at the beginning of `sys.path`.

## Validation

- `PYTHONDLL` is required: `PythonConnection.Open()` raises `InvalidOperationException` without it.
- The file must exist: otherwise `FileNotFoundException` is raised.
- The engine is a singleton: a connection with another `PYTHONDLL` raises `InvalidOperationException` while the
  engine is initialized.
- Empty value means "not set"; empty connection string means "no settings".
