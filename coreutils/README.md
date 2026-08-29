# COREUTILS(1)

## NAME

**coreutils** — route Icod.CoreUtils commands through a single installed .NET tool

## SYNOPSIS

```text
coreutils COMMAND [OPTION]... [ARG]...
coreutils --help
coreutils --version
```

## DESCRIPTION

`coreutils` is the installable router for the `Icod.CoreUtils` command suite. It dispatches the requested command directly to the corresponding managed implementation while preserving the command's arguments and exit status.

The router package is intended for users who prefer one global .NET tool installation. Platform-specific ZIP archives remain available for users who want the traditional individual executables.

## INSTALLATION

```text
dotnet tool install --global Icod.CoreUtils
```

To install a specific version:

```text
dotnet tool install --global Icod.CoreUtils --version VERSION
```

## COMMANDS

The router supports all 105 command projects currently shipped by Icod.CoreUtils:

`arch`, `b2sum`, `base32`, `base64`, `basename`, `basenc`, `cat`, `chcon`, `chgrp`, `chmod`, `chown`, `chroot`, `cksum`, `comm`, `cp`, `csplit`, `cut`, `date`, `dd`, `df`, `dir`, `dircolors`, `dirname`, `du`, `echo`, `env`, `expand`, `expr`, `factor`, `false`, `fmt`, `fold`, `groups`, `head`, `hostid`, `hostname`, `id`, `install`, `join`, `link`, `ln`, `logname`, `ls`, `md5sum`, `mkdir`, `mkfifo`, `mknod`, `mktemp`, `mv`, `nice`, `nl`, `nohup`, `nproc`, `numfmt`, `od`, `paste`, `pathchk`, `pinky`, `pr`, `printenv`, `printf`, `ptx`, `pwd`, `readlink`, `realpath`, `rm`, `rmdir`, `runcon`, `seq`, `sha1sum`, `sha224sum`, `sha256sum`, `sha384sum`, `sha512sum`, `shred`, `shuf`, `sleep`, `sort`, `split`, `stat`, `stdbuf`, `stty`, `sum`, `sync`, `tac`, `tail`, `tee`, `test`, `timeout`, `touch`, `tr`, `true`, `truncate`, `tsort`, `tty`, `uname`, `unexpand`, `uniq`, `unlink`, `users`, `vdir`, `wc`, `who`, `whoami`, `yes`

Run `coreutils COMMAND --help` for the selected command's detailed help.

## ROUTER OPTIONS

```text
-h, --help
    Display router help and the supported command names.

-v, --version
    Display the router package version.
```

## PATHNAME GLOBBING

`coreutils` does not perform `Icod.CommandFramework` pathname glob expansion at the dispatcher level. It preserves the selected applet's argument vector and delegates operand interpretation to that applet, which is responsible for deciding whether any of its own operands are eligible for expansion. Any expansion performed by an invoking shell or other caller occurs before `coreutils` receives the arguments.

## DISTRIBUTION

Tagged releases also provide ZIP archives for Windows, Linux, and macOS on x64 and ARM64. Each archive contains the standalone command executables, the `coreutils` router, each executable's README and GPL license copy, and the repository-level README and LICENSE.

The release ZIPs are framework-dependent single-file applications and require the .NET 10 runtime.

## LICENSE

GNU General Public License version 3 or later.

Copyright (c) 2026 Timothy J. Bruce