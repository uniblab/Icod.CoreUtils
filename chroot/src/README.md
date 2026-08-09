# `chroot` source layout

- `Command.cs` owns GNU Coreutils 9.11 command-line parsing, portable diagnostics, default-shell selection, and the injectable execution boundary used by tests.
- `ChrootPlatform.cs` owns the Unix-native root, identity, supplementary-group, and `execvp` operations. Successful execution replaces the `chroot` process, so command lookup occurs after the root change and command arguments are never reinterpreted by a shell.

Windows and other unsupported hosts retain portable `--help` and `--version` behavior and return a controlled failure for root-changing execution.
