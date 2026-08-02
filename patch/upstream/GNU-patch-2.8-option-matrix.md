# GNU patch 2.8 option and invocation matrix

This file is the pinned option inventory for the co-resident `Icod.Patch` implementation.
It was generated from GNU patch 2.8's `src/patch.c`, installed help text, and upstream test inventory.
It prevents later phases from silently adopting spellings or behavior from a different GNU patch release.

The inventory is complete for the options declared by the 2.8 source. The implementation-status column is intentionally progressive: P1 accepts only the source-selection and informational options owned by that phase; later options remain rejected until their owning phase implements their behavior and tests.

## Source evidence

- GNU release archive: `patch-2.8.tar.xz`
- Release tag: `v2.8`
- Source option declarations: `src/patch.c`, `shortopts`, `longopts`, `option_help`, and `get_some_switches`
- Source browser: <https://sources.debian.org/src/patch/2.8-2/src/patch.c/>
- Upstream test inventory: <https://sources.debian.org/src/patch/2.8-2/tests/>
- Installed manual source: <https://sources.debian.org/src/patch/2.8-2/patch.man/>

The Debian source browser is used only as a navigable rendering of the pinned 2.8 source. The signed GNU release archive recorded in [`GNU-patch-2.8.md`](GNU-patch-2.8.md) remains authoritative.

## Invocation contract

```text
patch [OPTION]... [ORIGFILE [PATCHFILE]]
```

- With no `PATCHFILE`, patch text is read from standard input.
- `-i PATCHFILE` / `--input=PATCHFILE` selects the patch stream and conflicts with the second operand.
- `ORIGFILE` supplies an explicit target filename candidate; complete filename behavior belongs to P7.
- More than two operands are usage trouble.
- GNU status classes are `0` success, `1` rejected/conflicted work, and `2` serious trouble.

## Complete option inventory

| Short form | Long form | Argument | GNU patch 2.8 behavior | Planned owner | Current state after P0-P2 |
|---|---|---|---|---|---|
| `-b` | `--backup` | none | Back up each original file. GNU 2.8 also retains a narrowly triggered obsolete `-b SUFFIX ORIGFILE PATCHFILE` compatibility form and warns in favor of `-b -z SUFFIX`. | P8 | Reserved; rejected until P8. |
| `-B PREFIX` | `--prefix=PREFIX` | required, nonempty | Prefix backup filenames. | P8 | Reserved; rejected until P8. |
| `-c` | `--context` | none | Force interpretation as context diff. | P3 | Reserved; rejected until P3. |
| `-d DIR` | `--directory=DIR` | required | Change working directory before processing. | P7 | Reserved; rejected until P7/E2. |
| `-D NAME` | `--ifdef=NAME` | required | Emit merged if/then/else output using `NAME`. | P8 | Reserved; rejected until P8. |
| `-e` | `--ed` | none | Force interpretation as an ed script. | P4 | Reserved; rejected until P4. |
| `-E` | `--remove-empty-files` | none | Remove output files left empty after patching. | P8/P9 | Reserved; rejected until filesystem policy exists. |
| `-f` | `--force` | none | Noninteractive policy that ignores bad prerequisites and assumes unreversed input. | P6 | Reserved; rejected until P6. |
| `-F LINES` | `--fuzz=LINES` | required, nonnegative integer | Set maximum context fuzz. | P6 | Reserved; rejected until P6. |
| `-g NUM` | `--get=NUM` | required, signed integer | Retrieve files from version control when positive; ask when negative. | P7 | Reserved; rejected until P7. |
| `-i PATCHFILE` | `--input=PATCHFILE` | required | Read patch text from `PATCHFILE` instead of standard input. `-` denotes standard input. | P1 | Implemented. |
| `-l` | `--ignore-whitespace` | none | Canonicalize whitespace while matching patch context to input. | P6 | Reserved; rejected until P6. |
| `-m` | `--merge[=STYLE]` | short form has no argument; long argument optional | Merge conflicts instead of producing rejects. Supported styles are `merge` and `diff3`; no style selects `merge`. Present when GNU patch is built with merge support. | P6/P8 | Reserved; rejected until matching and artifact policy exist. |
| `-n` | `--normal` | none | Force interpretation as normal diff. | P4 | Reserved; rejected until P4. |
| `-N` | `--forward` | none | Ignore input that appears reversed or already applied. | P6 | Reserved; rejected until P6. |
| `-o FILE` | `--output=FILE` | required | Write patched output to `FILE`. | P8 | Reserved; rejected until P8. |
| `-p NUM` | `--strip=NUM` | required, nonnegative integer | Strip `NUM` leading pathname components. | P7 | Reserved; rejected until P7/E2. |
| `-r FILE` | `--reject-file=FILE` | required | Write rejected hunks to `FILE`. | P8 | Reserved; rejected until P8. |
| `-R` | `--reverse` | none | Assume old and new sides were swapped. | P6 | Reserved; rejected until P6. |
| `-s` | `--quiet`, `--silent` | none | Suppress normal output while retaining errors. | P8 | Reserved; rejected until P8. |
| `-t` | `--batch` | none | Ask no questions, skip bad prerequisites, and assume reversed input when needed. | P6/P8 | Reserved; rejected until policy and prompts exist. |
| `-T` | `--set-time` | none | Set output timestamps, interpreting diff timestamps as local time. | P8/E3 | Reserved; rejected until P8/E3. |
| `-u` | `--unified` | none | Force interpretation as unified diff. | P3 | Reserved; rejected until P3. |
| `-v` | `--version` | none | Print version information and exit successfully. | P1 | Implemented. |
| `-V STYLE` | `--version-control=STYLE` | required | Select backup version control. Help names `simple`, `numbered`, and `existing`. | P8 | Reserved; rejected until P8. |
| `-x NUM` | `--debug=NUM` | required, signed integer | Internal debugging flags. The option is declared in the source table, but handling is compiled only with `DEBUGGING`; normal help omits it. | P12 | Not part of the normal release surface unless a deliberate debug-build policy is adopted. |
| `-Y PREFIX` | `--basename-prefix=PREFIX` | required, nonempty | Prefix only backup basenames. | P8 | Reserved; rejected until P8. |
| `-z SUFFIX` | `--suffix=SUFFIX` | required, nonempty | Append a suffix to backup filenames. | P8 | Reserved; rejected until P8. |
| `-Z` | `--set-utc` | none | Set output timestamps, interpreting diff timestamps as UTC. | P8/E3 | Reserved; rejected until P8/E3. |
| — | `--dry-run` | none | Report what would happen without changing files. | P8 | Reserved; rejected until P8. |
| — | `--verbose` | none | Emit additional progress information. | P8 | Reserved; rejected until P8. |
| — | `--binary` | none | Preserve binary transfer behavior and do not strip trailing carriage returns from input records. | P1/P2 | Accepted; P2 is byte-preserving on every platform. |
| — | `--help` | none | Print help and exit successfully. | P1 | Implemented. |
| — | `--backup-if-mismatch` | none | Back up when a patch is not an exact match. | P8 | Reserved; rejected until P8. |
| — | `--no-backup-if-mismatch` | none | Do not create mismatch backups unless another option requests backups. | P8 | Reserved; rejected until P8. |
| — | `--posix` | none | Select GNU patch's POSIX-conformance policy. | P12 | Reserved; rejected until the complete policy is implemented. |
| — | `--quoting-style=WORD` | required | Select filename diagnostic quoting: `literal`, `shell`, `shell-always`, `c`, or `escape`. | P7/P8 | Reserved; rejected until filename and diagnostic policy are complete. |
| — | `--reject-format=FORMAT` | required | Select `context` or `unified` reject output. | P8 | Reserved; rejected until P8. |
| — | `--read-only=BEHAVIOR` | required | Handle read-only inputs using `ignore`, `warn`, or `fail`. | P8/P9 | Reserved; rejected until E3/E4-backed mutation policy exists. |
| — | `--follow-symlinks` | none | Follow symbolic links when opening targets. It is source-defined but omitted from GNU 2.8's ordinary help array. | P9/P10 | Reserved; rejected until the E2/E4 path and no-follow contracts exist. |

## Aliases, conditional surface, and compatibility traps

1. `-b` means **backup**. Binary mode is the long-only `--binary` option.
2. `--quiet` and `--silent` are aliases for `-s`.
3. `--merge` and `-m` are conditional on GNU patch's merge build feature; only the long form can carry the optional style argument.
4. `-x` / `--debug` is declared by the source but is useful only when the `DEBUGGING` case is compiled. It is not advertised by normal help.
5. `--follow-symlinks` is accepted by the source but not advertised by normal help. Icod.Patch must not expose it before the shared link/path policy is available.
6. Long-option abbreviations are accepted by GNU `getopt_long` when unambiguous. P1 resolves abbreviations against the complete GNU 2.8 option-name inventory; an option owned by a later phase is then rejected explicitly rather than disappearing from the ambiguity set.
7. Exact GNU option conflicts, repetition behavior, environment-variable interactions, prompts, diagnostics, and platform-dependent effects remain conformance work for each owning phase and final P12 closure.

## Upstream test map

The complete 2.8 test inventory is retained as research evidence. The most directly relevant upstream tests are:

| Contract area | GNU 2.8 tests |
|---|---|
| Invocation and operands | `bad-usage`, `inname`, `need-filename` |
| Source scanning and garbage | `garbage`, `corrupt-patch`, `mixed-patch-types`, `unusual-blanks` |
| Line endings and incomplete records | `crlf-handling`, `no-newline-triggers-assert` |
| Numeric hardening | `line-numbers`, `mangled-numbers-abort` |
| Filename parsing and safety | `bad-filenames`, `filename-choice`, `quoted-filenames`, `deep-directories` |
| Context, normal, and ed syntax | `context-format`, `munged-context-format`, `ed-style`, `mixed-patch-types` |
| Backups and rejects | `backup-prefix-suffix`, `no-backup`, `remember-backup-files`, `global-reject-files`, `reject-format`, `corrupt-reject-files` |
| Merge and direction | `merge`, `false-match`, `criss-cross` |
| Creation, deletion, modes, and timestamps | `create-delete`, `empty-files`, `file-create-modes`, `file-modes`, `preserve-mode-and-timestamp`, `unmodified-files` |
| Links and special files | `symlinks`, `hardlinks`, `fifo` |

P0-P2 use a small provenance-separated corpus to establish the source model. P3-P12 progressively port or independently reproduce the applicable behavioral cases without shelling out to a locally installed GNU `patch` during ordinary tests.
