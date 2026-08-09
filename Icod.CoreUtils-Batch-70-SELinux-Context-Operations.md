# Batch 70 — SELinux context operations

## Scope

Batch 70 replaces the `chcon` host-command wrapper and the `runcon` `NotImplementedException` seed with native SELinux implementations for the pinned GNU Coreutils profile.

The production path does **not** invoke an installed `chcon`, `runcon`, shell, or equivalent host utility. Native behavior is isolated behind `Icod.CoreUtils.Shared.Platform.ISelinuxPlatform` so command semantics can be exercised without changing the test runner's security context.

## Shared SELinux boundary

`NativeSelinuxPlatform` is Linux-only and probes for `libselinux.so.1`. It binds:

- `is_selinux_enabled`;
- `getcon`;
- `getfilecon` / `lgetfilecon`;
- `setfilecon` / `lsetfilecon`;
- `security_check_context`;
- `security_compute_create` with the `process` security class;
- `setexeccon`;
- `freecon`.

`runcon` execution uses libc `execvp` for ordinary modes and `execv` after `--compute`, matching GNU Coreutils. Once `setexeccon` succeeds, a successful exec replaces the current process image. No shell command string is constructed, so argument boundaries are retained exactly. An `execvp` failure maps ENOENT to status 127 and other execution failures to 126; failures in `runcon` itself return 125.

`SelinuxContext` parses the conventional `user:role:type[:range]` structure while preserving the remainder of an MLS/MCS range verbatim, including embedded colons.

## `chcon`

Implemented modes and policies:

- complete `CONTEXT FILE...` mode;
- `--reference=RFILE FILE...` mode;
- partial `-u/--user`, `-r/--role`, `-t/--type`, and `-l/--range` edits;
- `--dereference` and `-h/--no-dereference`;
- `-R/--recursive`;
- `-H`, `-L`, and `-P` traversal policy;
- GNU-invalid recursive combinations are rejected before mutation (`-R --dereference -P`, and `-R -h` with `-H`/`-L`);
- `--preserve-root` and `--no-preserve-root`;
- `-v/--verbose`;
- per-operand error aggregation;
- context validation before mutation;
- explicit diagnostics for unlabeled files when partial context editing is requested.

For recursive work, the default context operation is non-dereferencing and traversal is physical (`-P`). `chcon` delegates recursion to the existing `ReadOnlyPathTraversalEngine`: `-P`, `-H`, and `-L` map to its `Never`, `RootsOnly`, and `Always` link policies, respectively, and directory contexts are applied from `LeaveDirectory` events so mutation remains post-order. The shared traversal layer supplies stable-identity cycle detection and the repository’s existing junction/reparse-point observation policy instead of duplicating filesystem walking inside the command.

## `runcon`

Implemented modes:

- no operands: print the current process security context;
- `CONTEXT COMMAND [ARG]...`: execute under a complete context;
- component mode: start with the current context and replace selected user/role/type/range fields;
- `-c/--compute`: read the executable context, compute the SELinux process transition, then apply any requested component overrides;
- duplicate component specifiers are rejected;
- the final context is validated before `setexeccon`;
- the command vector is passed directly to `execvp` in ordinary modes and `execv` after `--compute`, with no interpolation.

## Unsupported and privilege behavior

`--help` and `--version` remain portable. Operational requests fail cleanly when the host is not Linux, `libselinux.so.1` cannot be loaded, SELinux is disabled, the policy rejects a context, or native context/privilege operations fail. Native errno information is surfaced through the provider instead of throwing an unhandled platform exception.

## Tests

Two test projects are added under the solution `tests` folder:

- `Icod.CoreUtils.ChCon.Tests`;
- `Icod.CoreUtils.RunCon.Tests`.

They use a fake `ISelinuxPlatform` to test parser/context semantics, reference and component behavior, dereference/recursive policy, post-order directory application, informational-option termination, literal command-vector preservation, computed transition ordering and `execv`/`execvp` selection, launcher statuses, and controlled unsupported-host diagnostics without requiring SELinux privileges on CI runners.

## Validation status

Source-level implementation and test fixtures are complete. The execution environment used to prepare Batch 70 does not contain the .NET SDK, so no local `dotnet build` or `dotnet test` claim is made here. Full solution validation on `windows-latest`, `ubuntu-latest`, and `macos-latest` remains the Batch 70 closure step.
