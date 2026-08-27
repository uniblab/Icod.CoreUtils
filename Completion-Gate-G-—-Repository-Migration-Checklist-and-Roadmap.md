# Completion Gate G — Repository Migration Checklist and Roadmap

## Objective

Completion Gate G converts the present multi-suite incubation repository into the final repository and package architecture without changing command semantics.

The intended dependency model distinguishes published neutral foundations from repository-local suite implementation libraries:

```text
Published neutral foundations
    Icod.CommandFramework
    Icod.Path, where canonical-path contracts are required
            ↓ external PackageReference
Extracted suite repository
    suite-specific Shared / engine library, where required
            ↓ repository-local ProjectReference
    individual command projects
```

Individual commands may also reference the published neutral foundations directly when they consume those contracts without a suite-specific Shared layer.

`Icod.Path` is retained as a separate neutral package. Gate G1 is complete, and CoreUtils consumes the published package rather than a source-tree project. `Icod.CommandFramework` is likewise an independent published foundation.

The final G3 filesystem ownership audit identified a small residual mechanism set still physically located in `Icod.CoreUtils.Shared`: current-process file-creation-mask observation, POSIX filesystem inode-pool observation, and host file-clone/reflink execution. These mechanisms are approved for migration into `Icod.CommandFramework`; GNU mode grammar, GNU copy/move reflink policy, `df`/`du` accounting and presentation policy, and GNU ownership policy remain Coreutils-owned. No additional `Icod.Path` migration is required by this audit.

A suite-specific Shared/engine class library is **not automatically a package boundary**. In particular, `Icod.CoreUtils.Shared` is the permanent repository-local Shared library for the GNU Coreutils/Fileutils/Textutils family: it remains in the `Icod.CoreUtils` repository, is built and released with that suite, and is consumed by same-repository Coreutils projects through `ProjectReference`. It is **not** an independently published NuGet.org or GitHub Packages dependency.

No command suite may become a production dependency of `Icod.CommandFramework`, and sibling command suites must not acquire runtime dependencies on one another merely because they were developed together. Sibling suites must consume neutral contracts from `Icod.CommandFramework` and `Icod.Path` directly rather than using `Icod.CoreUtils.Shared` as a package bridge.

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

- [x] Retain all genuine GNU Coreutils/Fileutils/Textutils command projects.
- [x] Audit `Icod.CoreUtils.Shared`.
- [x] Move demonstrated cross-suite APIs from `Icod.CoreUtils.Shared` to `Icod.CommandFramework`.
- [x] Retain only Coreutils/Fileutils/Textutils-specific behavior in `Icod.CoreUtils.Shared` after the approved final filesystem mechanism migration and consumer cut-over.
- [x] Migrate the approved neutral filesystem remainder to `Icod.CommandFramework`: creation-mask observation, inode-pool observation, and host file-clone/reflink mechanism; then prune the corresponding CoreUtils implementations only after validated consumer cut-over.
- [x] Split `Shared.Tests` between framework tests and remaining Coreutils Shared tests.
- [x] Audit `Icod.CoreUtils.ProcessTestHost`.
  - Decision: retain it as a small repository-local test host for Coreutils integration tests.
  - `Nice.Tests` requires only deterministic child exit behavior.
  - `Timeout.Tests` requires only deterministic child sleep behavior.
  - Framework process-runner tests use the independent `Icod.CommandFramework.ProcessTestHost`; Coreutils does not reference that test project.
- [x] Retain `Icod.CoreUtils.Shared` as a repository-local class-library project and do not publish it as an independently downloadable package.
- [x] Preserve `ProjectReference` from genuine Coreutils/Fileutils/Textutils consumers to `Icod.CoreUtils.Shared` where that suite-local reuse is required.
- [x] Use published neutral packages for cross-repository dependencies (`Icod.CommandFramework` 1.1.0, `Icod.Path` 1.0.0, and direct `Icod.Timing` 1.0.0 where required); do not route sibling suites through `Icod.CoreUtils.Shared`.
- [x] Remove every sibling-suite project after successful extraction.
- [ ] Complete G9 cleanup of any remaining stale solution, packaging, output-path, CI, and documentation residue.

## Icod.DiffUtils — G6 COMPLETE

Destination: <https://github.com/uniblab/Icod.DiffUtils>

- [x] `Icod.DiffUtils.Shared`
- [x] `Icod.DiffUtils.Cmp`
- [x] `Icod.DiffUtils.Diff`
- [x] `Icod.DiffUtils.Diff3`
- [x] `Icod.DiffUtils.SDiff`
- [x] `Icod.DiffUtils.Shared.Tests`
- [x] `Icod.DiffUtils.Cmp.Tests`
- [x] `Icod.DiffUtils.Diff.Tests`
- [x] `Icod.DiffUtils.Diff3.Tests`
- [x] `Icod.DiffUtils.SDiff.Tests`
- [x] Create `Icod.DiffUtils.sln`.
- [x] Preserve project references from commands to `Icod.DiffUtils.Shared`.
- [x] Replace the CoreUtils Shared dependency with the published neutral `Icod.CommandFramework` boundary.
- [x] Preserve interoperability with Patch and Ed strictly through textual formats/fixtures rather than runtime references.
- [x] Remove all DiffUtils tests, executable projects, Shared source, solution folders, configuration mappings, and nesting entries from CoreUtils after the independent repository is proven.

**G6 closure:** no DiffUtils source, test project, or solution entry remains in `Icod.CoreUtils`; the authoritative implementation now lives in the dedicated repository.

## Icod.Grep

- [x] `Icod.Grep`
- [x] `Icod.Grep.Tests`
- [x] Create `Icod.Grep.sln`.
- [x] Replace the present CoreUtils Shared reference with `Icod.CommandFramework`.
- [x] Keep matcher orchestration, binary-input rules, recursive-selection policy, context grouping, and output formatting in Grep.

## Icod.Patch — G8 COMPLETE

Destination: <https://github.com/uniblab/Icod.Patch>

- [x] `Icod.Patch`
- [x] `Icod.Patch.Tests`
- [x] Create `Icod.Patch.sln`.
- [x] Replace the present CoreUtils Shared reference with published `Icod.CommandFramework` 1.1.0.
- [x] Replace the source-tree `Icod.Path` reference with the published `Icod.Path` 1.0.0 package.
- [x] Preserve fixture corpora and transactional/security tests.
- [x] Keep Diffutils interoperability textual; do not reference `Icod.DiffUtils.Shared`.
- [x] Remove Patch tests, production source, solution folder/project wiring, configuration mappings, and nesting entries from CoreUtils after standalone validation.

**G8 closure:** no Patch source, test project, or solution entry remains in `Icod.CoreUtils`; the authoritative implementation and Patch-specific roadmap now live in the dedicated repository.

## Icod.LineEditor — G7 COMPLETE

Destination: <https://github.com/uniblab/Icod.LineEditor>

- [x] `Icod.LineEditor.Ed.Shared`
- [x] `Icod.LineEditor.Ed`
- [x] `Icod.LineEditor.Red`
- [x] `Icod.LineEditor.Sed`
- [x] `Icod.LineEditor.Ed.Shared.Tests`
- [x] `Icod.LineEditor.Ed.Tests`
- [x] `Icod.LineEditor.Red.Tests`
- [x] `Icod.LineEditor.Sed.Tests`
- [x] Create `Icod.LineEditor.sln`.
- [x] Retain project references from Ed and Red to `Icod.LineEditor.Ed.Shared`.
- [x] Sed remains a separate execution engine.
- [x] Do **not** create `Icod.LineEditor.Shared`; the completed sharing audit did not justify it.
- [x] Replace cross-suite Shared references with published `Icod.CommandFramework` 1.1.0.
- [x] Run independent Windows/Ubuntu/macOS CI.
- [x] Remove LineEditor tests, production projects, solution wiring, and migrated root-level LineEditor documents from CoreUtils.

**G7 closure:** the standalone repository is authoritative for the Ed/Red/Sed family. CoreUtils no longer contains LineEditor source projects, test projects, or solution entries.

## Icod.UtilLinux

- [x] `Icod.UtilLinux.Kill`
- [x] `Icod.UtilLinux.Renice`
- [x] `Icod.UtilLinux.Kill.Tests`
- [x] `Icod.UtilLinux.Renice.Tests`
- [x] Create `Icod.UtilLinux.sln`.
- [x] Replace CoreUtils Shared references with `Icod.CommandFramework`.
- [x] Do not create `Icod.UtilLinux.Shared`; the completed extraction did not require a suite-local shared library.

## Icod.Tar

- [x] `Icod.Tar`
- [x] `Icod.Tar.Tests`
- [x] Create `Icod.Tar.sln`.
- [x] Replace CoreUtils Shared with `Icod.CommandFramework`.
- [x] Preserve tar-specific archive models, sparse behavior, selection policy, compression integration, and extraction security inside this repository.
- [x] Preserve native/compression test assets.

## Icod.ProcPs — G5 COMPLETE FOR COREUTILS EXTRACTION

Destination: <https://github.com/uniblab/Icod.ProcPs>

Extracted and independently owned baseline:

- [x] `Icod.ProcPs.Shared`
- [x] `Icod.ProcPs.Uptime`
- [x] `Icod.ProcPs.Free`
- [x] `Icod.ProcPs.Vmstat`
- [x] `Icod.ProcPs.Pgrep`
- [x] `Icod.ProcPs.Pkill`
- [x] `Icod.ProcPs.PidWait`
- [x] `Icod.ProcPs.PidOf`
- [x] `Icod.ProcPs.Pwdx`
- [x] `Icod.ProcPs.Pmap`
- [x] `Icod.ProcPs.Ps`
- [x] `Icod.ProcPs.W`
- [x] `Icod.ProcPs.Sysctl`
- [x] Corresponding command test projects for the extracted baseline.
- [x] `Icod.ProcPs.Shared.Tests`
- [x] Create `Icod.ProcPs.sln`.
- [x] Replace the CoreUtils Shared dependency with published `Icod.CommandFramework` 1.1.0 in the extracted Shared project.
- [x] Preserve repository-local command → `Icod.ProcPs.Shared` project references.
- [x] Retain `/proc`, process-domain, field-catalog, selection, sampling, and screen-model behavior in ProcPs Shared.
- [x] Remove all remaining `Icod.ProcPs` source, project, test, solution-folder, configuration, and nesting entries from CoreUtils.
- [x] Confirm procps-ng `kill`, `skill`, and `snice` remain deliberately absent.

Post-extraction ProcPs work is owned by the `Icod.ProcPs` repository and is not a CoreUtils Gate G blocker. That includes any continuation or reintroduction of the terminal-oriented `tload`, `watch`, `hugetop`, and `slabtop` implementations and deferred Batch 68 `top`/`top` tests. CoreUtils must not regain source-tree ProcPs projects merely to complete that work.

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
- [x] Publish the versioned package; `Icod.CommandFramework` 1.1.0 is the current CoreUtils dependency.
- [x] Validate against real CoreUtils and sibling-suite consumers.

**Exit criterion met:** sibling suites can compile against the published framework binary.
## G3 — Contract `Icod.CoreUtils.Shared` — COMPLETE

- [x] Remove every API already owned by `Icod.CommandFramework`.
- [x] Keep only demonstrated Coreutils/Fileutils/Textutils-specific reuse after the approved final filesystem mechanism migration.
- [x] Make it depend on `Icod.CommandFramework`.
- [x] Add the published `Icod.Path` package dependency where genuinely required.
- [x] Split/rehome tests appropriately.
- [x] Refresh the published `Icod.CommandFramework` filesystem foundation with the approved neutral remainder, migrate its tests, and cut CoreUtils consumers over before pruning migrated source.
- [x] Freeze the permanent distribution boundary: `Icod.CoreUtils.Shared` remains an internal class-library project in the `Icod.CoreUtils` repository and is not independently published.
- [x] Retain same-repository `ProjectReference` relationships from genuine Coreutils consumers to `Icod.CoreUtils.Shared`.
- [x] Ensure neutral external dependencies resolve through published `Icod.CommandFramework` and `Icod.Path` packages rather than through a published CoreUtils Shared package.
- [x] Build/test Coreutils without sibling-suite source projects being needed.

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
  - retain GNU mode parsing/expression policy; the creation-mask provider remained temporarily Coreutils-owned pending the final filesystem ownership audit.
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
- [x] **G3M — `Icod.CoreUtils.Shared` internal-library boundary closure**
  - [x] **G3M1 — package-boundary evaluation and local validation**
    - contract the `Icod.CoreUtils.Shared` surface and prove that it can pack and smoke-restore against published `Icod.CommandFramework` 1.0.0 and `Icod.Path` 1.0.0 dependencies;
    - record that technical packageability was successfully demonstrated;
    - architecture review nevertheless found no independently justified distribution boundary: the retained library is Coreutils-family implementation, not a cross-suite foundation, so separate publication is rejected.
  - [x] **G3M2 — final filesystem mechanism migration and framework package refresh**
    - [x] **G3M2A — migrate neutral mechanism and tests to `Icod.CommandFramework`**
      - extend `Icod.CommandFramework.FileSystem.Metadata.FileSystemInformation` with explicit total, free, and caller-available inode-pool observations and populate them from the existing `statvfs` data; Windows and unsupported hosts must report explicit unavailability rather than guessed counts;
      - move `IFileCreationMaskProvider` and `SystemFileCreationMaskProvider` into `Icod.CommandFramework.FileSystem.Modes`, preserving Linux non-mutating observation and guarded Unix query-and-restore behavior;
      - add a capability-aware file-clone/reflink primitive to `Icod.CommandFramework.FileSystem`, moving the native host mechanism below Coreutils while leaving GNU `--reflink=never|auto|always` policy in CoreUtils;
      - move/rehome the corresponding tests so the neutral mechanisms are validated in the framework repository;
      - audit the local `copy_file_range` helper; delete it if it has no production call site, and move it only if a demonstrated neutral accelerated-copy contract is actually required;
      - build/test `Icod.CommandFramework` on Windows, Ubuntu, and macOS, increment its package version, and publish the refreshed package before CoreUtils removes any migrated implementation.
    - [x] **G3M2B — CoreUtils consumer cut-over and prudent pruning**
      - update `Icod.CoreUtils.Shared` to the refreshed `Icod.CommandFramework` package;
      - make `SystemFileSystemUsageProvider` consume inode-pool values from framework `FileSystemInformation` and remove the duplicate local `statvfs` ABI;
      - replace CoreUtils-local creation-mask observation with the framework provider;
      - make `CopyMoveEngine` consume the framework file-clone primitive while retaining GNU copy/move policy;
      - replace direct `ApplyDirectoryMetadataBestEffort` mutation with the framework metadata-preservation/application path so ownership, mode, access/modification/birth timestamps, attributes, and required-versus-best-effort semantics are handled consistently for directories and regular files;
      - prune migrated CoreUtils source and tests only after each consumer cut-over is validated;
      - run the affected Shared, `cp`, `mv`, `df`, `du`, `chmod`, `mkdir`, `mkfifo`, and `mknod` tests together with full required-runner validation.
  - [x] **G3M3 — internal-library boundary freeze and Coreutils consumer validation**
    - [x] remove publication-only intent from the Shared project, release workflow, and package-install documentation; do not publish `Icod.CoreUtils.Shared` independently to NuGet.org or GitHub Packages;
    - [x] retain `ProjectReference` from genuine Coreutils/Fileutils/Textutils command projects to `Icod.CoreUtils.Shared` and guard against any `Icod.CoreUtils.Shared` package reference in repository tests;
    - [x] continue consuming published `Icod.CommandFramework` 1.1.0 and `Icod.Path` 1.0.0 as the neutral external package dependencies;
    - [x] classify remaining sibling-suite `Icod.CoreUtils.Shared` project references as transitional extraction debt to be replaced directly by the appropriate neutral foundation packages during G4 through G8; no sibling suite may consume a published `Icod.CoreUtils.Shared` package;
    - [x] build/test the in-repository Coreutils consumers with `Icod.CoreUtils.Shared` built from source before entering G3N.
- [x] **G3N — Isolated Coreutils validation and G3 closure**
  - build/test the retained Coreutils projects, `Icod.CoreUtils.Shared`, its tests, and the repository-local ProcessTestHost without sibling-suite source projects;
  - verify that genuine Coreutils consumers use same-repository `ProjectReference` for `Icod.CoreUtils.Shared` and that no Coreutils project uses `PackageReference Include="Icod.CoreUtils.Shared"`;
  - verify that all cross-repository dependencies required by this isolated build resolve from the published neutral packages;
  - remove stale solution, CI, output-path, packaging, and documentation references exposed by the isolation build;
  - mark G3 complete only after the contracted repository succeeds independently.

**Exit criterion met:** `Icod.CoreUtils.Shared` is no longer an incubation project: it is the permanent repository-local Shared library for Coreutils/Fileutils/Textutils, contains no cross-suite foundation ownership or approved neutral filesystem mechanism, is not independently published, and is not a permitted dependency of extracted sibling suites.
## G4 — Pilot repository extractions

Use small suites first to prove the repository-extraction and external-package procedure before moving the large families.

In every extraction below, replacing a transitional CoreUtils Shared reference means referencing the actual published neutral owner (`Icod.CommandFramework` and, where applicable, `Icod.Path`) directly. It never means replacing the project reference with an `Icod.CoreUtils.Shared` package.

### G4.1 — Icod.UtilLinux — COMPLETE

- [x] Extract repository/history.
- [x] Replace Shared reference with Framework package.
- [x] Restore/build/test Windows, Ubuntu, macOS independently.
- [x] Verify lowercase `kill` and `renice` executable names.

### G4.2 — Icod.Grep — COMPLETE

- [x] Extract repository/history.
- [x] Replace Shared reference with Framework.
- [x] Verify regex, record, traversal, diagnostics, and temporary-resource package dependencies.
- [x] Run independent CI.

### G4.3 — Icod.Tar — COMPLETE

- [x] Extract repository/history.
- [x] Replace Shared reference with Framework.
- [x] Verify traversal, metadata, transaction, process, signal, temporary-workspace, and native/compressor behavior against packages.
- [x] Run independent CI.

**Exit criterion met:** three structurally different standalone suites successfully consume published foundation packages.

## G5 — Extract Icod.ProcPs — COMPLETE FOR COREUTILS EXTRACTION

ProcPs followed the pilot migrations because it was the largest consumer of the cross-suite process/terminal foundation and because extraction was required before deferred Batch 68 could be developed against its permanent dependency boundary.

**Closure checkpoint (2026-08-22):** the independent `Icod.ProcPs` repository and solution own the extracted Shared library, Shared tests, the migrated command baseline, command tests, and independent Windows/Ubuntu/macOS CI. CoreUtils no longer contains any `Icod.ProcPs` source, project, test, solution-folder, configuration, or nesting entries.

- [x] Establish the independent `Icod.ProcPs` repository and `Icod.ProcPs.sln`.
- [x] Extract `Icod.ProcPs.Shared`, its Shared tests, and the independently validated command/test baseline.
- [x] Convert the extracted Shared layer to published `Icod.CommandFramework` 1.1.0.
- [x] Preserve repository-local command → `Icod.ProcPs.Shared` project references.
- [x] Validate the extracted baseline independently on Windows, Ubuntu, and macOS.
- [x] Remove all ProcPs production/test projects and solution wiring from CoreUtils.
- [x] Transfer all remaining ProcPs implementation work to the dedicated repository backlog rather than retaining or recreating placeholders in CoreUtils.

The terminal-oriented commands and deferred Batch 68 `top` are now ProcPs-repository concerns. Their implementation status does not keep the CoreUtils repository-extraction step open.

**Exit criterion met:** ProcPs is fully independent of CoreUtils, and future ProcPs work can proceed against its permanent repository/dependency boundary.

## G6 — Extract Icod.DiffUtils — COMPLETE

**Closure checkpoint (2026-08-22):** the independent `Icod.DiffUtils` repository contains `Icod.DiffUtils.Shared`, `cmp`, `diff`, `diff3`, `sdiff`, all five dedicated test projects, its own solution, documentation, and CI. CoreUtils removal was deliberately staged: DiffUtils tests first, then the four executable projects, then `Icod.DiffUtils.Shared`.

- [x] Extract Shared, four commands, five test projects, fixtures, and history.
- [x] Convert cross-suite dependencies to published neutral foundation packages.
- [x] Preserve internal repository-local project references to `Icod.DiffUtils.Shared`.
- [x] Provide independent repository CI for Windows, Ubuntu, and macOS.
- [x] Preserve Patch and Ed interoperability as a textual-format boundary rather than a runtime DiffUtils dependency.
- [x] Remove the five DiffUtils test projects from CoreUtils and clean their solution wiring.
- [x] Remove `cmp`, `diff`, `diff3`, and `sdiff` from CoreUtils and clean their solution wiring.
- [x] Remove `Icod.DiffUtils.Shared` from CoreUtils last, after no in-tree consumers remain.
- [x] Validate the CoreUtils working branch after each removal stage.

**Exit criterion met:** no DiffUtils-specific source, test project, or solution entry remains in CoreUtils; the authoritative implementation lives at <https://github.com/uniblab/Icod.DiffUtils>.

## G7 — Extract Icod.LineEditor — COMPLETE

**Closure checkpoint (2026-08-23):** `Icod.LineEditor` is an independent repository containing `Icod.LineEditor.Ed.Shared`, `ed`, `red`, `sed`, their four dedicated test projects, its own solution, documentation, licensing, and published-neutral dependencies. PR #2 passed the independent Staging clean/restore/build/test matrix on Windows, Ubuntu, and macOS. CoreUtils removal was staged as tests first, then the four production projects, then the migrated root-level LineEditor documents.

- [x] Extract Ed.Shared, Ed, Red, Sed, all tests, fixtures, and history.
- [x] Convert cross-suite dependencies to published Framework packages.
- [x] Preserve Ed/Red → Ed.Shared repository-local project references.
- [x] Keep Sed separate.
- [x] Confirm no general LineEditor Shared project is introduced.
- [x] Run independent Windows/Ubuntu/macOS CI.
- [x] Remove all LineEditor production/test projects and solution wiring from CoreUtils.
- [x] Remove migrated LineEditor-specific root documentation from CoreUtils.

**Exit criterion met:** LineEditor is fully independent of CoreUtils, and future LineEditor work belongs exclusively in <https://github.com/uniblab/Icod.LineEditor>.

## G8 — Extract Icod.Patch — COMPLETE

**Closure checkpoint (2026-08-23):** `Icod.Patch` is an independent repository containing the production implementation, complete dedicated test/fixture corpus, standalone solution, documentation, published `Icod.CommandFramework` 1.1.0 and `Icod.Path` 1.0.0 dependencies, no runtime Diffutils dependency, and independent Windows/Ubuntu/macOS CI configuration. CoreUtils removal was deliberately staged: Patch tests first, then the 31-file production tree together with the Patch solution folder/project wiring, configuration mappings, and nesting entry.

- [x] Extract Patch and its complete fixture/test corpus.
- [x] Use published `Icod.Path`.
- [x] Use published `Icod.CommandFramework`.
- [x] Verify no runtime Diffutils dependency.
- [x] Re-run all security, canonical-path, race, metadata, transaction, fuzz, offset, reversal, backup, reject, and compatibility tests.
- [x] Run independent CI.
- [x] Remove Patch tests, production source, and solution wiring from CoreUtils after validating the standalone repository.

**Exit criterion met:** Patch is fully independent of CoreUtils, and future Patch work belongs exclusively in <https://github.com/uniblab/Icod.Patch>.

## G9 — Final CoreUtils cleanup

### G9A — Repository hygiene and documentation truth

- [x] Treat the repository `.editorconfig` as the authoritative text-format and C# formatting policy.
- [x] Align contributor guidance and active Gate G documentation with the authoritative `.editorconfig`.
- [x] Update the upstream-version ledger so completed/extracted batches are no longer described as merely planned.
- [x] Retain extracted-suite batch documents as historical implementation records without treating them as live source ownership.
- [x] Continue with G9B mechanical architecture/dependency audit and reconcile any remaining superseded inventory text against its final evidence.

### G9B — Mechanical architecture and dependency audit

**Audit checkpoint (2026-08-27, `Gate_G9` commit `ee02c2ad858a1048908c82a09f3e980dd227f877`):**

- [x] The live solution contains no extracted sibling-suite project entries.
- [x] The recursive repository tree contains no extracted sibling-suite source, project, or test paths.
- [x] No production CoreUtils project references an extracted sibling suite.
- [x] `Icod.CoreUtils.Shared` is non-packable and remains a same-repository `ProjectReference` dependency.
- [x] No CoreUtils project uses `PackageReference Include="Icod.CoreUtils.Shared"`.
- [x] Published external Icod dependencies are neutral packages: `Icod.CommandFramework` 1.1.0, `Icod.Path` 1.0.0, and direct `Icod.Timing` 1.0.0 where required.
- [x] PR and `main` workflows target only `Icod.CoreUtils.sln` and retain the required Windows/Ubuntu/macOS matrix.
- [x] Perform the final all-project output-path/collision-rule sweep locally during G9C.
- [x] Reconcile any remaining stale roadmap inventory text exposed by the G9C validation sweep.

- [x] Remove all successfully extracted suite projects from `Icod.CoreUtils.sln`.
- [x] Remove corresponding source/test directories from the CoreUtils repository.
- [x] Remove temporary output-path collision rules.
  - The historical collision rule used suite-specific output directories only while sibling suites with colliding executable names were co-resident.
  - The surviving common CoreUtils `..\bin\$(Configuration)\` output root is the suite's aggregate command-output layout and is retained.
- [x] Remove obsolete solution folders.
- [x] Remove stale CI references.
- [x] Remove stale sibling-suite packaging references.
- [x] Remove stale roadmap inventory text.
- [x] Confirm no production CoreUtils project references a sibling suite.
- [x] Confirm every CoreUtils **external** dependency is a published neutral package and that `Icod.CoreUtils.Shared` remains a same-repository project dependency.
- [x] Confirm no CoreUtils project uses a package reference to `Icod.CoreUtils.Shared`.
- [ ] Run complete Debug/Staging/Release build.
- [ ] Run Windows/Ubuntu/macOS CI.
- [ ] Verify clean checkout package restore.
- [ ] Verify repository text-format policy against the authoritative `.editorconfig`.
- [ ] Freeze final Gate G dependency graph.

**G9B exit state:** the repository/project/package boundary is mechanically consistent with the completed extractions.

### G9C — Closure validation

**Mechanical-preparation checkpoint (2026-08-27, `Gate_G9` commit `246b10281a2a374f94e0b01440296eec090f330a`):**

- [x] Complete the all-project output-path sweep and distinguish the retained common CoreUtils aggregate output root from obsolete sibling-suite collision directories.
- [x] Correct the stale G2 current-dependency ledger from `Icod.CommandFramework` 1.0.0 to 1.1.0.
- [x] Allow the local build entry points to select `Debug`, `Staging`, or `Release`, while preserving `Debug` as the default.
- [ ] Execute clean restore plus build/test validation for Debug, Staging, and Release.
- [ ] Run required Windows/Ubuntu/macOS CI validation.
- [ ] Verify repository text-format policy against `.editorconfig`.
- [ ] Freeze the final Gate G dependency graph after execution validation is green.

G9C remains open until the unchecked execution-validation items above are actually proven.

## G10 — Architecture closure

- [ ] Publish final architecture/migration document.
- [ ] Document repository URLs, published external package names, and repository-local Shared/engine boundaries.
- [ ] Document executable ownership.
- [ ] Document external package dependency direction and internal project-reference direction.
- [ ] Document versioning policy.
- [ ] Document release/CI policy.
- [ ] Document textual interoperability boundaries.
- [ ] Confirm no circular dependency.
- [ ] Confirm `Icod.CommandFramework` has no command-suite dependency.
- [ ] Confirm every repository builds using published **external** dependency packages plus its own repository-local project references, never neighboring source trees.
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
- [ ] repository text-format policy as defined by that repository's authoritative `.editorconfig`;
- [ ] Windows/Ubuntu/macOS CI;
- [ ] deterministic package restore;
- [ ] repository version/release metadata, with package metadata only for artifacts intentionally published as packages;
- [ ] repository-local `ProjectReference` relationships for suite-specific Shared/engine libraries unless a separate package boundary is independently justified;
- [ ] no references back into the old CoreUtils repository.

A migration is **not complete merely because the files have moved**. It is complete only when the extracted repository builds and tests independently from a clean checkout using published external dependency packages together with its own repository-local project references.

A suite-specific Shared/engine assembly does not become a separately published package merely because multiple projects inside one repository consume it. Cross-repository reuse must be satisfied by the neutral published foundations or by another independently justified package boundary.

---

# D. Immediate Next Work

Completion Gate G remains active. The completed extraction sequence is now:

- [x] G4.1 — `Icod.UtilLinux`
- [x] G4.2 — `Icod.Grep`
- [x] G4.3 — `Icod.Tar`
- [x] G5 — `Icod.ProcPs` CoreUtils extraction
- [x] G6 — `Icod.DiffUtils`
- [x] G7 — `Icod.LineEditor`
- [x] G8 — `Icod.Patch`

These suites have been moved out of the live `Icod.CoreUtils` source/solution after successful extraction validation. Remaining feature work in an extracted suite is owned by that suite's repository and does not require reintroducing its source into CoreUtils.

The next engineering step is **G9 — Final CoreUtils cleanup**. Remove stale solution, output-path, packaging, CI, and roadmap inventory residue; confirm no sibling-suite production dependencies remain; validate Debug/Staging/Release and the three required runners; verify clean package restore and UTF-8/LF policy; then freeze the final Gate G dependency graph before G10.
