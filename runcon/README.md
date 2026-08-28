# RUNCON(1)

## NAME

**runcon** — run a command in an SELinux security context

## SYNOPSIS

```text
runcon CONTEXT COMMAND [ARG]...
runcon [-c] [-u USER] [-r ROLE] [-t TYPE] [-l RANGE] COMMAND [ARG]...
runcon
```

## DESCRIPTION

`Icod.CoreUtils.RunCon` is a managed .NET implementation of GNU Coreutils `runcon(1)`, modeled on GNU Coreutils 9.11.

With no context and no command, `runcon` prints the current SELinux process context. A complete context may be supplied explicitly, or a target context may be computed from the current process and executable contexts and then modified component by component.

The selected context is validated by the SELinux platform provider before command execution.

## OPTIONS

```text
-c, --compute
    Compute a process transition context before applying component overrides.

-u, --user=USER
    Set the SELinux user component.

-r, --role=ROLE
    Set the SELinux role component.

-t, --type=TYPE
    Set the SELinux type component.

-l, --range=RANGE
    Set the SELinux range component.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Options use GNU-style `+` ordering: the first operand ends option parsing.

## EXIT STATUS

```text
0      Printing the current context or informational output succeeded.
125    runcon usage, SELinux setup, context computation/validation, or wrapper
       execution failed.
other  The status supplied by the SELinux execution provider for COMMAND.
```

## PLATFORM NOTES

`runcon` requires an available and enabled SELinux implementation. Unsupported hosts and hosts on which SELinux is disabled are diagnosed explicitly.

No non-SELinux security model is substituted. Context retrieval, file-context lookup, transition computation, validation, and execution are all delegated to the SELinux platform implementation.

## AUTHORS

GNU `runcon` was written by Russell Coker.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`runcon(1)`, `chroot(1)`, `env(1)`
