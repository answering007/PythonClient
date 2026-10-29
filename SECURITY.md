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

`Pike.PythonClient64\key.snk` (the strong name key used to sign the assembly) is stored in the repository.
Changing or removing it changes the `PublicKeyToken`, which breaks existing `machine.config` registrations and
assembly bindings. Keep the key stable for released versions; if the key must be rotated, publish a migration note
and update the registration with `install.ps1`.
