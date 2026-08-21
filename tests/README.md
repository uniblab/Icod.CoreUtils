# Automated tests

This directory is intentionally distinct from `/test`, which implements the Unix `test(1)` command.

`Shared.Tests` verifies the behavior that remains owned by `Icod.CoreUtils.Shared` after Completion Gate G contraction. Neutral command-framework tests live in the independent `Icod.CommandFramework` repository and are not duplicated here.

`ProcessTestHost` is a deliberately small repository-local executable used only by real-child integration tests for Coreutils commands that must launch a deterministic process. At present:

- `Nice.Tests` uses the `exit` behavior to verify propagation of a real child exit status.
- `Timeout.Tests` uses the `sleep` behavior to verify that the system executor can actually bound and terminate a real child process.

The framework repository keeps its own independent process test host for framework-level process-runner tests. Coreutils does not depend on that test project or assembly.

## Test ownership

Tests under `Shared.Tests` cover retained Coreutils-specific shared behavior such as GNU formatting/escape policy, numeric operand grammar, ordering policy, directory-listing policy, filesystem ownership/usage behavior, ranges, tab-stop parsing, and GNU date/time policy.

Command test projects remain responsible for command-visible behavior and command-specific integration. Tests should not write to standard output or standard error except when an inter-process communication test deliberately requires inherited streams.

## Running tests

Run the contracted Shared test project with:

```text
dotnet test tests/Shared.Tests/Icod.CoreUtils.Shared.Tests.csproj -c Debug
```

Run the complete repository build/test sequence with `build.cmd` on Windows or `./build.sh` on Unix-like systems.
