# `mktemp` implementation

This directory contains the command-specific orchestration for secure GNU-compatible temporary-file and temporary-directory creation.

## Components

- `Command.cs` parses template, suffix, directory, quiet, dry-run, and temporary-directory options; resolves the effective parent directory; validates templates; calls the Shared secure temporary-object creator; writes the resulting pathname; and returns controlled statuses for invalid input or creation failure.
- `IMkTempEnvironment.cs` defines the environment inputs used when resolving `TMPDIR` and the host default temporary directory. Keeping this boundary injectable makes directory-selection behavior deterministic in tests.
- `SystemMkTempEnvironment.cs` reads process environment variables and supplies the portability-policy default (`/tmp` on supported Unix-like systems and the BCL temporary path elsewhere).

## Security boundary

Exclusive creation, randomized replacement characters, symlink/race resistance, retry policy, and file-versus-directory creation live in the reusable secure temporary-object facilities in `Icod.CoreUtils.Shared`. The command project retains only `mktemp` syntax, directory precedence, diagnostics, and output behavior.

No code in this directory implements an insecure “generate a name, then create it” sequence. The pathname reported to the caller is the object that was atomically created by the Shared provider, except in explicit dry-run mode.
