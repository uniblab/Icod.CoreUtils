# TIMEOUT(1)

## NAME

**timeout** — run a command with a time limit

## SYNOPSIS

```text
timeout [OPTION] DURATION COMMAND [ARG]...
```

## DESCRIPTION

`Icod.CoreUtils.Timeout` implements the GNU Coreutils 9.11 `timeout` command.

The command keeps policy in the command project while consuming Completion Gate F4 for exact argument-vector launch, executable lookup, protected child identities, process/process-group signal delivery, cancellation cleanup, termination translation, and monotonic timing.  In standalone non-foreground POSIX operation, the `timeout` monitor makes itself the leader of a new process group and the command inherits that group.  Timeout and continuation signals are delivered both directly to the monitored command and to the shared group so descendants are covered without shell delegation.  The monitor suppresses `SIGTTIN` and `SIGTTOU` so background terminal access cannot stop the watchdog, while the launched command receives the normal default dispositions for those signals.

Windows uses the platform substitutions declared by F4.  A non-foreground TERM or KILL timeout uses process-tree cancellation because POSIX group-signal semantics are not available; unsupported signal operations are reported rather than silently pretending to have POSIX behavior.

## DEFERRED LINUX PARENT-DEATH PROTECTION

GNU Coreutils 9.11 uses Linux `prctl(PR_SET_PDEATHSIG, ...)` in the forked child immediately before `exec`, followed by a parent-PID check, so the monitored command cannot silently outlive a `timeout` monitor that dies unexpectedly.

`Icod.CoreUtils.Timeout` does not currently promise that Linux-specific guarantee.  Support is intentionally deferred until `Icod.Processes` has a safe native pre-exec launch boundary or equivalent native trampoline.  Calling `fork()` from a multithreaded managed .NET process and then executing managed code in the post-fork child is not considered an acceptable implementation.

Windows has no `fork()`/`prctl()` equivalent, and this Linux-specific parent-death contract will not be emulated there.

## PATHNAME GLOBBING

`timeout` does not perform `Icod.CommandFramework` pathname glob expansion. Duration and timeout controls and the command argument vector have their own meanings and are not reinterpreted as pathname patterns by `timeout`; child-command arguments are preserved for the invoked program. Any expansion performed by an invoking shell or other caller occurs before `timeout` receives the arguments.

## AUTHORS

GNU `timeout` was written by Pádraig Brady.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`timeout(1)`, `nice(1)`, `nohup(1)`
