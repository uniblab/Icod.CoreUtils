# Batch 69 — Root-directory execution

Batch 69 replaces the historical `chroot` placeholder with a privilege-aware Unix implementation aligned with GNU Coreutils 9.11.

## Execution model

`chroot` cannot be implemented correctly by launching an ordinary child process first: the command must be resolved after the root-directory change. The system implementation therefore changes the root of the dedicated `chroot` process, optionally changes its working directory to `/`, applies supplementary groups and requested group/user IDs, and finally calls `execvp`. A successful `execvp` replaces the .NET process and never returns; the requested command therefore inherits the process's real standard descriptors and its own eventual exit status becomes the `chroot` process status.

No command string is constructed and no shell is used to interpret command operands. The command and every argument remain separate entries in the native argument vector. The only shell-specific behavior is GNU's no-command fallback: `$SHELL -i`, or `/bin/sh -i` when `SHELL` is unset or empty.

## Identity and group handling

The Unix implementation supports `--userspec=USER:GROUP`, `--groups=G_LIST`, and `--skip-chdir`. It performs a best-effort user/group resolution before entering a changed root so NSS machinery and host records can serve as fallbacks, repeats the resolution after `chroot`, initializes supplementary groups from the selected user when `--groups` is absent, supports an explicitly empty `--groups=` list to clear supplementary groups, then applies `setgroups`, `setgid`, and `setuid` in that order.

Numeric user and group IDs are accepted. A leading `+` forces numeric interpretation; otherwise a numeric-looking name is first allowed to resolve as an actual account/group name, matching GNU's compatibility behavior.

## Platform boundary and tests

`IChrootPlatform` isolates the irreversible host operation from command-line policy. Unit tests use a fake implementation and therefore never call `chroot(2)`, change credentials, or replace the test process. Linux, macOS, and FreeBSD use the libc-backed system implementation. Other hosts return a controlled status 125 diagnostic for execution while still supporting `--help` and `--version`.

The command uses GNU execution-status conventions: setup/root/identity failures are 125, an executable that cannot be invoked is 126, and command-not-found is 127.

## Roadmap sequencing

Batch 68 (`Icod.ProcPs.Top`) is deliberately deferred. The ProcPs work through Batch 67 will be migrated out of this repository before `top` is implemented, allowing the largest ProcPs command to target the final suite boundary directly. Batch 69 and later Coreutils work may proceed while that deferred ProcPs milestone remains open.
