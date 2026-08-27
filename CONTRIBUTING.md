# Contributing to Icod.CoreUtils

Thank you for contributing to Icod.CoreUtils. This repository contains the managed GNU Coreutils/Fileutils/Textutils implementation and its repository-local support infrastructure. All sibling suites formerly incubated here—including `Icod.UtilLinux`, `Icod.Grep`, `Icod.Tar`, `Icod.ProcPs`, `Icod.DiffUtils`, `Icod.LineEditor`, and `Icod.Patch`—have completed Gate G extraction and belong in their dedicated repositories. They must not be reintroduced here as source-tree dependencies. Changes should preserve command compatibility, cross-platform behavior, and the repository's shared architectural boundaries.

## Supported toolchain

- Target framework: `net10.0`.
- Language version: C# 13, declared as `<LangVersion>13.0</LangVersion>` in every project.
- Nullable reference types and implicit global usings remain enabled where the existing project enables them.
- Supported CI runners are `windows-latest`, `ubuntu-latest`, and `macos-latest`; best-effort BSD portability is also a project goal.
- The repository `.editorconfig` is authoritative for text formatting. Its current policy is UTF-8, CRLF line endings, and no required final newline; configure editors to honor it rather than imposing a separate line-ending convention.
- Runtime command output should use `Environment.NewLine` unless the command contract requires a byte delimiter or preserves input record terminators.
- Literal newline escapes such as `\n` and `\r\n` are permitted only when they are part of the utility’s data semantics, escape grammar, or documented byte transformation. They are never used as the host platform’s generated line separator.
- Generated line endings use `WriteLine`, `WriteLineAsync`, or `Environment.NewLine`. Line-oriented input uses `ReadLine`, `ReadLineAsync`, and `Environment.NewLine` as appropriate. Code must not hard-code `\n` or `\r\n` for host line-reading or line-writing semantics.
- When multiple strings are sent to `WriteAsync`, `WriteLineAsync`, or related output methods, combine them with `System.String.Concat` rather than the `+` operator.

Do not change the target framework, language version, configuration policy, signing policy, or repository line-ending convention in an unrelated contribution.

## Repository architecture

Each retained command is a standalone Coreutils/Fileutils/Textutils executable project. Extracted sibling-suite commands are no longer part of this repository.

Examples include:

- `Icod.CoreUtils.Shared` for repository-local behavior shared by GNU Coreutils/Fileutils/Textutils commands; it is not an independently published package;
- `Icod.CommandFramework` for published neutral cross-suite command, process, terminal, text, and filesystem mechanism;
- `Icod.Path` for published neutral canonical-path behavior.

Extracted suite families belong in their dedicated repositories. In particular, `Icod.UtilLinux` lives at <https://github.com/uniblab/Icod.UtilLinux>, `Icod.Grep` at <https://github.com/uniblab/Icod.Grep>, `Icod.Tar` at <https://github.com/uniblab/Icod.Tar>, `Icod.ProcPs` at <https://github.com/uniblab/Icod.ProcPs>, `Icod.DiffUtils` at <https://github.com/uniblab/Icod.DiffUtils>, `Icod.LineEditor` at <https://github.com/uniblab/Icod.LineEditor>, and `Icod.Patch` at <https://github.com/uniblab/Icod.Patch>. CoreUtils contributions must not recreate those source trees or introduce runtime dependencies on sibling suites.

Genuine Coreutils/Fileutils/Textutils projects that need suite-shared behavior use a same-repository `ProjectReference` to `Icod.CoreUtils.Shared`. Never add a `PackageReference` to `Icod.CoreUtils.Shared`.

Production command projects must not reference sibling command projects. Shared behavior belongs in the appropriate suite-local or neutral library only after a real cross-command contract has been identified. Do not create command-local replacements for an existing shared pathname, filesystem, record, regex, metadata, or transaction model.

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
- FIFO and device-node commands must use the E4 mutation provider. Unsupported or privilege-limited hosts must receive controlled failures, and special files must never be emulated with ordinary files.

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
dotnet clean Icod.CoreUtils.sln -c Debug
dotnet restore Icod.CoreUtils.sln
dotnet build Icod.CoreUtils.sln -c Debug --no-restore
dotnet test Icod.CoreUtils.sln -c Debug --no-build
```

Also validate Release when the change is intended for completion or merge:

```text
dotnet clean Icod.CoreUtils.sln -c Release
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
