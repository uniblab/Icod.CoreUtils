# Batch 52 — Environment and hangup-independent execution

## Scope

Batch 52 brings `env` and `nohup` to the GNU Coreutils 9.11 command profile on top of Completion Gate F4. Command projects own GNU option grammar, policy, diagnostics, and exit-status decisions; the current Shared incubation project continues to own cross-suite process environment, lookup, launch, stream, signal, cancellation, and termination mechanics.

## `env`

`Icod.CoreUtils.Env` now implements inherited or empty environments, `NAME=VALUE` assignments, `-u` removal, a bare `-` alias for `-i`, NUL-delimited environment output, `-C` working directories, `-a` native argument zero, signal disposition and mask launch policy, signal-policy reporting (including inherited Linux blocked masks), debug diagnostics, command lookup, and GNU 125/126/127 status boundaries.

`-S` is parsed without a shell. Its quote, escape, comment, `\\_`, `\\c`, and `${VARNAME}` rules are command-local because they are GNU `env` syntax rather than a general command framework. Variable expansion reads the original environment before `-i`, `-u`, or assignments, matching the upstream sequencing.

The Shared executor gained a narrow POSIX `posix_spawn` path for a true independent native `argv[0]`; the command-line `env` path also uses it with the original command spelling as `argv[0]`, which preserves native environment-vector edge cases. Programmatic launches that inject managed streams continue through `ProcessStartInfo.ArgumentList` unless `-a` requires the native path. Launch-time signal policy is applied only around child creation and the managed host state is restored immediately. Windows reports explicit `argv[0]` and POSIX launch-signal requirements through the existing controlled setup-failure boundary instead of silently rewriting them.

## `nohup`

`Icod.CoreUtils.Nohup` inspects the three standard streams through the Shared terminal provider. Terminal standard output appends to `nohup.out`, falling back to `$HOME/nohup.out`; terminal standard error follows standard output. When inherited standard output is closed and standard error is a terminal, the error stream alone appends to `nohup.out` while descriptor 1 remains closed for the child. Terminal standard input is disconnected from the caller. Newly created POSIX output files are forced to exact user read/write permissions (`0600`) independently of the caller umask, and the native append flag is enabled so concurrent writes retain append semantics. The output-file provider is injectable so current-directory failure and HOME fallback are testable without process-global directory changes.

On POSIX, the child is launched with SIGHUP ignored through the F4 launch-signal policy. Windows has no POSIX SIGHUP, so no fictitious signal is installed there; applicable stream redirection and execution semantics remain available. Caller cancellation uses the F4 leave-running policy so cancellation of the wrapper does not deliberately destroy the launched child.

GNU Coreutils deliberately opens `/dev/null` write-only when terminal input must be disabled so a child read fails. On POSIX, the F4 launch scope now reproduces that behavior by replacing descriptor 0 with a write-only `/dev/null` only for the child-creation window, then restoring the host descriptor immediately. Windows has no equivalent POSIX descriptor contract, so its controlled substitution closes the redirected input pipe and yields end-of-file. No implementation shells to native `nohup`.

## Shared changes

The F4 process layer now additionally exposes:

- launch-time signal disposition, mask, and POSIX descriptor directives, plus Linux blocked-mask observation;
- a true POSIX native argument-zero launch path;
- a public result factory for deterministic command-boundary tests;
- a distinct setup-failure launch class mapped to status 125;
- environment names that follow native `env` rules by rejecting only `=` and NUL rather than rejecting empty or whitespace-only names.

These remain provisional `Icod.CommandFramework` candidates because later Coreutils, util-linux, ProcPs, Tar, and editor consumers share the same mechanics.

## Tests

Dedicated `Env.Tests` coverage exercises ordinary and NUL environment output, clearing/removal/assignment, exact child arguments, `-S` quoting and original-environment expansion, `-C`, `-a`, signal launch policy and inherited blocked-mask reporting, GNU launch-failure statuses, and invalid option combinations.

Dedicated `Nohup.Tests` coverage exercises passthrough streams, terminal redirection, shared stdout/stderr destinations, closed-stdout/terminal-stderr handling, `$HOME` fallback, append preservation, POSIX `0600` creation, `POSIXLY_CORRECT` internal status, leave-running child policy, and GNU launch-failure translation.
