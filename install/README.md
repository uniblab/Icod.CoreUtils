# INSTALL(1)

## NAME

**install** — copy files and set attributes

## SYNOPSIS

```text
install [OPTION]... [-T] SOURCE DEST
install [OPTION]... SOURCE... DIRECTORY
install [OPTION]... -t DIRECTORY SOURCE...
install [OPTION]... -d DIRECTORY...
```

## PATHNAME GLOBBING

In ordinary copy forms, source operands that contain supported pathname patterns are expanded in-process according to the repository policy. Matches preserve source-operand order and repetition; unmatched source patterns are preserved as literal operands.

Destination operands and `-t`/`--target-directory` values remain literal. In `-d`/`--directory` mode, positional directory operands also remain literal because they name directories to be created rather than existing source objects to select.

## DESCRIPTION

`Icod.CoreUtils.Install` implements the GNU Coreutils 9.11 `install` command. It copies source files to destinations, creates directories, and applies requested ownership, mode, timestamp, stripping, backup, comparison, and SELinux-context policies.

File replacement is staged beside the destination. Strip, ownership, mode, timestamp, and context operations are completed on the private stage before the transactional replacement layer flushes and atomically publishes it. Existing destinations are never modified in place.

## OPTIONS

```text
-b, --backup[=CONTROL]
    Make a backup of each existing destination file.

-C, --compare
    Compare content and attributes; do not modify matching files.

-c
    Accepted and ignored for historical compatibility.

-d, --directory
    Treat all operands as directory names.

-D, --create-leading-dirs
    Create all leading components of DEST.

-g, --group=GROUP
    Set group ownership.

-m, --mode=MODE
    Set the installed mode; the default is u=rwx,go=rx,a-s.

-o, --owner=OWNER
    Set owner.

-p, --preserve-timestamps
    Apply SOURCE access and modification times.

-s, --strip
    Strip symbol tables.

      --strip-program=PROGRAM
    Use PROGRAM to strip binaries.

-S, --suffix=SUFFIX
    Override the usual backup suffix.

-t, --target-directory=DIR
    Copy all SOURCE operands into DIR.

-T, --no-target-directory
    Treat DEST as an ordinary file rather than a directory.

-v, --verbose
    Report each created directory or installed file.

      --debug
    Explain actions and imply --verbose.

      --preserve-context
    Preserve the SOURCE SELinux context.

-Z, --context[=CTX]
    Use the destination-default or explicit SELinux context.

      --help
    Display command help and exit.

      --version
    Display version information and exit.
```

Backup control accepts GNU-compatible `none`/`off`, `simple`/`never`, `numbered`/`t`, and `existing`/`nil` modes. `--compare` is incompatible with timestamp preservation and stripping. `--preserve-context` and `--context` are mutually exclusive.

## IMPLEMENTATION NOTES

The implementation is split into command parsing, installation planning, staged-file policy, and platform adaptation.

- `src/Command.cs` provides the public synchronous and asynchronous command surface.
- `src/InstallArgumentParser.cs` owns GNU-facing option precedence and validation.
- `src/InstallEngine.cs` performs directory creation and transactional file publication.
- `src/InstallStripper.cs` invokes only the explicitly requested strip program, without a command shell.
- `src/InstallSecurityContext.cs` implements SELinux context preservation and explicit labeling through the platform provider, including destination-policy lookup without shelling to `restorecon`; disabled SELinux policies receive GNU-style warnings.

An explicitly named directory symlink or eligible directory reparse point may anchor target-directory interpretation and `-D` creation of missing descendants. The terminal destination is different: publication commits against a no-follow ordinary-file observation. A final symbolic link, junction, or other reparse object is therefore rejected with a controlled diagnostic rather than dereferenced or removed non-atomically. The referenced target is left unchanged.

`--debug` implies verbose output and additionally reports whether transactional publication used a configured private sibling stage or retained an equivalent destination. An explicitly selected strip program is invoked only with `--strip`; otherwise GNU-compatible warning behavior is used.

## EXIT STATUS

```text
0  The requested installation completed successfully.
1  Usage, filesystem, metadata, stripping, security-context, cancellation, or publication failed.
```

## AUTHORS

GNU `install` was written by David MacKenzie.

Migrated to .Net by Timothy J. Bruce <uniblab@hotmail.com>.

## COPYRIGHT

Copyright (c) 2026 Timothy J. Bruce

See the repository `LICENSE` file for licensing terms and notices applicable to this project.

## SEE ALSO

`install(1)`, `cp(1)`, `mkdir(1)`
