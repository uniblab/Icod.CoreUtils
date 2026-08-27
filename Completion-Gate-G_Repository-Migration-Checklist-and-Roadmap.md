__FILLER_1__
__FILLER_2__
__FILLER_3__
__FILLER_4__
__FILLER_5__
__FILLER_6__
__FILLER_7__
__FILLER_8__
__FILLER_9__
__FILLER_10__
__FILLER_11__
__FILLER_12__
__FILLER_13__
__FILLER_14__
__FILLER_15__
__FILLER_16__
__FILLER_17__
__FILLER_18__
__FILLER_19__
__FILLER_20__
__FILLER_21__
__FILLER_22__
__FILLER_23__
__FILLER_24__
__FILLER_25__
__FILLER_26__
__FILLER_27__
__FILLER_28__
__FILLER_29__
__FILLER_30__
__FILLER_31__
__FILLER_32__
__FILLER_33__
__FILLER_34__
__FILLER_35__
__FILLER_36__
__FILLER_37__
__FILLER_38__
__FILLER_39__
__FILLER_40__
__FILLER_41__
__FILLER_42__
__FILLER_43__
__FILLER_44__
__FILLER_45__
__FILLER_46__
__FILLER_47__
__FILLER_48__
__FILLER_49__
__FILLER_50__
__FILLER_51__
__FILLER_52__
__FILLER_53__
__FILLER_54__
__FILLER_55__
__FILLER_56__
__FILLER_57__
__FILLER_58__
__FILLER_59__
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
- [x] Complete G9 cleanup of any remaining stale solution, packaging, output-path, CI, and documentation residue.

## Icod.DiffUtils — G6 COMPLETE

Destination: <https://github.com/uniblab/Icod.DiffUtils>

- [x] `Icod.DiffUtils.Shared`
- [x] `Icod.DiffUtils.Cmp`
- [x] `Icod.DiffUtils.Diff`
- [x] `Icod.DiffUtils.Diff3`
- [x] `Icod.DiffUtils.SDiff`
__FILLER_81__
__FILLER_82__
__FILLER_83__
__FILLER_84__
__FILLER_85__
__FILLER_86__
__FILLER_87__
__FILLER_88__
__FILLER_89__
__FILLER_90__
__FILLER_91__
__FILLER_92__
__FILLER_93__
__FILLER_94__
__FILLER_95__
__FILLER_96__
__FILLER_97__
__FILLER_98__
__FILLER_99__
__FILLER_100__
__FILLER_101__
__FILLER_102__
__FILLER_103__
__FILLER_104__
__FILLER_105__
__FILLER_106__
__FILLER_107__
__FILLER_108__
__FILLER_109__
__FILLER_110__
__FILLER_111__
__FILLER_112__
__FILLER_113__
__FILLER_114__
__FILLER_115__
__FILLER_116__
__FILLER_117__
__FILLER_118__
__FILLER_119__
__FILLER_120__
__FILLER_121__
__FILLER_122__
__FILLER_123__
__FILLER_124__
__FILLER_125__
__FILLER_126__
__FILLER_127__
__FILLER_128__
__FILLER_129__
__FILLER_130__
__FILLER_131__
__FILLER_132__
__FILLER_133__
__FILLER_134__
__FILLER_135__
__FILLER_136__
__FILLER_137__
__FILLER_138__
__FILLER_139__
__FILLER_140__
__FILLER_141__
__FILLER_142__
__FILLER_143__
__FILLER_144__
__FILLER_145__
__FILLER_146__
__FILLER_147__
__FILLER_148__
__FILLER_149__
__FILLER_150__
__FILLER_151__
__FILLER_152__
__FILLER_153__
__FILLER_154__
__FILLER_155__
__FILLER_156__
__FILLER_157__
__FILLER_158__
__FILLER_159__
__FILLER_160__
__FILLER_161__
__FILLER_162__
__FILLER_163__
__FILLER_164__
__FILLER_165__
__FILLER_166__
__FILLER_167__
__FILLER_168__
__FILLER_169__
__FILLER_170__
__FILLER_171__
__FILLER_172__
__FILLER_173__
__FILLER_174__
__FILLER_175__
__FILLER_176__
__FILLER_177__
__FILLER_178__
__FILLER_179__
__FILLER_180__
__FILLER_181__
__FILLER_182__
__FILLER_183__
__FILLER_184__
Post-extraction ProcPs work is owned by the `Icod.ProcPs` repository and is not a CoreUtils Gate G blocker. That includes any continuation or reintroduction of the terminal-oriented `tload`, `watch`, `hugetop`, and `slabtop` implementations and deferred Batch 68 `top`/`top` tests. CoreUtils must not regain source-tree ProcPs projects merely to complete that work.

---

# B. Gate G Migration Roadmap

## G0 — Freeze and inventory — COMPLETE

No projects move during this phase.

- [x] Identify the intended final repositories.
- [x] Identify the existing suite-specific Shared libraries.
- [x] Confirm that no general `Icod.LineEditor.Shared` is currently justified.
- [x] Identify `Icod.Path` as an unresolved Gate G repository/package item.
- [x] Update the living-status header: Batch 72 is validated/merged and Completion Gate G is active.
- [x] Add `Icod.Path` explicitly to the Gate G checklist.
- [x] Reconcile every `.csproj` into the final repository/project inventory; G9 and G10B record the authoritative production project/package boundary.
- [x] Reconcile the complete production `ProjectReference` graph; G10B re-verifies that every production project edge is repository-local.
- [x] Inventory the public/protected/internal `Icod.CoreUtils.Shared` surface through the G3 contraction tranches.
- [x] Record actual Shared API consumers by project and suite through the G3 consumer cut-overs and extraction audits.
- [x] Perform the corresponding ownership audits for:
  - `Icod.DiffUtils.Shared`;
  - `Icod.LineEditor.Ed.Shared`;
  - `Icod.ProcPs.Shared`;
  - `Icod.Path`;
  - other reusable engine boundaries.
- [x] Classify retained and migrated APIs as:
  - `Framework` / published neutral foundation;
  - `CoreUtils.Shared`;
  - suite-specific;
  - command-local;
  - obsolete/duplicate.
- [x] Detect public-signature/package-boundary leaks through consumer cut-over and independent build validation.
- [x] Detect circular package dependencies; G10B re-verifies that the final repository/package graph is acyclic.

**G10C reconciliation:** G0 originally called for standalone generated inventory artifacts. The inventory work was completed incrementally through the G3 ownership/consumer contraction, the G9 mechanical repository sweep, and the G10B final production-edge audit. Those retained audits and the final source repositories are the authoritative inventory; no separate transient inventory file is required for closure.

**Exit criterion met:** every shared API has a permanent owner and every production project/package edge has a validated final replacement.

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
__FILLER_233__
__FILLER_234__
__FILLER_235__
__FILLER_236__
__FILLER_237__
__FILLER_238__
__FILLER_239__
__FILLER_240__
__FILLER_241__
__FILLER_242__
__FILLER_243__
__FILLER_244__
__FILLER_245__
__FILLER_246__
__FILLER_247__
__FILLER_248__
__FILLER_249__
__FILLER_250__
__FILLER_251__
__FILLER_252__
__FILLER_253__
__FILLER_254__
__FILLER_255__
__FILLER_256__
__FILLER_257__
__FILLER_258__
__FILLER_259__
__FILLER_260__
__FILLER_261__
__FILLER_262__
__FILLER_263__
__FILLER_264__
__FILLER_265__
__FILLER_266__
__FILLER_267__
__FILLER_268__
__FILLER_269__
__FILLER_270__
__FILLER_271__
__FILLER_272__
__FILLER_273__
__FILLER_274__
__FILLER_275__
__FILLER_276__
__FILLER_277__
__FILLER_278__
__FILLER_279__
__FILLER_280__
__FILLER_281__
__FILLER_282__
__FILLER_283__
__FILLER_284__
__FILLER_285__
__FILLER_286__
__FILLER_287__
__FILLER_288__
__FILLER_289__
__FILLER_290__
__FILLER_291__
__FILLER_292__
__FILLER_293__
__FILLER_294__
__FILLER_295__
__FILLER_296__
__FILLER_297__
__FILLER_298__
__FILLER_299__
__FILLER_300__
__FILLER_301__
__FILLER_302__
__FILLER_303__
__FILLER_304__
__FILLER_305__
__FILLER_306__
__FILLER_307__
__FILLER_308__
__FILLER_309__
__FILLER_310__
__FILLER_311__
__FILLER_312__
__FILLER_313__
__FILLER_314__
__FILLER_315__
__FILLER_316__
__FILLER_317__
__FILLER_318__
__FILLER_319__
__FILLER_320__
__FILLER_321__
__FILLER_322__
__FILLER_323__
__FILLER_324__
__FILLER_325__
__FILLER_326__
__FILLER_327__
__FILLER_328__
__FILLER_329__
__FILLER_330__
__FILLER_331__
__FILLER_332__
__FILLER_333__
__FILLER_334__
__FILLER_335__
__FILLER_336__
__FILLER_337__
__FILLER_338__
__FILLER_339__
__FILLER_340__
__FILLER_341__
__FILLER_342__
__FILLER_343__
__FILLER_344__
__FILLER_345__
__FILLER_346__
__FILLER_347__
__FILLER_348__
__FILLER_349__
__FILLER_350__
__FILLER_351__
__FILLER_352__
__FILLER_353__
__FILLER_354__
__FILLER_355__
__FILLER_356__
__FILLER_357__
__FILLER_358__
__FILLER_359__
__FILLER_360__
__FILLER_361__
__FILLER_362__
__FILLER_363__
__FILLER_364__
__FILLER_365__
__FILLER_366__
__FILLER_367__
__FILLER_368__
__FILLER_369__
__FILLER_370__
__FILLER_371__
__FILLER_372__
__FILLER_373__
__FILLER_374__
__FILLER_375__
__FILLER_376__
__FILLER_377__
__FILLER_378__
__FILLER_379__
__FILLER_380__
__FILLER_381__
__FILLER_382__
__FILLER_383__
__FILLER_384__
__FILLER_385__
__FILLER_386__
__FILLER_387__
__FILLER_388__
__FILLER_389__
__FILLER_390__
__FILLER_391__
__FILLER_392__
__FILLER_393__
__FILLER_394__
__FILLER_395__
__FILLER_396__
__FILLER_397__
__FILLER_398__
__FILLER_399__
__FILLER_400__
__FILLER_401__
__FILLER_402__
__FILLER_403__
__FILLER_404__
__FILLER_405__
__FILLER_406__
__FILLER_407__
__FILLER_408__
__FILLER_409__
__FILLER_410__
__FILLER_411__
__FILLER_412__
__FILLER_413__
__FILLER_414__
__FILLER_415__
__FILLER_416__
__FILLER_417__
__FILLER_418__
__FILLER_419__
__FILLER_420__
__FILLER_421__
__FILLER_422__
__FILLER_423__
__FILLER_424__
__FILLER_425__
__FILLER_426__
__FILLER_427__
__FILLER_428__
__FILLER_429__
__FILLER_430__
__FILLER_431__
__FILLER_432__
__FILLER_433__
__FILLER_434__
__FILLER_435__
__FILLER_436__
__FILLER_437__
__FILLER_438__
__FILLER_439__
__FILLER_440__
__FILLER_441__
__FILLER_442__
__FILLER_443__
__FILLER_444__
__FILLER_445__
__FILLER_446__
__FILLER_447__
__FILLER_448__
__FILLER_449__
__FILLER_450__
__FILLER_451__
__FILLER_452__
__FILLER_453__
__FILLER_454__
__FILLER_455__
__FILLER_456__
__FILLER_457__
__FILLER_458__
__FILLER_459__
__FILLER_460__
__FILLER_461__
__FILLER_462__
__FILLER_463__
__FILLER_464__
__FILLER_465__
__FILLER_466__
__FILLER_467__
__FILLER_468__
__FILLER_469__
__FILLER_470__
__FILLER_471__
__FILLER_472__
__FILLER_473__
__FILLER_474__
__FILLER_475__
__FILLER_476__
__FILLER_477__
__FILLER_478__
__FILLER_479__
__FILLER_480__
__FILLER_481__
__FILLER_482__
__FILLER_483__
__FILLER_484__
__FILLER_485__
__FILLER_486__
__FILLER_487__
__FILLER_488__
__FILLER_489__
__FILLER_490__
__FILLER_491__
__FILLER_492__
__FILLER_493__
__FILLER_494__
__FILLER_495__
__FILLER_496__
__FILLER_497__
__FILLER_498__
__FILLER_499__
__FILLER_500__
__FILLER_501__
__FILLER_502__
__FILLER_503__
__FILLER_504__
__FILLER_505__
__FILLER_506__
__FILLER_507__
__FILLER_508__
__FILLER_509__
__FILLER_510__
__FILLER_511__
__FILLER_512__
__FILLER_513__
__FILLER_514__
__FILLER_515__
__FILLER_516__
__FILLER_517__
__FILLER_518__
__FILLER_519__
__FILLER_520__
__FILLER_521__
__FILLER_522__
__FILLER_523__
__FILLER_524__
__FILLER_525__
__FILLER_526__
__FILLER_527__
__FILLER_528__
__FILLER_529__
__FILLER_530__
__FILLER_531__
__FILLER_532__
**G9C exit criterion met:** execution validation, steady-state CI, repository-policy reconciliation, and dependency-graph freeze are complete.

**G9 exit criterion met:** CoreUtils is cleanly separated from the extracted sibling suites and validated against its permanent repository/package boundaries. G10 architecture closure is next.

## G10 — Architecture closure — COMPLETE

### G10A — Final architecture and migration record — COMPLETE

- [x] Publish `Icod.CoreUtils-Architecture-and-Migration.md`.
- [x] Document repository URLs and repository-local Shared/engine boundaries.
- [x] Document executable ownership.
- [x] Document external package direction and repository-local `ProjectReference` direction.
- [x] Document independent per-repository versioning.
- [x] Document release/CI ownership.
- [x] Document textual interoperability boundaries.
- [x] Confirm the complete cross-repository graph is acyclic.
- [x] Confirm every final repository builds using published external packages plus its own repository-local projects, never neighboring source trees.

**G10A exit state:** the architecture is written down and G10B has supplied the dependency/isolation evidence needed to validate it.

### G10B — Cross-repository dependency and isolation verification — COMPLETE

**Audit checkpoint (2026-08-27, `Gate_G10` commit `151cc91bf27bfe339bc9f1e5c933b9a527ec527c`):**

- [x] Enumerate every production `PackageReference` and `ProjectReference` in each final repository.
- [x] Reject any `ProjectReference` that crosses a repository boundary.
- [x] Reject runtime package dependencies from one command suite to another command suite.
- [x] Verify neutral-foundation edges do not point back into command suites.
- [x] Confirm the complete repository/package graph is acyclic.
- [x] Confirm clean restore/build/test or equivalent current CI evidence for each final repository.
- [x] Record required version-pin migrations without imposing ecosystem-wide lockstep versioning.
  - No G10B dependency migration is required. Existing explicit version pins remain consumer-owned and independently versioned.

See `Icod.CoreUtils-G10B-Dependency-Audit.md` for the repository-by-repository production graph and current CI evidence.

**G10B exit state:** every audited cross-repository production edge is a published neutral package edge; every production `ProjectReference` is repository-local; no command-suite runtime package cycle or neutral-foundation back-edge exists; the graph is acyclic; and current independent CI evidence is green.

### G10C — Completion Gate G closure — COMPLETE

- [x] Reconcile the final checklist against G10B evidence.
- [x] Record the final closure checkpoint in both roadmaps.
- [x] Tick Completion Gate G complete.

**G10C / Completion Gate G closure checkpoint (2026-08-27):** the historical G0 inventory language and the final extraction checklist have been reconciled against the completed G3 through G9 work, the G10A architecture record, and the G10B repository-by-repository dependency/isolation evidence. Stale wording that assumed every extracted suite would consume the same neutral package has been replaced by the proven rule: cross-repository reuse flows through the published neutral owner, while suite engines remain repository-local. No unresolved source-tree, package-direction, cycle, CI-isolation, or CoreUtils cleanup blocker remains. Completion Gate G is complete.

---

# C. Rule for Every Repository Extraction

**G10C reconciliation:** the G4 through G9 extraction records establish the retained source, history, project identity, tests, documentation, configuration, release metadata, and repository-local engine boundaries; G10B supplies current independent CI and dependency-isolation evidence. Checklist items apply where the extracted repository actually has a suite-local Shared/engine project.

Every extracted repository must retain:

- [x] relevant Git history;
- [x] source projects;
- [x] suite-specific Shared/engine projects;
- [x] all applicable tests and fixtures;
- [x] README/license/documentation;
- [x] `net10.0` and C# 13 configuration;
- [x] Debug/Staging/Release behavior;
- [x] warnings-as-errors Release policy;
- [x] lowercase executable assembly names;
- [x] PascalCase project/namespace conventions;
- [x] repository text-format policy as defined by that repository's authoritative `.editorconfig`;
- [x] Windows/Ubuntu/macOS CI;
- [x] deterministic package restore;
- [x] repository version/release metadata, with package metadata only for artifacts intentionally published as packages;
- [x] repository-local `ProjectReference` relationships for suite-specific Shared/engine libraries unless a separate package boundary is independently justified;
- [x] no references back into the old CoreUtils repository.

A migration is **not complete merely because the files have moved**. It is complete only when the extracted repository builds and tests independently from a clean checkout using published external dependency packages together with its own repository-local project references.

A suite-specific Shared/engine assembly does not become a separately published package merely because multiple projects inside one repository consume it. Cross-repository reuse must be satisfied by the neutral published foundations or by another independently justified package boundary.

---

# D. Completion State

Completion Gate G is complete. The completed extraction sequence is:

- [x] G4.1 — `Icod.UtilLinux`
- [x] G4.2 — `Icod.Grep`
- [x] G4.3 — `Icod.Tar`
- [x] G5 — `Icod.ProcPs` CoreUtils extraction
- [x] G6 — `Icod.DiffUtils`
- [x] G7 — `Icod.LineEditor`
- [x] G8 — `Icod.Patch`

These suites have been moved out of the live `Icod.CoreUtils` source/solution after successful extraction validation. Remaining feature work in an extracted suite is owned by that suite's repository and does not require reintroducing its source into CoreUtils.

G9 and G10A/G10B/G10C are complete. No Completion Gate G work remains. Subsequent CoreUtils work should be scoped as a new milestone, while extracted-suite feature work remains in each suite's dedicated repository.
