# Completion Gate G — Repository Migration Checklist and Roadmap

## Objective

Completion Gate G converts the present multi-suite incubation repository into the final repository and package architecture without changing command semantics.

The intended dependency model is provisionally:

```text
Icod.Path
    ↓ where canonical-path contracts are required
Icod.CommandFramework
    ↓
suite-specific Shared / engine library, where required
    ↓
individual command projects
```

`Icod.Path` is retained as a separate neutral package. Gate G1 is complete, and CoreUtils consumes the published package rather than a source-tree project.

No command suite may become a production dependency of `Icod.CommandFramework`, and sibling command suites must not acquire runtime dependencies on one another merely because they were developed together.

---

# A. Repository / Project Migration Inventory

## Neutral foundations

- [x] **Icod.Path**
  - `Icod.Path`
  - `Icod.Path.Tests`
  - Destination: independent `Icod.Path` repository and published package.
  - Gate G roadmap should explicitly add this repository decision.

- [x] **Icod.CommandFramework**
  - New class-library project extracted from demonstrated cross-suite portions of the present `Icod.CoreUtils.Shared`.
  - New dedicated test project assembled from the applicable portions of `Shared.Tests`.
  - New independent solution.
  - New independent repository.
  - New versioned NuGet package.
  - No production reference to any command suite.

## Icod.CoreUtils

- [ ] Retain all genuine GNU Coreutils/Fileutils/Textutils command projects.
- [ ] Audit `Icod.CoreUtils.Shared`.
- [ ] Move demonstrated cross-suite APIs from `Icod.CoreUtils.Shared` to `Icod.CommandFramework`.
- [ ] Retain only Coreutils/Fileutils/Textutils-specific behavior in `Icod.CoreUtils.Shared`.
- [ ] Split `Shared.Tests` between framework tests and remaining Coreutils Shared tests.
- [x] Audit `Icod.CoreUtils.ProcessTestHost`.
  - Decision: retain it as a small repository-local test host for Coreutils integration tests.
  - `Nice.Tests` requires only deterministic child exit behavior.
  - `Timeout.Tests` requires only deterministic child sleep behavior.
  - Framework process-runner tests use the independent `Icod.CommandFramework.ProcessTestHost`; Coreutils does not reference that test project.
- [ ] Convert retained Coreutils projects from transitional source-tree references to published package references.
- [ ] Remove every sibling-suite project after successful extraction.
- [ ] Remove stale solution folders, packaging entries, output-path exceptions, CI references, and documentation references.

## Icod.DiffUtils

- [ ] `Icod.DiffUtils.Shared`
- [ ] `Icod.DiffUtils.Cmp`
- [ ] `Icod.DiffUtils.Diff`
- [ ] `Icod.DiffUtils.Diff3`
- [ ] `Icod.DiffUtils.SDiff`
- [ ] `Icod.DiffUtils.Shared.Tests`
- [ ] `Icod.DiffUtils.Cmp.Tests`
- [ ] `Icod.DiffUtils.Diff.Tests`
- [ ] `Icod.DiffUtils.Diff3.Tests`
- [ ] `Icod.DiffUtils.SDiff.Tests`
- [ ] Create `Icod.DiffUtils.sln`.
- [ ] Preserve project references from commands to `Icod.DiffUtils.Shared`.
- [ ] Replace the present CoreUtils Shared reference with `Icod.CommandFramework`.
- [ ] Preserve interoperability with Patch and Ed strictly through textual formats/fixtures rather than runtime references.

## Icod.Grep

- [ ] `Icod.Grep`
- [ ] `Icod.Grep.Tests`
- [ ] Create `Icod.Grep.sln`.
- [ ] Replace the present CoreUtils Shared reference with `Icod.CommandFramework`.
- [ ] Keep matcher orchestration, binary-input rules, recursive-selection policy, context grouping, and output formatting in Grep.

## Icod.Patch

- [ ] `Icod.Patch`
- [ ] `Icod.Patch.Tests`
- [ ] Create `Icod.Patch.sln`.
- [ ] Replace the present CoreUtils Shared reference with `Icod.CommandFramework`.
- [ ] Replace the source-tree `Icod.Path` reference with the published `Icod.Path` package.
- [ ] Preserve fixture corpora and transactional/security tests.
- [ ] Keep Diffutils interoperability textual; do not reference `Icod.DiffUtils.Shared`.

## Icod.LineEditor

- [ ] `Icod.LineEditor.Ed.Shared`
- [ ] `Icod.LineEditor.Ed`
- [ ] `Icod.LineEditor.Red`
- [ ] `Icod.LineEditor.Sed`
- [ ] `Icod.LineEditor.Ed.Shared.Tests`
- [ ] `Icod.LineEditor.Ed.Tests`
- [ ] `Icod.LineEditor.Red.Tests`
- [ ] `Icod.LineEditor.Sed.Tests`
- [ ] Create `Icod.LineEditor.sln`.
- [ ] Retain project references from Ed and Red to `Icod.LineEditor.Ed.Shared`.
- [ ] Sed remains a separate execution engine.
- [ ] Do **not** create `Icod.LineEditor.Shared`; the completed sharing audit did not justify it.
- [ ] Replace cross-suite Shared references with `Icod.CommandFramework`.

## Icod.UtilLinux

- [ ] `Icod.UtilLinux.Kill`
- [ ] `Icod.UtilLinux.Renice`
- [ ] `Icod.UtilLinux.Kill.Tests`
- [ ] `Icod.UtilLinux.Renice.Tests`
- [ ] Create `Icod.UtilLinux.sln`.
- [ ] Replace CoreUtils Shared references with `Icod.CommandFramework`.
- [ ] Do not create `Icod.UtilLinux.Shared` unless a genuine suite-local abstraction appears during extraction.

## Icod.Tar

- [ ] `Icod.Tar`
- [ ] `Icod.Tar.Tests`
- [ ] Create `Icod.Tar.sln`.
- [ ] Replace CoreUtils Shared with `Icod.CommandFramework`.
- [ ] Preserve tar-specific archive models, sparse behavior, selection policy, compression integration, and extraction security inside this repository.
- [ ] Preserve native/compression test assets.

## Icod.ProcPs

Current extraction set:

- [ ] `Icod.ProcPs.Shared`
- [ ] `Icod.ProcPs.Uptime`
- [ ] `Icod.ProcPs.Free`
- [ ] `Icod.ProcPs.Vmstat`
- [ ] `Icod.ProcPs.Pgrep`
- [ ] `Icod.ProcPs.Pkill`
- [ ] `Icod.ProcPs.PidWait`
- [ ] `Icod.ProcPs.PidOf`
- [ ] `Icod.ProcPs.Pwdx`
- [ ] `Icod.ProcPs.Pmap`
- [ ] `Icod.ProcPs.Ps`
- [ ] `Icod.ProcPs.W`
- [ ] `Icod.ProcPs.Sysctl`
- [ ] `Icod.ProcPs.Tload`
- [ ] `Icod.ProcPs.Watch`
- [ ] `Icod.ProcPs.HugeTop`
- [ ] `Icod.ProcPs.SlabTop`
- [ ] Corresponding command test projects.
- [ ] `Icod.ProcPs.Shared.Tests`
- [ ] Create `Icod.ProcPs.sln`.
- [ ] Replace CoreUtils Shared with `Icod.CommandFramework`.
- [ ] Preserve project references from commands to `Icod.ProcPs.Shared`.
- [ ] Retain `/proc`, process-domain, field-catalog, selection, sampling, and screen-model behavior in ProcPs Shared.
- [ ] After extraction succeeds, implement deferred `Icod.ProcPs.Top`.
- [ ] Add `Icod.ProcPs.Top.Tests`.
- [ ] Confirm procps-ng `kill`, `skill`, and `snice` remain deliberately absent.

---

# B. Gate G Migration Roadmap

## G0 — Freeze and inventory

No projects move during this phase.

- [x] Identify the intended final repositories.
- [x] Identify the existing suite-specific Shared libraries.
- [x] Confirm that no general `Icod.LineEditor.Shared` is currently justified.
- [x] Identify `Icod.Path` as an unresolved Gate G repository/package item.
- [ ] Update the living-status header: Batch 72 is validated/merged and Completion Gate G is active.
- [ ] Add `Icod.Path` explicitly to the Gate G checklist.
- [ ] Generate a machine-readable inventory of every `.csproj`.
- [ ] Generate the complete `ProjectReference` dependency graph.
- [ ] Inventory every public/protected/internal member of `Icod.CoreUtils.Shared`.
- [ ] Record every consumer of every Shared API by project **and suite**.
- [ ] Perform the same inventory for:
  - `Icod.DiffUtils.Shared`;
  - `Icod.LineEditor.Ed.Shared`;
  - `Icod.ProcPs.Shared`;
  - `Icod.Path`;
  - other reusable engine boundaries.
- [ ] Mark every API:
  - `Framework`;
  - `CoreUtils.Shared`;
  - suite-specific;
  - command-local;
  - obsolete/duplicate.
- [ ] Detect APIs whose public signatures expose types belonging to another proposed package.
- [ ] Detect circular package dependencies before moving source.

**Exit criterion:** every shared API has a proposed permanent owner and every existing project reference has a proposed replacement.

## G1 — Freeze `Icod.Path` — COMPLETE

- [x] Audit its public API and actual consumers.
- [x] Confirm that it remains an independent neutral package.
- [x] Freeze namespace and package surface.
- [x] Establish package versioning, symbols, deterministic builds, README, license, and CI.
- [x] Extract the repository.
- [x] Publish the versioned package; `Icod.Path` 1.0.0 is the current CoreUtils dependency.
- [x] Convert in-tree consumers to the package and validate the split.

**Exit criterion met:** canonical-path behavior is consumed without a source-tree `Icod.Path` project reference.
## G2 — Extract `Icod.CommandFramework` — COMPLETE

- [x] Create the independent repository and solution.
- [x] Move only APIs demonstrated to have independent-suite consumers.
- [x] Move/split their tests out of `Icod.CoreUtils.Shared.Tests`.
- [x] Remove Coreutils-specific names and assumptions from the framework public API.
- [x] Audit accessibility and XML documentation during extraction.
- [x] Audit native ABI boundaries during extraction and three-platform validation.
- [x] Ensure no command-suite production dependency exists.
- [x] Package with symbols and repository metadata.
- [x] Publish the versioned package; `Icod.CommandFramework` 1.0.0 is the current CoreUtils dependency.
- [x] Validate against real CoreUtils and sibling-suite consumers.

**Exit criterion met:** sibling suites can compile against the published framework binary.
## G3 — Contract `Icod.CoreUtils.Shared` — ACTIVE

- [x] Remove every API now owned by `Icod.CommandFramework`.
- [x] Keep only demonstrated Coreutils/Fileutils/Textutils-specific reuse.
- [x] Make it depend on `Icod.CommandFramework`.
- [x] Add the published `Icod.Path` package dependency where genuinely required.
- [x] Split/rehome tests appropriately.
- [ ] Publish `Icod.CoreUtils.Shared`.
- [ ] Convert Coreutils commands to package references.
- [ ] Build/test Coreutils without sibling-suite source projects being needed.

### G3 contraction progress

- [x] **G3A — Text split**
  - move general text consumers to `Icod.CommandFramework.Text`;
  - retain the GNU tab-stop parser policy in `Icod.CoreUtils.Shared.Text`;
  - remove duplicated framework-owned text mechanisms and tests.
- [x] **G3B — Time split**
  - move monotonic-clock and periodic-scheduler consumers to `Icod.CommandFramework.Time`;
  - retain GNU date parsing/formatting and wall-clock mutation policy in `Icod.CoreUtils.Shared.Time`;
  - remove duplicated framework-owned monotonic timing mechanisms and tests.
- [x] **G3C1 — Platform consumer cut-over**
  - move consumers of identity, capability/result, and SELinux contracts to `Icod.CommandFramework.Platform`;
  - retain Coreutils-local login-accounting, process-information, system-information, system-metrics, and user-information providers.
- [x] **G3C2 — Platform implementation excision**
  - remove the duplicated framework-owned Platform implementation and framework-owned Platform tests;
  - contract the Platform README to the retained Coreutils responsibilities.
- [x] **G3D1 — File-mode value-model consumer cut-over**
  - move `PosixFileMode`, `PosixFileModeBits`, and `FileCreationMask` consumers to `Icod.CommandFramework.FileSystem.Modes`;
  - retain GNU mode parsing/expression policy and the Coreutils creation-mask provider in `Icod.CoreUtils.Shared.FileSystem.Modes`.
- [x] **G3D2 — File-mode value-model excision**
  - remove the duplicate CoreUtils `PosixFileMode.cs`;
  - contract the Modes README around the retained GNU/Coreutils policy and framework value model.
- [x] **G3E1 — Metadata consumer cut-over**
  - move every surviving consumer of `Icod.CoreUtils.Shared.FileSystem.Metadata` to `Icod.CommandFramework.FileSystem.Metadata`;
  - leave the duplicate Metadata implementation and its duplicated Shared tests in place only until G3E2.
  - follow-up namespace alignment: because the framework Metadata contracts expose `Icod.CommandFramework.FileSystem.Traversal` identities, surviving Metadata consumers were also aligned to the framework Traversal namespace; the duplicate Traversal implementation/tests remain for a later excision tranche.
- [x] **G3E2 — Metadata implementation/test excision**
  - remove the duplicate CoreUtils Metadata source directory;
  - remove the duplicated Metadata tests from `Icod.CoreUtils.Shared.Tests`.
- [x] **G3F1 — Traversal implementation/test excision**
  - remove the duplicate CoreUtils `FileSystem.Traversal` implementation and its duplicated Shared tests;
  - rely exclusively on `Icod.CommandFramework.FileSystem.Traversal`, whose consumer cut-over was completed during the G3E1 namespace-alignment follow-up.
- [x] **G3G1 — Mutation consumer cut-over**
  - move surviving consumers of `Icod.CoreUtils.Shared.FileSystem.Mutation` to `Icod.CommandFramework.FileSystem.Mutation`;
  - leave the duplicate Mutation implementation and its duplicated Shared tests in place until G3G2.
- [x] **G3G2 — Mutation implementation/test excision**
  - remove the duplicate CoreUtils `FileSystem.Mutation` implementation and its duplicated Shared tests.
- [x] **G3H1 — RecursiveMutation consumer cut-over**
  - move surviving consumers of `Icod.CoreUtils.Shared.FileSystem.RecursiveMutation` to `Icod.CommandFramework.FileSystem.RecursiveMutation`;
  - leave the duplicate RecursiveMutation implementation and its duplicated Shared tests in place until G3H2.
- [x] **G3H2 — RecursiveMutation implementation/test excision**
  - remove the duplicate CoreUtils `FileSystem.RecursiveMutation` implementation and its duplicated Shared tests.
- [x] **G3I1 — TransactionalReplacement consumer cut-over**
  - move surviving consumers of `Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement` to `Icod.CommandFramework.FileSystem.TransactionalReplacement`;
  - leave the duplicate TransactionalReplacement implementation and duplicated Shared tests in place until G3I2.
- [x] **G3I2 — TransactionalReplacement implementation/test excision**
  - remove the duplicate CoreUtils `FileSystem.TransactionalReplacement` implementation and duplicated Shared tests.
- [x] **G3J — Root filesystem operations audit and contraction**
  - compare `FileSystemCapabilities`, `IFileSystemOperations`, and `SystemFileSystemOperations` with the framework package;
  - cut neutral consumers to `Icod.CommandFramework.FileSystem` where contracts match;
  - preserve any Coreutils-only delta explicitly rather than deleting it blindly.
- [x] **G3K1 — Remaining framework-owned namespace consumer cut-over**
  - move surviving consumers of the wholly framework-owned `CommandLine`, `Delimiters`, `Diagnostics`, `Host`, `IO`, `Processes`, `Records`, `RegularExpressions`, `Temporary`, and `Terminal` namespaces to `Icod.CommandFramework`;
  - preserve Coreutils-specific namespaces such as formatting/escape policy, numeric operand grammar, ownership, listing, copy/move, and filesystem-usage behavior.
- [x] **G3K2 — Remaining framework-owned namespace/test excision**
  - remove the duplicated implementations for the G3K1 namespaces from `Icod.CoreUtils.Shared`;
  - remove or rehome their duplicated Shared tests, retaining only tests for Coreutils-owned behavior;
  - complete the intentionally deferred `tests/Shared.Tests` namespace cut-over while distinguishing authoritative framework tests from retained Coreutils tests.
- [x] **G3L — ProcessTestHost audit and contraction**
  - removed the stale `Shared.Tests` project reference after framework-owned process tests left the repository;
  - retained `Icod.CoreUtils.ProcessTestHost` as a repository-local integration-test executable because `Nice.Tests` and `Timeout.Tests` still require real child processes;
  - contracted the host to the two Coreutils-required behaviors: `exit` and `sleep`;
  - kept the framework test host independent in the `Icod.CommandFramework` repository.
- [ ] **G3M — `Icod.CoreUtils.Shared` package closure**
  - [ ] **G3M1 — package metadata freeze and local package validation**
    - freeze the contracted package identity at `Icod.CoreUtils.Shared` 1.0.0 for `net10.0`;
    - align symbols, deterministic build, repository metadata, package icon, README, license, and package tags with the published foundation-package conventions;
    - pack and smoke-restore the package against published `Icod.CommandFramework` 1.0.0 and `Icod.Path` 1.0.0 dependencies before publication.
  - [ ] **G3M2 — publish and Coreutils consumer cut-over**
    - publish `Icod.CoreUtils.Shared` 1.0.0 to the permanent package feeds;
    - convert retained Coreutils command projects from the transitional Shared project reference to `PackageReference Include="Icod.CoreUtils.Shared" Version="1.0.0"`;
    - build/test the in-repository Coreutils consumers against the published package before entering G3N.
- [ ] **G3N — Isolated Coreutils validation and G3 closure**
  - build/test Coreutils without sibling-suite source projects;
  - remove stale solution, CI, output-path, packaging, and documentation references exposed by the isolation build;
  - mark G3 complete only after the contracted repository succeeds independently.

**Exit criterion:** `Icod.CoreUtils.Shared` is no longer an incubation project.
## G4 — Pilot repository extractions

Use small suites first to prove the packaging procedure before moving the large families.

### G4.1 — Icod.UtilLinux

- [ ] Extract repository/history.
- [ ] Replace Shared reference with Framework package.
- [ ] Restore/build/test Windows, Ubuntu, macOS independently.
- [ ] Verify lowercase `kill` and `renice` executable names.

### G4.2 — Icod.Grep

- [ ] Extract repository/history.
- [ ] Replace Shared reference with Framework.
- [ ] Verify regex, record, traversal, diagnostics, and temporary-resource package dependencies.
- [ ] Run independent CI.

### G4.3 — Icod.Tar

- [ ] Extract repository/history.
- [ ] Replace Shared reference with Framework.
- [ ] Verify traversal, metadata, transaction, process, signal, temporary-workspace, and native/compressor behavior against packages.
- [ ] Run independent CI.

**Exit criterion:** three structurally different standalone suites successfully consume published foundation packages.

## G5 — Extract Icod.ProcPs

ProcPs follows the pilot migrations because it is the largest consumer of the cross-suite process/terminal foundation and because successful extraction unblocks deferred Batch 68.

- [ ] Extract all current ProcPs projects/tests/history.
- [ ] Convert ProcPs Shared to Framework package references.
- [ ] Remove transitional direct CoreUtils Shared references from ProcPs command tests.
- [ ] Validate Linux authoritative behavior.
- [ ] Validate Windows/macOS portability providers.
- [ ] Run independent three-runner CI.
- [ ] Remove ProcPs from `Icod.CoreUtils.sln`.
- [ ] Implement Batch 68 `top` in the final ProcPs repository.

**Exit criterion:** ProcPs is fully independent and `top` can be developed against its permanent dependency boundary.

## G6 — Extract Icod.DiffUtils

- [ ] Extract Shared, four commands, five test projects, fixtures, and history.
- [ ] Convert cross-suite dependencies to Framework packages.
- [ ] Preserve internal project references to DiffUtils Shared.
- [ ] Run independent CI.
- [ ] Re-run textual compatibility fixtures consumed by Patch and Ed.

## G7 — Extract Icod.LineEditor

- [ ] Extract Ed.Shared, Ed, Red, Sed, all tests, fixtures, and history.
- [ ] Convert cross-suite dependencies to Framework packages.
- [ ] Preserve Ed/Red → Ed.Shared project references.
- [ ] Keep Sed separate.
- [ ] Confirm no general LineEditor Shared project is introduced.
- [ ] Run independent CI.

## G8 — Extract Icod.Patch

Patch is deliberately late because it exercises both `Icod.Path` and some of the most security-sensitive filesystem/transactional framework APIs.

- [ ] Extract Patch and its complete fixture/test corpus.
- [ ] Use published `Icod.Path`.
- [ ] Use published `Icod.CommandFramework`.
- [ ] Verify no runtime Diffutils dependency.
- [ ] Re-run all security, canonical-path, race, metadata, transaction, fuzz, offset, reversal, backup, reject, and compatibility tests.
- [ ] Run independent CI.

## G9 — Final CoreUtils cleanup

- [ ] Remove all successfully extracted suite projects from `Icod.CoreUtils.sln`.
- [ ] Remove corresponding source/test directories from the CoreUtils repository.
- [ ] Remove temporary output-path collision rules.
- [ ] Remove obsolete solution folders.
- [ ] Remove stale CI and packaging references.
- [ ] Remove stale roadmap inventory text.
- [ ] Confirm no production CoreUtils project references a sibling suite.
- [ ] Confirm CoreUtils uses only published neutral/CoreUtils packages.
- [ ] Run complete Debug/Staging/Release build.
- [ ] Run Windows/Ubuntu/macOS CI.
- [ ] Verify clean checkout package restore.
- [ ] Verify UTF-8/LF policy.
- [ ] Freeze final Gate G dependency graph.

## G10 — Architecture closure

- [ ] Publish final architecture/migration document.
- [ ] Document repository URLs and package names.
- [ ] Document executable ownership.
- [ ] Document package dependency direction.
- [ ] Document versioning policy.
- [ ] Document release/CI policy.
- [ ] Document textual interoperability boundaries.
- [ ] Confirm no circular dependency.
- [ ] Confirm `Icod.CommandFramework` has no command-suite dependency.
- [ ] Confirm every repository builds using **published packages**, not neighboring source trees.
- [ ] Tick Completion Gate G complete.

---

# C. Rule for Every Repository Extraction

Every extracted repository must retain:

- [ ] relevant Git history;
- [ ] source projects;
- [ ] suite-specific Shared/engine projects;
- [ ] all applicable tests and fixtures;
- [ ] README/license/documentation;
- [ ] `net10.0` and C# 13 configuration;
- [ ] Debug/Staging/Release behavior;
- [ ] warnings-as-errors Release policy;
- [ ] lowercase executable assembly names;
- [ ] PascalCase project/namespace conventions;
- [ ] UTF-8/LF policy;
- [ ] Windows/Ubuntu/macOS CI;
- [ ] deterministic package restore;
- [ ] package/version metadata;
- [ ] no references back into the old CoreUtils repository.

A migration is **not complete merely because the files have moved**. It is complete only when the extracted repository builds and tests independently from a clean checkout using the published dependency packages.

---

# D. Immediate Next Work

Completion Gate G is active. `Icod.Path` and `Icod.CommandFramework` are now independent published foundations, so the current work is the G3 contraction of `Icod.CoreUtils.Shared`.

Current sequence:

- [x] G3A — contract the split `Text` namespace.
- [x] G3B — contract the split `Time` namespace.
- [x] G3C1 — cut identity/capability/SELinux consumers over to `Icod.CommandFramework.Platform`.
- [x] G3C2 — delete the duplicated framework-owned Platform implementation and tests.
- [x] G3D1 — cut the neutral file-mode value model over to `Icod.CommandFramework.FileSystem.Modes`.
- [x] G3D2 — delete the duplicate CoreUtils file-mode value model and contract its documentation.
- [x] G3E1 — cut Metadata consumers over to `Icod.CommandFramework.FileSystem.Metadata`.
- [x] G3E2 — remove the duplicate CoreUtils Metadata implementation and tests.
- [x] G3F1 — remove the duplicate CoreUtils Traversal implementation and tests.
- [x] G3G1 — cut Mutation consumers over to `Icod.CommandFramework.FileSystem.Mutation`.
- [x] G3G2 — remove the duplicate CoreUtils Mutation implementation and tests.
- [x] G3H1 — cut RecursiveMutation consumers over to `Icod.CommandFramework.FileSystem.RecursiveMutation`.
- [x] G3H2 — remove the duplicate CoreUtils RecursiveMutation implementation and tests.
- [x] G3I1 — cut TransactionalReplacement consumers over to `Icod.CommandFramework.FileSystem.TransactionalReplacement`.
- [x] G3I2 — remove the duplicate CoreUtils TransactionalReplacement implementation and tests.
- [x] **G3J — Root filesystem operations audit and contraction**
  - compare `FileSystemCapabilities`, `IFileSystemOperations`, and `SystemFileSystemOperations` with the framework package;
  - cut neutral consumers to `Icod.CommandFramework.FileSystem` where contracts match;
  - preserve any Coreutils-only delta explicitly rather than deleting it blindly.
- [ ] Excise namespaces that are wholly owned by `Icod.CommandFramework`.
- [ ] Audit and remove/rehome framework-owned `Shared.Tests` and `ProcessTestHost` infrastructure.
- [ ] Publish the contracted `Icod.CoreUtils.Shared` package and convert retained Coreutils commands to package references.
- [ ] Run the G3 clean-checkout/build/test closure before beginning G4 repository extraction.

Do not begin G4 pilot extractions until the G3 exit criterion is satisfied.
