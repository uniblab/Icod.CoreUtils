# NICE(1)

## NAME

**nice** — run a command with adjusted scheduling priority

## SYNOPSIS

```text
nice [OPTION] [COMMAND [ARG]...]
```

## DESCRIPTION

`Icod.CoreUtils.Nice` is a managed .NET implementation of GNU Coreutils `nice(1)`, modeled on GNU Coreutils 9.11.

With no command and no explicit adjustment, `nice` prints the current niceness. With a command, it computes a target niceness from the current process value plus the requested adjustment and launches the command through the shared process provider.

The normal GNU niceness range is `-20` (more favorable scheduling priority) through `19` (less favorable). The implementation clamps the resulting target to that range.

## OPTIONS

```text
-n, --adjustment=N
    Add integer N to the current niceness. The default adjustment is 10.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

Historical `-N`, `--N`, and signed numeric adjustment spellings accepted by GNU-style invocation are also recognized by the parser.

## EXIT STATUS

```text
125    nice itself failed.
126    COMMAND was found but could not be invoked.
127    COMMAND could not be found.
other  The exit status translated from COMMAND.
```

## PLATFORM NOTES

Priority observation and mutation use the shared process-priority provider. Raising scheduling priority may require additional privilege and can fail with access denied.

On Windows, after applying the wrapper's priority mapping, the implementation also applies the selected priority to the started child through the process-start callback because native niceness inheritance does not match POSIX behavior exactly.

## PATHNAME GLOBBING

`nice` does not perform `Icod.CommandFramework` pathname glob expansion. The command and argument vector supplied for execution are not reinterpreted as pathname patterns by `nice`; child-command arguments are preserved for the invoked program. Any expansion performed by an invoking shell or other caller occurs before `nice` receives the arguments.

## AUTHORS

GNU `nice` was written by David MacKenzie.

Migrated to .NET by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`nice(1)`, `env(1)`, `nohup(1)`
