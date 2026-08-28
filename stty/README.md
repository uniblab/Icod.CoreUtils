# STTY(1)

## NAME

**stty** — print or change terminal characteristics

## SYNOPSIS

```text
stty [-F DEVICE | --file=DEVICE] [SETTING]...
stty [-F DEVICE | --file=DEVICE] [-a|--all]
stty [-F DEVICE | --file=DEVICE] [-g|--save]
```

## DESCRIPTION

`Icod.CoreUtils.Stty` is a managed .NET implementation of GNU Coreutils `stty(1)`, modeled on GNU Coreutils 9.11.

Without settings, the command prints the default terminal-mode summary. `--all` prints the complete human-readable state, while `--save` emits the machine-readable serialization accepted by the terminal mode editor.

Settings are read and applied through the shared terminal-control provider. A specific terminal device may be selected instead of standard input.

## OPTIONS

```text
-a, --all
    Print all current settings in human-readable form.

-g, --save
    Print the complete current mode in machine-readable form.

-F, --file=DEVICE
    Open and use DEVICE instead of standard input.

--help
    Display command help and exit.

--version
    Display version information and exit.
```

A bare numeric setting changes input and output speed. The `speed` operand reports the current speed without changing it. Supported editing syntax includes common settings such as `sane`, `raw`, `cooked`, `echo`, `icanon`, `isig`, `opost`, control-character names, `ispeed`, `ospeed`, `line`, `drain`, and `-drain`. Prefix a boolean setting with `-` to disable it.

## EXIT STATUS

```text
0    Reporting or mutation completed successfully.
1    Usage, terminal observation, editing, mutation, cancellation, or another
     operational action failed.
```

## PLATFORM NOTES

On POSIX hosts, the terminal provider supplies the native terminal-mode model and supported editing operations.

Windows console support preserves the complete native console mode and supports `sane`/`raw` plus processed-input, line-input, echo, and output-processing toggles. POSIX speeds, parity, line discipline, control characters, and drain timing are reported as unsupported rather than emulated.

## AUTHORS

GNU `stty` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`stty(1)`, `tty(1)`
