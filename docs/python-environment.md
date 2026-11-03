# Python environment (uv)

The provider does not install Python: it loads an existing 64-bit `python3xx.dll` into the host process and reads
`numpy` / `pandas` (optionally `polars`) from that interpreter. This page builds such an environment from scratch with
[uv](https://docs.astral.sh/uv/) — uv, Python, a virtual environment, the packages — and shows the `PYTHONDLL` /
`PYTHONPATH` values the connection string needs.

Nothing on this page requires administrator rights; only the provider installation itself does, see `installation.md`.

## 1. Install uv

| Method | Command |
|---|---|
| WinGet | `winget install --id=astral-sh.uv -e` |
| Standalone installer | `powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 \| iex"` |
| pipx | `pipx install uv` |
| pip | `pip install uv` |

```powershell
uv --version     # uv 0.12.13 (0ebbd9274 2026-09-10 x86_64-pc-windows-msvc)
uv self update   # standalone installer only; other installs upgrade through their package manager
```

The standalone installer puts `uv.exe` into `%USERPROFILE%\.local\bin` and adds that folder to `PATH` — open a new
shell before running `uv`. Nothing is installed machine-wide, so no UAC prompt appears.

## 2. Install Python

```powershell
uv python install 3.11 3.13       # the provider supports 3.9 - 3.13
uv python list --only-installed
uv python find 3.11
uv python dir                     # C:\Users\<user>\AppData\Roaming\uv\python
```

Python does not publish official Windows binaries, so uv installs the
[python-build-standalone](https://github.com/astral-sh/python-build-standalone) builds. On 64-bit Windows the folder
name ends with `-windows-x86_64-none`, which is exactly what the x64-only provider needs. The interpreter folder
contains `python.exe`, `python3.dll`, the versioned `python3xx.dll`, `Lib` and `DLLs`:

```text
C:\Users\<user>\AppData\Roaming\uv\python\cpython-3.11-windows-x86_64-none\
  python.exe  python3.dll  python311.dll  Lib\  DLLs\
```

uv adds only a versioned shim to `PATH` (`%USERPROFILE%\.local\bin\python3.11.exe`); `--default` additionally installs
the `python` and `python3` shims. Interpreters are installed per user and the download is cached, so installing another
patch release is fast.

## 3. Create the virtual environment

```powershell
uv venv C:\PythonEnvs\pike --python 3.11
```

```text
C:\PythonEnvs\pike\
  Scripts\python.exe  Lib\site-packages\  pyvenv.cfg
```

`uv venv` downloads the requested interpreter when it is missing. Activation is optional: `uv pip` and the provider
address `python.exe` directly.

```powershell
. C:\PythonEnvs\pike\Scripts\Activate.ps1            # optional
Set-ExecutionPolicy -Scope Process Bypass            # if the activation script is blocked
uv venv C:\PythonEnvs\pike --python 3.11 --clear     # recreate an existing environment
```

## 4. Install pandas and polars

```powershell
uv pip install --python C:\PythonEnvs\pike\Scripts\python.exe numpy pandas
uv pip install --python C:\PythonEnvs\pike\Scripts\python.exe numpy pandas polars   # polars is optional
uv pip list --python C:\PythonEnvs\pike\Scripts\python.exe

& C:\PythonEnvs\pike\Scripts\python.exe -c "import pandas, polars; print(pandas.__version__, polars.__version__)"
```

pandas depends on numpy, so both are installed. Supported versions (see `README.md`):

| Package | Supported | Note |
|---|---|---|
| Python | 3.9 – 3.13 x64 | 3.7 / 3.8 are not tested |
| pandas | 1.5 – 3.1 | pandas 3 defaults to `datetime64[us]`, the provider reads the unit from the dtype |
| numpy | 1.26 – 2.x | |
| polars | 1.x / 2.x | optional: only for `polars.DataFrame` results |

Pinning works as usual, e.g. `uv pip install --python <venv>\Scripts\python.exe "pandas==2.2.3" "numpy<2.5"`.

## 5. Point the provider at the environment

A virtual environment has **no** `python3xx.dll` (`Scripts\` contains `python.exe` only), therefore:

- `PYTHONDLL` → `python3xx.dll` of the **base** interpreter the environment was created from;
- `PYTHONPATH` → `Lib\site-packages` of the **virtual environment**.

`Lib`, `DLLs`, `Lib\site-packages` and `Library\bin` next to `python3xx.dll` are added to `sys.path` automatically
(`PythonDataProvider.DefaultPathComponents`), so only the packages of the environment have to be listed:

```powershell
$venv   = 'C:\PythonEnvs\pike'
$python = Join-Path $venv 'Scripts\python.exe'
$base   = (& $python -c "import sys; print(sys.base_prefix)").Trim()
$site   = (& $python -c "import sysconfig; print(sysconfig.get_paths()['purelib'])").Trim()
$ver    = (& $python -c "import sys; print('%d%d' % sys.version_info[:2])").Trim()

"PYTHONDLL=$(Join-Path $base "python$ver.dll");PYTHONPATH=`"$site`""
```

Output for a uv-managed Python 3.11:

```text
PYTHONDLL=C:\Users\User\AppData\Roaming\uv\python\cpython-3.11-windows-x86_64-none\python311.dll;PYTHONPATH="C:\PythonEnvs\pike\Lib\site-packages"
```

Power Query (M):

```fsharp
let
    ConnectionString = "PYTHONDLL=C:\Users\User\AppData\Roaming\uv\python\cpython-3.11-windows-x86_64-none\python311.dll;PYTHONPATH=""C:\PythonEnvs\pike\Lib\site-packages""",
    Query = "import pandas as pd
result = pd.DataFrame({'pandas': [pd.__version__]})",
    Source = AdoDotNet.Query("Pike.PythonClient64.Data.PythonProviderFactory", ConnectionString, Query)
in
    Source
```

C#:

```csharp
var builder = new PythonConnectionStringBuilder
{
    PythonDll = @"C:\Users\User\AppData\Roaming\uv\python\cpython-3.11-windows-x86_64-none\python311.dll",
    PythonPath = @"C:\PythonEnvs\pike\Lib\site-packages"
};
```

The integration tests use the same pair: `scripts\test\PythonEnvironment.ps1` sets `PYTHONNET_TEST_DLL` to the base
interpreter and `PYTHONPATH` to the `site-packages` of the environment. `PYTHONPATH` accepts several folders separated
by `;`, see `connection-strings.md`.

## 6. Repository scripts

The same steps are wrapped for the integration tests, so they do not have to be repeated by hand:

```powershell
# creates <repo>\.venv (python 3.11, numpy + pandas) and prints PYTHONNET_TEST_DLL / PYTHONPATH
. .\scripts\test\Initialize-PythonEnvironment.ps1 -Packages numpy,pandas,polars

.\scripts\test\Invoke-Tests.ps1 -Category Python          # picks up <repo>\.venv automatically
.\scripts\test\Invoke-VersionMatrix.ps1                   # .venv-matrix\*, one environment per combination
```

`uv` must be in `PATH`. The default environment is `<repository root>\.venv`; `-PythonVersion` and `-EnvironmentPath`
select another interpreter and folder.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `uv` is not recognized | the standalone installer changes `PATH`: open a new shell, or add `%USERPROFILE%\.local\bin` manually |
| `... cannot be loaded because running scripts is disabled` | `Set-ExecutionPolicy -Scope Process Bypass`, or skip activation and call `Scripts\python.exe` directly |
| `ImportError: No module named pandas` (or numpy) | the packages live in another environment: check `uv pip list --python <venv>\Scripts\python.exe`, then install again into the environment used by the connection string |
| `Can't find python*.dll file` | `PYTHONDLL` points into the virtual environment: it must be the `python3xx.dll` of the base interpreter |
| `BadImageFormatException` / `%1 is not a valid Win32 application` | a 32-bit interpreter or 32-bit Excel: use an `...-windows-x86_64-none` build |
| `pandas` is built from source or the resolution fails | the version is outside the supported range (pandas 1.5 – 3.1, numpy 1.26 – 2.x) |
| Environment is broken after experiments | `uv venv <path> --clear --python 3.11` recreates it; `uv cache dir` and `uv cache clean` inspect / drop the download cache |

## See also

- [installation.md](installation.md) — installing the provider (administrator rights, GAC, `machine.config`);
- [power-query.md](power-query.md) — Power Query / Excel scenarios;
- [connection-strings.md](connection-strings.md) — `PYTHONDLL`, `PYTHONPATH`, `FILE` and quoting.
