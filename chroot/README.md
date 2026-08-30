# CHROOT(1)

## NAME

**chroot** — run a command with a different root directory

## SYNOPSIS

```text
chroot [OPTION]... NEWROOT [COMMAND [ARG]...]
```

## PATHNAME GLOBBING

`chroot` is a Class C utility and performs no in-process pathname globbing. `NEWROOT` is a singular process-control boundary and remains exactly as supplied, while `COMMAND` and every following `ARG` are passed through without pathname reinterpretation. An invoking shell may still expand an unquoted pattern before `chroot` starts.

## DESCRIPTION

`Icod.CoreUtils.ChRoot` is a managed .NET implementation of GNU Coreutils `chroot(1)`, modeled on GNU Coreutils 9.11.

The native implementation changes the current process root to `NEWROOT`, normally changes the working directory to `/`, optionally applies requested user/group credentials, and then replaces the process image with `COMMAND`.

If no command is supplied, the command runs `$SHELL -i`, defaulting to `/bin/sh -i`.

## OPTIONS

```text
--groups=G_LIST
    Set supplementary groups from the comma-separated group list.

--userspec=USER:GROUP
    Select the user and group, by name or numeric identity, to use after
    changing root.

--skip-chdir
    Do not change the working directory to `/`. This is permitted only when
    NEWROOT resolves to the process's existing root.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

## EXIT STATUS

```text
125    chroot itself failed.
126    COMMAND was found but could not be invoked.
127    COMMAND could not be found.
other  On successful exec, the process becomes COMMAND and ultimately returns
       COMMAND's exit status.
```

## PLATFORM NOTES

The system implementation is supported on Linux, macOS, and FreeBSD through native `chroot`, credential, directory-change, and `execvp` operations. Windows is explicitly unsupported.

Changing root and changing credentials normally require elevated privilege. These operations are intentionally native and process-wide; they are not simulated with managed pathname rewriting.

## AUTHORS

GNU `chroot` was written by Roland McGrath.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`chroot(1)`, `env(1)`, `runcon(1)`
