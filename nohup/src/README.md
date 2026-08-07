# nohup implementation

`Icod.CoreUtils.Nohup` follows GNU Coreutils 9.11. The command owns terminal-driven redirection policy, `nohup.out`/`$HOME/nohup.out` selection, diagnostics, and SIGHUP launch policy. Completion Gate F4 remains responsible for executable lookup, argument-safe launch, inherited environment transfer, asynchronous stream forwarding, child identity, cancellation, and termination translation.

The output-file and inherited-standard-stream providers are injectable so tests can exercise current-directory failure, `$HOME` fallback, and GNU's closed-stdout/terminal-stderr rule without mutating process-global state. When stdout is closed but stderr is a terminal, the command temporarily reserves descriptor 1 while opening `nohup.out`, then closes descriptor 1 again before launch so only stderr is redirected. Newly created output files are forced to exact user read/write mode (`0600`) on POSIX hosts regardless of umask, and the native append flag is enabled after creation. Windows has no POSIX SIGHUP, so the command applies no fictitious signal disposition there while retaining the applicable redirection and execution behavior.

On POSIX, terminal standard input is inherited by the child as write-only `/dev/null`, matching GNU's deliberate unreadable-input extension. On Windows the controlled substitution closes redirected input and therefore presents end-of-file.
