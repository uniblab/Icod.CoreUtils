# hostname source

This directory contains the GNU Coreutils `hostname` command implementation.

- `Command.cs` owns the GNU Coreutils 9.11 command-line contract, diagnostics, standard-stream behavior, cancellation, and exit status.
- `HostNamePlatform.cs` owns the host boundary for reading and changing the active host name.

The Coreutils profile intentionally does not implement the extended GNU Inetutils/net-tools query options such as `-s`, `-f`, `-i`, `-F`, or `-y`. GNU Coreutils 9.11 defines only the zero-operand query form, the one-operand mutation form, and the common `--help` and `--version` options.

Active-hostname mutation uses `sethostname(2)` on Linux, macOS, and FreeBSD. Hosts without a defensible equivalent return a controlled unsupported-operation diagnostic rather than reporting false success.
