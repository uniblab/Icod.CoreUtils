# TIMEOUT(1)

## NAME

**timeout** — run a command with a time limit

## SYNOPSIS

```text
timeout [OPTION] DURATION COMMAND [ARG]...
```

## DESCRIPTION

`Icod.CoreUtils.Timeout` implements the GNU Coreutils 9.11 `timeout` command.

The command keeps policy in the command project while consuming Completion Gate F4 for exact argument-vector launch, executable lookup, protected child identities, process/process-group signal delivery, cancellation cleanup, termination translation, and monotonic timing.  Non-foreground POSIX launches request a new child process group atomically through the F4 executor so descendants can receive timeout and continuation signals without shell delegation.

Windows uses the platform substitutions declared by F4.  A non-foreground TERM or KILL timeout uses process-tree cancellation because POSIX group-signal semantics are not available; unsupported signal operations are reported rather than silently pretending to have POSIX behavior.

## AUTHORS

GNU `timeout` was written by Pádraig Brady.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`timeout(1)`, `nice(1)`, `nohup(1)`
