# Icod.CoreUtils

Standard BSD and Linux coreutils ported to .NET.

## Pathname globbing policy

`Icod.CoreUtils` intends to provide consistent pathname globbing for appropriate filesystem operands through `Icod.CommandFramework`, rather than relying exclusively on the invoking shell to expand pathnames. This gives utilities a defined cross-platform expansion model when wildcard-bearing operands reach the application unexpanded.

Globbing is **command- and operand-specific**. A utility must expand only operands whose semantic role is an eligible filesystem pathname. It must not blindly expand every argument merely because the argument contains wildcard characters. Destination names, names being created, lexical pathname text, expressions, data, and arguments belonging to a child command remain literal unless a tool explicitly defines otherwise.

For this CoreUtils policy, the pathname glob syntax is:

- `*` — matches zero or more characters within one pathname component; it does not consume a pathname separator.
- `?` — matches exactly one character within one pathname component; it does not consume a pathname separator.
- `**` — matches zero or more complete pathname components when, and only when, the complete component is exactly `**`. For example, `src/**/*.cs` is recursive, while an occurrence of `**` embedded inside another component does not acquire recursive meaning.

Recursive `**` expansion is pathname selection. It does not imply or enable a utility's own recursive operation. For example, expanding `rm **/*.tmp` produces explicit operands; it does not grant `rm` permission to recursively remove directories. Likewise, expanding a set of pathnames for `chmod` is distinct from `chmod -R`.

The invoking shell may already have expanded an unquoted pattern before the utility starts. In that case the utility simply receives the resulting literal pathname operands. The policy in this section governs wildcard-bearing pathname operands that reach an `Icod.CoreUtils` application unexpanded.

Unmatched-pattern behavior is a separate command-level policy and is not fixed globally by this declaration. Each participating utility must preserve its own operand semantics and document any command-specific unmatched-pattern behavior when globbing support is implemented.

### Utilities in which globbing will be implemented

The following utilities are the committed globbing implementation set. This declaration records intended behavior; it does **not** claim that every listed utility already implements the policy in the current release.

| Area | Utilities |
| --- | --- |
| File content and input | `cat`, `cut`, `expand`, `fmt`, `fold`, `head`, `nl`, `od`, `paste`, `pr`, `sort`, `tac`, `tail`, `unexpand`, `wc` |
| Checksums and hashes | `b2sum`, `cksum`, `md5sum`, `sha1sum`, `sha224sum`, `sha256sum`, `sha384sum`, `sha512sum`, `sum` |
| File inspection and reporting | `df`, `dir`, `du`, `ls`, `readlink`, `realpath`, `stat`, `vdir` |
| Copy, move, and install | `cp`, `install`, `mv` |
| Metadata mutation | `chcon`, `chgrp`, `chmod`, `chown` |
| Removal and destructive operations | `rm`, `rmdir`, `shred` |
| Other pathname operations | `sync`, `touch`, `truncate` |

Eligibility remains operand-specific even for utilities in this table. In particular, source collections may be expandable while destinations and other singular control operands remain literal. For example, the source operands of `cp`, `mv`, and `install` are candidates for expansion, while their destination operand is not. Similarly, an option such as `--reference=FILE` does not automatically become expandable merely because the utility also accepts expandable primary pathname operands.

### Utilities in which CommandFramework globbing will not apply

The following utilities will **not** perform `Icod.CommandFramework` pathname expansion. This does not prevent an invoking shell from expanding a pattern before launching the utility; it means the utility itself will not reinterpret wildcard-bearing arguments as filesystem glob patterns.

| Area | Utilities |
| --- | --- |
| Host, process, environment, and identity information | `arch`, `groups`, `hostid`, `hostname`, `id`, `logname`, `nproc`, `pinky`, `printenv`, `pwd`, `tty`, `uname`, `whoami` |
| Numeric, data, and string operations | `echo`, `expr`, `factor`, `numfmt`, `printf`, `seq`, `sleep`, `tr`, `yes` |
| Pure status commands | `false`, `true` |
| Creation and template commands | `mkdir`, `mkfifo`, `mknod`, `mktemp` |
| Command wrappers and executors | `env`, `nice`, `nohup`, `runcon`, `stdbuf`, `timeout` |
| Special command grammars | `dd`, `test` |
| Multicall dispatcher | `coreutils` |

This exclusion is intentional. Creation-oriented utilities must preserve the names they are asked to create. Data- and expression-oriented utilities may legitimately receive `*`, `?`, or `**` as ordinary text. Wrapper utilities must pass the child command and its arguments through without reinterpreting them. `dd` and `test` have command grammars in which automatic argv expansion would alter the meaning of the command. The `coreutils` multicall dispatcher likewise leaves pathname policy to the selected utility rather than applying globbing itself.

Utilities not listed in either table remain intentionally undecided. Their pathname roles will be reviewed individually before a globbing policy is assigned to them.
