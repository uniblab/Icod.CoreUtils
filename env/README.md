# ENV(1)

## NAME

**env** — run a command in a modified environment

## SYNOPSIS

```text
env [OPTION]... [-] [NAME=VALUE]... [COMMAND [ARG]...]
```

## DESCRIPTION

`Icod.CoreUtils.Env` is a managed .NET implementation of GNU Coreutils `env(1)`, modeled on GNU Coreutils 9.11.

The command starts from the inherited environment unless `-i` or a lone `-` requests an empty one, applies requested removals and `NAME=VALUE` assignments, and then either prints the resulting environment or executes a command with it.

Process execution uses the shared process provider with exact argument-vector construction, executable lookup, working-directory control, signal-launch policy, and portable launch-failure translation.

## OPTIONS

```text
-a, --argv0=ARG
    Pass ARG as argument zero of COMMAND.

-i, --ignore-environment
    Start with an empty environment.

-0, --null
    Terminate printed environment entries with NUL instead of newline.
    This may not be combined with COMMAND execution.

-u, --unset=NAME
    Remove NAME from the inherited environment.

-C, --chdir=DIR
    Run COMMAND with DIR as its working directory.

-S, --split-string=S
    Split S into arguments using env's shebang-oriented split-string rules.

--block-signal[=SIG]
    Arrange for selected signals to be blocked in COMMAND.

--default-signal[=SIG]
    Reset selected signal dispositions to their defaults for COMMAND.

--ignore-signal[=SIG]
    Arrange for selected signals to be ignored in COMMAND.

--list-signal-handling
    List non-default signal handling to standard error.

-v, --debug
    Report environment and command-processing steps.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Signal arguments may be names such as `PIPE` or numeric signal values. Multiple signals may be comma-separated. Omitting the signal selects all known signals; an empty signal argument is a no-op.

If no `COMMAND` remains, the resulting environment is printed.

## EXIT STATUS

```text
0      Environment printing or informational output succeeded.
125    env itself failed, including usage or cancellation.
126    COMMAND was found but could not be invoked.
127    COMMAND could not be found.
other  The exit status translated from COMMAND.
```

## PLATFORM NOTES

Signal-launch options are implemented through the shared process-signal provider. Exact POSIX signal-mask and disposition behavior therefore depends on host support; unsupported operations are surfaced by the provider rather than silently invented.

Child standard handles are inherited directly in normal production execution unless explicit streams are injected by a caller.

## PATHNAME GLOBBING

`env` does not perform `Icod.CommandFramework` pathname glob expansion. Path-valued control operands such as `--chdir=DIR` and the `COMMAND [ARG]...` vector are not glob-expanded by `env`; child-command arguments are preserved for the invoked program. Any expansion performed by an invoking shell or other caller occurs before `env` receives the arguments.

## AUTHORS

GNU `env` was written by Richard Mlynarik, David MacKenzie, and Assaf Gordon.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`env(1)`, `printenv(1)`, `nice(1)`, `nohup(1)`
