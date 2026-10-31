# Security policy

## Supported versions

| Version | Supported |
|---|---|
| `dev` / latest release | yes |
| older releases | no |

## Reporting a vulnerability

Please do not open a public issue for security problems. Report them privately to the repository maintainers
(for example through a GitHub security advisory) and allow time for a fix before any disclosure.

Include: affected version, reproduction steps, impact assessment and, if possible, a proposed fix.

## Threat model

The provider executes arbitrary Python code **inside the consumer process** (Excel, Power Query, your application)
with the permissions of that process:

- there is no sandbox, no resource limit and no code signing of the scripts;
- the `FILE` value of the connection string points to a local file that is executed as Python code;
- parameters and query text are passed to the Python interpreter and must be treated as untrusted input
  when they come from outside the process;
- the connection string can point to any `python3xx.dll` on the machine; the provider only validates that the file
  exists.

Recommendations:

- run the provider only with scripts and connection strings you control;
- keep script files and their folders writable only by trusted users;
- prefer `pythonnet`'s own security guidance for the Python environment (packages, `site-packages`).

## Strong name key

`Pike.PythonClient64\key.snk` (the strong name key used to sign `Pike.PythonClient64.dll` and
`Pike.PythonClient64.Install.dll`) is stored in the repository. Changing or removing it changes the `PublicKeyToken`,
which breaks the existing `machine.config` registrations (they contain the fully qualified type name), the GAC entries
and assembly bindings. Keep the key stable for released versions; if the key must be rotated, publish a migration note
and update the registration with `install.ps1`.

A strong name is an identity, not a signature of authorship: the key is public, so anyone can build an assembly with
the same `PublicKeyToken`. Authenticity has to be established with an Authenticode signature of the shipped
`Pike.PythonClient64.dll` (and of an installer package, if one is added later).

## Global assembly cache

`scripts\install.ps1` publishes `Pike.PythonClient64.dll` and `Python.Runtime.dll` into the GAC (`GAC_64`), because the
`machine.config` registration resolves the provider by its fully qualified name outside the host application base.
The GAC is machine-wide shared state:

- only one version of the provider can be installed at a time (`AssemblyVersion` is fixed to `2.0.0.0`, so an upgrade
  replaces the previous GAC entry);
- the provider becomes available to **any** process on the machine that registers it, including 64-bit Excel — review
  the threat model above before installing it on a shared machine;
- `scripts\uninstall.ps1` must be used to remove it: deleting the folder only leaves the assemblies in the GAC.
