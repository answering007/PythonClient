# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Connection string (P0-1).** Values are no longer upper-cased: the custom parser was replaced by
  `DbConnectionStringBuilder.ConnectionString`, keyword lookup is case-insensitive.
  *Migration:* quote values that contain `;` — `PYTHONPATH="C:\Py\Lib;C:\Py\Lib\site-packages"`.
- **Parameter collection (P0-2, P0-4).** `CopyTo` copies all items from the requested index;
  `Parameters[name] = value` replaces the existed parameter instead of throwing; `Insert` honours the index;
  `Contains(object)`/`IndexOf(object)`/`Remove(object)` are safe for `null` and non-parameter values;
  `IsSynchronized` is `false`; the enumerator returns parameters instead of key/value pairs.
- **Parameters (P0-3).** `PythonParameter.Value = null` and `DbType = DbType.X` are allowed;
  `DBNull` no longer breaks `Size` calculation.
- **Provider registration (P0-5).** `DbProviderFactories` is created inside `system.data`, the existed record is
  updated, `type` is version-independent, `machine.config` is backed up and replaced atomically (UTF-8 without BOM).
  The dependency on `Microsoft.Build.Utilities.v4.0` was removed.
- **GIL and engine lifecycle (P0-6, P0-7).** The GIL is acquired per operation instead of being held for the whole
  application; the engine is reference counted, so closing one connection no longer shuts it down for others;
  `PythonDataProvider.ShutdownOnLastClose` allows keeping the interpreter alive until the process exits.
- **Date/time converters (P0-8).** Dates before 1970, `NaT` and out-of-range `timedelta64` values no longer throw:
  `NaT` and out-of-range values are returned as `DBNull`.
- **DataFrame conversion (P0-9).** dtype is normalized (`datetime64[ns, UTC]`, `string[python]`, nullable `Int64`…),
  unknown dtype raises `NotSupportedException` instead of the silent string conversion, column names are normalized
  and uniquified, duplicated labels are reported, the result object is disposed, `np.nan` becomes `DBNull`.
- **ADO.NET contracts (P1-1…P1-5).** `CommandBehavior` (`SchemaOnly`, `SingleRow`, `CloseConnection`) is supported,
  `CommandTimeout` and transactions raise `NotSupportedException`, `ExecuteNonQuery` allows scripts without `result`,
  script files are read as UTF-8 and cached, parameters support `null`/`decimal`/`Guid`/`TimeSpan`/`DateTimeOffset`,
  `@`/`:` prefixes are trimmed, execution context is per call (`PythonScriptContext`).

### Added

- `PythonScriptContext` and `PythonDataQuery.RunScript(scriptText, context)` for isolated script execution.
- `SupportedTypes.Register/Reset/TryGetConverter/NormalizeDtype` — extensible dtype registry.
- `PythonDataProvider.ShutdownOnLastClose`, `PythonDataProvider.Shutdown()`, `PythonDataProvider.Version` caching.
- `PythonDataQuery.StrictColumnTypes` (default `true`).
- Unit test suite (`TestCategory=Unit`, 85 tests) that does not require Python.
- `install.ps1` / `uninstall.ps1`, `LICENSE`, `SECURITY.md`, `CHANGELOG.md`, `.editorconfig`, CI workflow.

### Changed

- Projects migrated to SDK-style (`Microsoft.NET.Sdk`) with `PackageReference`; `packages.config` removed.
- Release builds keep portable PDB files (the post-build PDB deletion was removed).
- The legacy Visual Studio Installer project (`.vdproj`) is deprecated in favour of `install.ps1`.

### Deprecated

- `PythonDataQuery.Query`, `PythonDataQuery.Parameters`, `PythonDataQuery.Reset()`: use `PythonScriptContext` or
  `PythonCommand` properties.

### Breaking changes

- `PythonConnectionStringBuilder` rejects values with unquoted `;` (spec-compliant).
- `SupportedTypes.Values` is `IReadOnlyDictionary<string, IColumnConverter>` (was mutable `IDictionary`).
- `SupportedTypes` is a static class.
- `float` columns use `DBNull` instead of `double.NaN` for `np.nan`.
- Unknown dtype raises `NotSupportedException` (previously converted to string silently).
- Duplicated DataFrame column labels raise `NotSupportedException`.
- `PythonParameterCollection` enumeration yields `PythonParameter` items (not `KeyValuePair`).
- `CommandTimeout > 0` raises `NotSupportedException`.

## [1.2] - 2025

- .NET Framework 4.8 target, XML documentation for `DbProviderFactory`, README example.
