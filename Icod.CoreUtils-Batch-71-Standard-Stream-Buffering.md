# Batch 71 — Standard-stream buffering control

## Decision

Batch 71 replaces the previous best-effort `stdbuf` wrapper with a real implementation boundary.

GNU `stdbuf` does not change another process's buffering through pipes or ordinary process-launch APIs. It injects a shared library before the child starts, and that library uses a constructor to call `setvbuf(3)` on the child's C `stdin`, `stdout`, and `stderr` streams. GNU Coreutils requires the platform's preload and constructor mechanisms for `stdbuf`. A managed-only pass-through cannot provide those semantics honestly.

The Icod implementation therefore supports active buffering control on Linux ELF and macOS Mach-O targets through a repository-owned native preload shim and returns controlled status 125 on Windows or a supported POSIX installation where that shim is unavailable. Help, version, and command-line validation remain portable on every host. The command never shells out to an installed system `stdbuf` and never silently launches a child without the requested buffering changes.

Primary GNU references used for the contract:

- GNU Coreutils 9.11 `src/stdbuf.c`: https://github.com/coreutils/coreutils/blob/v9.11/src/stdbuf.c
- GNU Coreutils 9.11 `src/libstdbuf.c`: https://github.com/coreutils/coreutils/blob/v9.11/src/libstdbuf.c
- GNU Coreutils manual, `stdbuf` invocation: https://www.gnu.org/software/coreutils/manual/html_node/stdbuf-invocation.html

## Managed command layer

`Icod.CoreUtils.StdBuf.Command` now:

- accepts `-i`/`--input`, `-o`/`--output`, and `-e`/`--error` and stops option processing at the first command operand;
- accepts `L` for line buffering except on standard input, `0` for unbuffered operation, and GNU-style size suffixes (`K`, `KB`, `KiB`, and corresponding larger prefixes through `Q`);
- canonicalizes numeric modes to decimal bytes before exporting `_STDBUF_I`, `_STDBUF_O`, and `_STDBUF_E`;
- appends the owned shim to an inherited `LD_PRELOAD` on Linux or `DYLD_INSERT_LIBRARIES` on macOS rather than discarding an existing preload list;
- sets `DYLD_FORCE_FLAT_NAMESPACE=y` for Darwin children, matching GNU Coreutils 9.11;
- uses `IProcessExecutor` / `SystemProcessExecutor` for argument-safe child startup with inherited standard streams;
- preserves the shared process layer's GNU-facing launch status mapping: 125 for internal/setup failure, 126 when a command is found but cannot be invoked, 127 when it cannot be found, and otherwise the child's termination status;
- reports unsupported preload semantics before attempting child launch.

`IStdBufPlatform` is injectable so command behavior can be tested without depending on the test runner's native loader. `SystemStdBufPlatform` exposes Linux `LD_PRELOAD` using `libicodstdbuf.so` and macOS `DYLD_INSERT_LIBRARIES` using `libicodstdbuf.dylib`; architecture-qualified package assets are preferred with the host-built unqualified shim retained as a local-build fallback.

## Native preload shim

`stdbuf/native/icod_stdbuf.c` is intentionally narrow native plumbing. The project builds it as `libicodstdbuf.so` on Linux with `cc -shared -fPIC` and as `libicodstdbuf.dylib` on macOS with `cc -dynamiclib`; Windows does not compile or ship the shim.

The shim constructor runs before the child program's `main` routine and applies standard error first so later setup diagnostics use the requested error buffering. It then applies standard input and standard output. Line and unbuffered modes call `setvbuf` directly. Fully buffered modes allocate the requested buffer and pass that storage to `setvbuf`, which avoids the glibc behavior where a nonzero size paired with a null buffer may be treated only as a hint.

As with GNU `stdbuf`, the result applies only to programs that use ISO C `FILE*` streams and do not subsequently replace their own buffering policy. Programs performing raw descriptor I/O, statically linked programs, secure-execution cases where the loader suppresses preload environment variables, and programs that later call `setvbuf` are outside what preload-based `stdbuf` can force.

## Tests

`tests/StdBuf.Tests` covers:

- help without native capability;
- missing operands and missing buffering options;
- rejection of line-buffered standard input;
- GNU decimal/binary size suffix normalization;
- last-option-wins behavior;
- option termination and literal child argument preservation;
- preservation/appending of an inherited preload list;
- Darwin flat-namespace environment propagation;
- controlled unsupported behavior without child launch;
- 126/127 launch-status propagation;
- Linux and macOS integration using a tiny POSIX probe compiled for the test run. The probe redirects the already-configured C `stdout` stream to a nonblocking pipe and observes that a byte remains buffered until a newline, proving that the preloaded constructor changed the actual stream to line-buffered mode without relying on libc-private inspection APIs.

## Validation state

The native C shim and Linux probe compile cleanly with `cc -std=c11 -Wall -Wextra -Werror`, and a direct preload smoke check reports line buffering as enabled after `_STDBUF_O=L`. The current execution environment does not contain the .NET SDK, so full `dotnet build` / xUnit execution and the required `windows-latest`, `ubuntu-latest`, and `macos-latest` runner pass remain the repository-validation closure step.
