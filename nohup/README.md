# NOHUP(1)

## NAME

**nohup** — run a command immune to terminal hangup conventions

## SYNOPSIS

```text
nohup COMMAND [ARG]...
nohup OPTION
```

## DESCRIPTION

`Icod.CoreUtils.Nohup` is a managed .NET implementation of GNU Coreutils `nohup(1)`, modeled on GNU Coreutils 9.11.

The command inspects whether standard input, output, and error are terminals and adjusts them according to GNU `nohup` conventions before launching the child.

If standard output is a terminal, output is appended to `nohup.out` in the current directory when possible, with `$HOME/nohup.out` as the fallback. If standard error is a terminal, it is redirected to the selected output destination. Terminal standard input is made unreadable on Unix-like systems and replaced with a null input stream on Windows.

On Unix-like hosts, the child is launched with `SIGHUP` ignored.

## OPTIONS

```text
--help
    Display command help and exit.

--version
    Display version information and exit.
```

To choose an explicit output destination, use ordinary shell or host redirection, for example `nohup COMMAND > FILE`.

## EXIT STATUS

```text
125    nohup itself failed under the normal GNU policy.
126    COMMAND was found but could not be invoked.
127    COMMAND could not be found, or nohup itself failed when POSIXLY_CORRECT
       selects the POSIX-compatible internal-failure convention.
other  The exit status translated from COMMAND.
```

## PLATFORM NOTES

Terminal detection uses the shared terminal-device provider. The child process is deliberately launched with a leave-running cancellation policy so cancellation of the `nohup` wrapper does not automatically terminate the protected child.

Windows has no POSIX `SIGHUP` disposition to install. Its terminal-input handling therefore uses the platform substitution described above while preserving the same user-visible intent.

## PATHNAME GLOBBING

`nohup` does not perform `Icod.CommandFramework` pathname glob expansion. The command and argument vector supplied for execution are not reinterpreted as pathname patterns by `nohup`; child-command arguments are preserved for the invoked program. Any expansion performed by an invoking shell or other caller occurs before `nohup` receives the arguments.

## AUTHORS

GNU `nohup` was written by Jim Meyering.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`nohup(1)`, `env(1)`, `nice(1)`
