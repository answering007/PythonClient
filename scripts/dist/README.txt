Pike.PythonClient64 {{VERSION}} - ADO.NET x64 data provider for Power Query / Excel
==============================================================================

The provider runs a Python script and returns its pandas.DataFrame / polars.DataFrame
as a table: Power Query gets a real data source written in Python.

Requirements
------------
  * 64-bit Windows (the provider is x64-only);
  * .NET Framework 4.8 (included in Windows 10 1903+ and Windows 11);
  * 64-bit Excel for the Power Query scenario (32-bit Excel cannot load the provider);
  * Python 3.9 - 3.13 x64 with numpy and pandas (polars is optional) - installed separately,
    the provider does not install Python;
  * administrator rights (the assemblies are published into the GAC and the provider is
    registered in the x64 machine.config).

Install
-------
  1. Extract this archive to a local folder (do not run it from inside the ZIP).
  2. Run install.cmd and confirm the UAC prompt.
     The script prints the prerequisites, publishes Pike.PythonClient64.dll, Python.Runtime.dll
     and Pike.PythonClient64.Install.dll into the GAC and registers the provider in
     machine.config. No files are copied to %ProgramFiles%.
  3. Restart Excel (Power Query keeps the provider inside the Excel process).

Use in Power Query
------------------
  let
      Source = AdoDotNet.Query(
          "Pike.PythonClient64.Data.PythonProviderFactory",
          "PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH=""C:\Python311\Lib;C:\Python311\Lib\site-packages"";FILE=C:\Scripts\Report.py",
          "")
  in
      Source

  The script file must assign a pandas.DataFrame or polars.DataFrame to the variable 'result'.
  A non-empty third argument of AdoDotNet.Query is available inside the script as 'query'
  (for example, a compressed Excel table).
  Ready-made workbooks: samples\power-query.

Verify / uninstall
------------------
  verify.cmd      read-only check: GAC entries, machine.config record and the type resolved in a
                  new process, compared with the payload of this archive (no administrator rights).
  uninstall.cmd   removes the machine.config record and the GAC entries. The registration code
                  is kept in this archive (lib\), not in the GAC: run uninstall.cmd from the
                  extracted archive (keep it until the provider is uninstalled).

Silent / unattended
-------------------
  install.cmd -NoPause
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\install.ps1 -SourcePath lib
  (advanced: -SkipGac, -SkipMachineConfig, -SkipPrerequisiteCheck;
   diagnostics: -KeepFiles -InstallPath "D:\path" copies the assemblies next to a known path)

Troubleshooting
---------------
  * "Administrator rights are required" - start install.cmd again and confirm UAC.
  * "Python runtime is not configured" / "Can't find python*.dll file" - PYTHONDLL must point
    to an existing 64-bit python3xx.dll.
  * Excel still runs the old build after an upgrade - restart Excel, then run verify.cmd;
    a stale GAC entry is reported there.
  * "The 'query' variable is not defined" - the command text was empty; pass a payload or make
    the script independent of 'query'.
  * Details: README.md and docs\installation.md in the repository.
