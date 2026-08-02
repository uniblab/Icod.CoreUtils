# Contributing to Icod.CoreUtils

Thank you for contributing to the Icod command-suite ports. The repository contains managed implementations of GNU Coreutils, Fileutils, Textutils, Diffutils, Patch, and related command families. Changes should preserve command compatibility, cross-platform behavior, and the repository's shared architectural boundaries.

## Supported toolchain

- Target framework: `net10.0`.
- Language version: C# 13, declared as `<LangVersion>13.0</LangVersion>` in every project.
- Nullable reference types and implicit global usings remain enabled where the existing project enables them.
- Supported CI runners are `windows-latest`, `ubuntu-latest`, and `macos-latest`; best-effort BSD portability remains a project goal.
- Repository text files use UTF-8 with LF line endings. Runtime command output should use `Environment.NewLine` unless the command contract requires a byte delimiter or preserves input record terminators.

Do not change the target framework, language version, configuration policy, signing policy, or repository line-ending convention in an unrelated contribution.

## Repository architecture

Each command is a standalone executable project. Suite and shared-library names are intentional; do not rename them into a uniform `Icod.CoreUtils.<command>` pattern when the command belongs to another suite.

Examples include:

- `Icod.CoreUtils.Shared` for common Coreutils facilities and provisional cross-suite infrastructure during incubation;
- `Icod.Path` for neutral canonical-path behavior;
- `Icod.DiffUtils.*` for Diffutils commands and shared code;
- `Icod.Patch` for `patch`;
- `Icod.LineEditor.*` for `ed`, `red`, and `sed` work;
- `Icod.ProcPs.*` and other suite-specific projects as scheduled by the roadmap.

Production command projects must not reference sibling command projects. Shared behavior belongs in the appropriate shared or neutral library only after a real cross-command contract has been identified. Do not create command-local replacements for an existing shared pathname, filesystem, record, regex, metadata, or transaction model.

The production implementation must not delegate ordinary behavior to the host command being ported. Native GNU tools may be used only by clearly opt-in differential tests.

## Project and solution conventions

- Keep command projects in their suite solution folder.
- Keep every test project in the top-level `tests` solution folder, not under an individual command's solution folder.
- Preserve the established Debug, Staging, and Release property groups in every `.csproj`.
- Release builds treat warnings as errors except `CS1591` under the current repository policy.
- Preserve the command's executable `AssemblyName`, namespace, and public `Command` facade.
- Add substantive XML documentation to every public, protected, and internal type and member.
- Add or update a directory `README.md` when a source directory contains more than one implementation file.
- Do not introduce a `Directory.Build.props` policy migration as part of an unrelated command change.

When adding a project, add every solution configuration mapping and place it in the correct solution folder.

## C# and command implementation style

Follow the repository `.editorconfig` and the style already used in the surrounding project. In particular:

- use tabs for indentation where the existing files do;
- use PascalCase for types and members and camelCase for locals and parameters;
- use `var` when the assigned type is clear;
- keep nullable flow explicit rather than suppressing warnings casually;
- prefer checked arithmetic and explicit resource limits for untrusted input;
- propagate `CancellationToken` through asynchronous work;
- use TAP-based asynchronous orchestration and retain the established synchronous compatibility wrapper when the command family exposes one;
- pass standard input, standard output, standard error, byte-oriented input, diagnostics, and cancellation through `CommandContext` rather than accessing global console state in the command engine;
- use the shared declarative option parser and shared diagnostic conventions;
- use `ProcessStartInfo.ArgumentList` when an external process is genuinely required; never construct a shell command from untrusted arguments.

Unsupported platform behavior must produce a controlled diagnostic and nonzero status. It must not silently report success or fabricate Unix capabilities.

## Path and filesystem changes

Filesystem work must respect the current E-series completion gates in `Icod.CoreUtils-Audit-and-Refactor-Roadmap.md`.

- Use `Icod.Path` for lexical normalization, physical resolution, link/reparse inspection, roots, volumes, containment, and missing-component policy.
- Use injectable providers for filesystem-dependent logic so POSIX and Windows path behavior can be tested on every runner.
- Preserve no-follow semantics where required and test symbolic links, reparse points, dangling links, loops, and containment escapes.
- Do not claim atomic, transactional, metadata-preserving, or rollback behavior until the corresponding shared contract exists and the active provider can guarantee it.
- Never remove the only recoverable original before a complete replacement is ready.

## Tests

Every behavior change requires focused automated coverage in the command's dedicated test project. Tests should include the relevant combinations of:

- ordinary success and GNU-compatible status classes;
- invalid options, malformed input, and controlled operational failures;
- cancellation and deterministic cleanup;
- LF, CRLF, CR, NUL-delimited, binary, and incomplete-final-record cases where applicable;
- Windows and POSIX pathname grammar through synthetic providers;
- real host links or special files only when capability checks can skip unsupported cases deterministically;
- resource limits and adversarial inputs;
- multiple operands or files and continuation after per-item failure;
- executable/process-host behavior in addition to direct command calls where the public CLI contract is affected.

Keep test workspaces uniquely named and delete only resources owned by that test. Avoid assertions over a global temporary-file namespace that another test assembly may use concurrently.

Fixtures must record provenance. Keep GNU-generated, Icod-generated, independent, malformed, binary, and security fixtures separated where those distinctions matter. Do not generate an expected result with the same implementation being tested. Native-tool differential tests must be opt-in and must not be required for the normal test suite.

Follow the installed xUnit analyzer guidance. Prefer dedicated assertions such as `Assert.Contains`, `Assert.DoesNotContain`, `Assert.Single`, and `Assert.ThrowsAsync` over wrapping the same condition in `Assert.True`.

## Build and validation

From the repository root, restore, build, and test the solution:

```text
dotnet restore Icod.CoreUtils.sln
dotnet build Icod.CoreUtils.sln -c Debug --no-restore
dotnet test Icod.CoreUtils.sln -c Debug --no-build
```

Also validate Release when the change is intended for completion or merge:

```text
dotnet build Icod.CoreUtils.sln -c Release
dotnet test Icod.CoreUtils.sln -c Release --no-build
```

Repository build scripts may be used instead when they cover the same solution-wide steps. Run the focused test project while developing, but do not substitute that for the full solution test run before submitting a pull request.

## Documentation and roadmaps

The repository roadmap governs suite ordering, completion gates, and shared infrastructure. Suite-specific roadmaps govern detailed command behavior. Update the applicable roadmap when completing a scheduled phase, but distinguish clearly among:

- implemented behavior;
- locally validated behavior;
- three-runner CI validation;
- deliberately deferred behavior;
- platform-limited behavior.

Do not mark a gate or phase complete by weakening its tests or by claiming validation that was not run.

## Pull requests and commits

Use a focused branch and keep unrelated formatting or project-policy churn out of the change. A pull request should:

- explain the GNU or suite behavior being implemented;
- identify important compatibility decisions and intentional divergences;
- list added or changed tests;
- report the exact build and test commands run and the platforms used;
- update relevant documentation and roadmap status;
- call out any remaining unsupported or deferred cases.

Use an imperative, present-tense commit subject such as `Implement patch filename selection`. Keep the subject concise and add a body when the compatibility or safety reasoning is not obvious.

For questions or design changes that affect more than one command family, open an issue or discuss the contract before creating another shared abstraction.
