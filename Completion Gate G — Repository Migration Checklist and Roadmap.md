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

`Icod.Path` remains a separate neutral package unless the Gate G API audit demonstrates that incorporating it into `Icod.CommandFramework` is materially cleaner.

No command suite may become a production dependency of `Icod.CommandFramework`, and sibling command suites must not acquire runtime dependencies on one another merely because they were developed together.

---

# A. Repository / Project Migration Inventory

## Neutral foundations

- [ ] **Icod.Path**
  - `Icod.Path`
  - `Icod.Path.Tests`
  - Proposed destination: `Icod.Path` repository and NuGet package.
  - Gate G roadmap should explicitly add this repository decision.

- [ ] **Icod.CommandFramework**
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
- [ ] Audit `Icod.CoreUtils.ProcessTestHost` and decide whether it is:
  - Coreutils-local test infrastructure;
  - framework test infrastructure; or
  - a small repository-local test host replicated independently where needed.
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

## G1 — Freeze `Icod.Path`

- [ ] Audit its public API and actual consumers.
- [ ] Confirm whether it remains independent or moves into `Icod.CommandFramework`.
- [ ] Preferred decision: retain independent `Icod.Path`.
- [ ] Freeze namespace and package surface.
- [ ] Establish package versioning, symbols, SourceLink, deterministic builds, README, license, and CI.
- [ ] Extract repository history.
- [ ] Publish a prerelease package.
- [ ] Convert a controlled in-tree consumer to the package and validate all three runners.

**Exit criterion:** canonical-path behavior can be consumed without a source-tree project reference.

## G2 — Extract `Icod.CommandFramework`

- [ ] Create the new repository/solution.
- [ ] Move only APIs demonstrated to have independent-suite consumers.
- [ ] Move/split their tests out of `Icod.CoreUtils.Shared.Tests`.
- [ ] Remove Coreutils-specific names and assumptions from the new public API.
- [ ] Audit accessibility and XML documentation.
- [ ] Audit trimming and AOT behavior.
- [ ] Audit native ABI boundaries on Windows/Linux/macOS.
- [ ] Ensure no suite dependency exists.
- [ ] Package with symbols and SourceLink.
- [ ] Publish prerelease package.
- [ ] Validate against several real consumers before declaring the API frozen.

**Exit criterion:** sibling suites can compile against a published framework binary.

## G3 — Contract `Icod.CoreUtils.Shared`

- [ ] Remove every API now owned by `Icod.CommandFramework`.
- [ ] Keep only demonstrated Coreutils/Fileutils/Textutils-specific reuse.
- [ ] Make it depend on `Icod.CommandFramework`.
- [ ] Add `Icod.Path` package dependency where genuinely required.
- [ ] Split/rehome tests appropriately.
- [ ] Publish `Icod.CoreUtils.Shared`.
- [ ] Convert Coreutils commands to package references.
- [ ] Build/test Coreutils without sibling-suite source projects being needed.

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

The next Gate G implementation tranche should contain **analysis artifacts only**, not repository extraction:

- [ ] Update the main roadmap status to make Completion Gate G active.
- [ ] Add the `Icod.Path` decision explicitly to Gate G.
- [ ] Add this migration roadmap as the Gate G working document.
- [ ] Produce the complete project dependency graph.
- [ ] Produce the complete Shared API → consumer matrix.
- [ ] Produce an initial proposed ownership classification for every Shared source area.
- [ ] Review that classification before moving a single source file.

Only after that review should G1 (`Icod.Path`) and G2 (`Icod.CommandFramework`) begin.