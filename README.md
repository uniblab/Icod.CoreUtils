# Icod.CoreUtils

![Icod TUI Toolchain](https://raw.githubusercontent.com/uniblab/Icod.CoreUtils/v1.0.0/Icod.CoreUtils.banner.png)

Standard BSD and Linux coreutils ported to .NET.

## Pathname globbing policy

`Icod.CoreUtils` provides consistent in-process pathname globbing for appropriate filesystem operands through `Icod.CommandFramework`, rather than relying exclusively on the invoking shell to expand pathnames. This gives Class A and Class B utilities a defined cross-platform expansion model when wildcard-bearing operands reach the application unexpanded.

Globbing is **command- and operand-specific**. A utility must expand only operands whose semantic role is an eligible filesystem pathname. It must not blindly expand every argument merely because the argument contains wildcard characters. Destination names, names being created, lexical pathname text, expressions, data, and arguments belonging to a child command remain literal unless a tool explicitly defines otherwise.

For this CoreUtils policy, the pathname glob syntax is:

- `*` — matches zero or more characters within one pathname component; it does not consume a pathname separator.
- `?` — matches exactly one character within one pathname component; it does not consume a pathname separator.
- `**` — matches zero or more complete pathname components when, and only when, the complete component is exactly `**`. For example, `src/**/*.cs` is recursive, while an occurrence of `**` embedded inside another component does not acquire recursive meaning.

Recursive `**` expansion is pathname selection. It does not imply or enable a utility's own recursive operation. For example, expanding `rm **/*.tmp` produces explicit operands; it does not grant `rm` permission to recursively remove directories. Likewise, expanding a set of pathnames for `chmod` is distinct from `chmod -R`. Wildcards do not match a leading `.` unless the pattern names that period explicitly. Matching is ordinal case-insensitive on Windows and ordinal case-sensitive on other supported hosts.

Class A expansion preserves operand order and repetition. Explicitly named intermediate symbolic-link components may be followed where necessary to reach the named path, while wildcard-discovered symbolic-link directories are not recursively traversed by globbing. Globbing selects pathnames; it does not canonicalize literal operands.

The invoking shell may already have expanded an unquoted pattern before the utility starts. In that case the utility simply receives the resulting literal pathname operands. The policy in this section governs wildcard-bearing pathname operands that reach an `Icod.CoreUtils` application unexpanded.

For the Class A utilities below, an unmatched pattern is preserved as its original literal operand. Commands then apply their ordinary operand semantics to that literal. The conventional `-` operand is likewise preserved for commands that use it as a standard-input or standard-output sentinel.

### Class A utilities with in-process globbing

The following utilities implement the repository pathname-globbing policy for their eligible command-line pathname operands.

| Area | Utilities |
| --- | --- |
| File content and input | `cat`, `cut`, `expand`, `fmt`, `fold`, `head`, `nl`, `od`, `paste`, `pr`, `sort`, `tac`, `tail`, `unexpand`, `wc` |
| Checksums and hashes | `b2sum`, `cksum`, `md5sum`, `sha1sum`, `sha224sum`, `sha256sum`, `sha384sum`, `sha512sum`, `sum` |
| File inspection and reporting | `df`, `dir`, `du`, `ls`, `readlink`, `realpath`, `stat`, `vdir` |
| Copy, move, and install | `cp`, `install`, `mv` |
| Metadata mutation | `chcon`, `chgrp`, `chmod`, `chown` |
| Removal and destructive operations | `rm`, `rmdir`, `shred` |
| Other pathname operations | `sync`, `touch`, `truncate` |

Eligibility remains operand-specific even for utilities in this table. In particular, source collections may be expandable while destinations and other singular control operands remain literal. The following qualifications are part of the Class A contract:

- `cp`, `mv`, and ordinary file-copy forms of `install` expand source operands only. Destination operands and `--target-directory` values remain literal. `install -d` directory-creation operands remain literal because they name objects to be created.
- Option values such as `--reference=FILE` remain literal unless a command explicitly documents otherwise. Owner/group/mode/context specifications are not pathname patterns.
- `sort --files0-from`, `wc --files0-from`, and `du --files0-from` treat names read from those lists literally; command-line pathname operands remain independently eligible for expansion.
- `readlink` and `realpath` preserve the original spelling of non-pattern operands so that `Icod.Path` can interpret the intended pathname dialect. `realpath --relative-to` and `--relative-base` values remain literal.
- `**` selects explicit operands only. Command recursion such as `ls -R`, `chmod -R`, ownership recursion, `rm -r`, `rmdir --parents`, and `du` traversal remains controlled by each utility's own options and semantics.
- Old-style `od` offset/label operands are classified before pathname expansion, so only actual file operands are globbed.

### Class B utilities with slot-aware in-process globbing

Class B uses the same pathname syntax, leading-dot rule, platform case behavior, symbolic-link traversal policy, and literal-preservation rules as Class A, but it preserves the command's syntactic arity. A singular pathname slot is expanded independently: a literal operand remains literal, an unmatched pattern remains literal, exactly one match replaces the pattern, and more than one match is an error. Matches from one singular slot never spill into another argument position.

Some Class B commands are mode-aware. An argument position is eligible only when the command grammar has already identified it as an existing-path input. Data operands, destinations, names being created, symbolic-link payload text, and option values remain literal unless a command explicitly documents otherwise.

| Area | Utilities |
| --- | --- |
| Encoded-data input | `base32`, `base64`, `basenc` |
| Fixed-arity file comparison | `comm`, `join` |
| Splitting, filtering, indexing, and ordering | `csplit`, `split`, `uniq`, `ptx`, `shuf`, `tsort` |
| Link and name operations | `ln`, `link`, `unlink` |
| Configuration and accounting input | `dircolors`, `users`, `who` |

The following qualifications are part of the Class B contract:

- `base32`, `base64`, `basenc`, and `tsort` singular-expand their optional input `FILE`; `-` remains standard input.
- `comm` and `join` expand `FILE1` and `FILE2` independently. Each slot may resolve to exactly one pathname, but expansion never flattens the two slots into a shared operand list.
- `csplit` expands only its initial input `FILE`; every following `PATTERN` remains command-language syntax.
- `split` expands only its input `FILE`; output `PREFIX` remains literal. `uniq` likewise expands only `INPUT`; `OUTPUT` remains literal.
- `ptx` uses collection expansion for GNU-extension input operands. Traditional `[INPUT [OUTPUT]]` mode singular-expands `INPUT` and leaves `OUTPUT` literal. Break/ignore/only parameter-file option values remain literal.
- `shuf` singular-expands its positional `FILE` only in ordinary file mode. `--echo` operands are data, `--input-range` has no pathname operand, and `--output` and `--random-source` values remain literal.
- `ln` never expands symbolic-link targets. Hard-link sources use collection expansion only when the already-selected grammar targets a directory; otherwise the source is a singular slot. Destination names and target-directory operands remain literal.
- `link` singular-expands existing source `FILE1` while creation name `FILE2` remains literal. `unlink` singular-expands its one pathname and rejects multiple matches before attempting removal.
- `dircolors` and `users` singular-expand their optional input `FILE`. `who` singular-expands only the one-operand accounting-file form; the traditional two-operand form remains literal control syntax.

### Utilities in which CommandFramework globbing will not apply

The following utilities will **not** perform `Icod.CommandFramework` pathname expansion. This does not prevent an invoking shell from expanding a pattern before launching the utility; it means the utility itself will not reinterpret wildcard-bearing arguments as filesystem glob patterns.

| Area | Utilities |
| --- | --- |
| Host, process, environment, and identity information | `arch`, `groups`, `hostid`, `hostname`, `id`, `logname`, `nproc`, `pinky`, `printenv`, `pwd`, `tty`, `uname`, `whoami` |
| Numeric, data, and string operations | `echo`, `expr`, `factor`, `numfmt`, `printf`, `seq`, `sleep`, `tr`, `yes` |
| Pure status commands | `false`, `true` |
| Creation and template commands | `mkdir`, `mkfifo`, `mknod`, `mktemp` |
| Lexical and destination pathname grammars | `basename`, `dirname`, `pathchk`, `tee` |
| Singular control and option-file grammars | `chroot`, `date`, `stty` |
| Command wrappers and executors | `env`, `nice`, `nohup`, `runcon`, `stdbuf`, `timeout` |
| Special command grammars | `dd`, `test` |
| Multicall dispatcher | `coreutils` |

This exclusion is intentional. Creation-oriented utilities must preserve the names they are asked to create. Data- and expression-oriented utilities may legitimately receive `*`, `?`, or `**` as ordinary text. `basename` and `dirname` operate lexically on supplied pathname-shaped strings, while `pathchk` examines the pathname spelling itself. `tee` operands are output destinations. `chroot` keeps its process-root boundary explicit, and the path-valued arguments accepted by `date` and `stty` are option/control values rather than general input pathname operands. Wrapper utilities must pass the child command and its arguments through without reinterpreting them. `dd` and `test` have command grammars in which automatic argv expansion would alter the meaning of the command. The `coreutils` multicall dispatcher likewise leaves pathname policy to the selected utility rather than applying globbing itself.

The Class A, Class B, and no-internal-globbing tables above define the pathname-expansion policy for the current command suite. New utilities or new operand forms must choose their pathname class explicitly rather than inheriting globbing merely because an argument happens to contain wildcard characters.
