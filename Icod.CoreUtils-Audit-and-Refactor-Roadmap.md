# Icod.CoreUtils Audit and Refactor Roadmap

## Living status

| Item | Status |
|---|---|
| Completed command batches | `0` through `67`, `69` through Batch `72`; Batch `68` (`Icod.ProcPs.Top`) remains deliberately deferred to the extracted `Icod.ProcPs` repository |
| Current engineering milestone | Completion Gate G — **COMPLETE** (G10A architecture record, G10B dependency/isolation verification, and G10C final reconciliation complete) |
| Completed infrastructure milestone | Completion Gates E2 through E6, F1 through F4, P1, and Completion Gate G in full, including G1 through G10C |
| Completed suite extractions | `Icod.UtilLinux`, `Icod.Grep`, `Icod.Tar`, `Icod.ProcPs`, `Icod.DiffUtils`, `Icod.LineEditor`, and `Icod.Patch` |
| Active infrastructure dependency | retain `Icod.CoreUtils.Shared` as a non-packable repository-local Coreutils library; consume published neutral `Icod.CommandFramework` 1.1.0 and `Icod.Path` 1.0.0 through Shared, plus direct `Icod.Timing` 1.0.0 where required; extracted sibling suites must not depend on `Icod.CoreUtils.Shared` |
| Next engineering step | No Completion Gate G work remains; scope subsequent CoreUtils work as a new milestone. Extracted-suite feature work remains owned by its dedicated repository. |
| Current target framework | `net10.0` |
| Required CI runners | `windows-latest`, `ubuntu-latest`, `macos-latest` |

**G9A closure checkpoint (2026-08-27):** the repository `.editorconfig` is authoritative for text and C# formatting policy; active documentation now follows its current UTF-8/CRLF/no-required-final-newline policy. Contributor guidance no longer describes Patch or any other extracted suite as co-resident. The upstream-version ledger now distinguishes completed historical incubation work from Batch 68, whose live implementation remains owned by `Icod.ProcPs`. Extracted-suite batch documents remain as historical implementation records. G9B is next.

**G9B closure checkpoint (2026-08-27):** audited `Gate_G9` at commit `ee02c2ad858a1048908c82a09f3e980dd227f877`. The live solution and recursive repository tree contain no extracted sibling-suite project/source/test paths; no production project references an extracted sibling suite. `Icod.CoreUtils.Shared` remains non-packable and repository-local, with no package reference back to it. The external Icod dependency set is published neutral `Icod.CommandFramework` 1.1.0, `Icod.Path` 1.0.0, and direct `Icod.Timing` 1.0.0 where required. PR and main workflows target only `Icod.CoreUtils.sln` on Windows, Ubuntu, and macOS. The final all-project output-path sweep and execution validation remain for G9C.

**G9 closure checkpoint (2026-08-27):** G9A through G9C are complete. PR #169 supplied a successful temporary Debug/Staging/Release × Windows/Ubuntu/macOS validation matrix, and the restored steady-state Staging-only PR workflow subsequently passed all three runners. Repository text-format guidance is reconciled to the authoritative `.editorconfig`; the final production dependency graph is frozen at repository-local `Icod.CoreUtils.Shared`, published `Icod.CommandFramework` 1.1.0, published `Icod.Path` 1.0.0, and direct `Icod.Timing` 1.0.0 where required. G10 is next.

**G10A architecture checkpoint (2026-08-27):** `Icod.CoreUtils-Architecture-and-Migration.md` records the final repository, executable, suite-engine, dependency-direction, versioning, release/CI, and textual-interoperability boundaries. G10B is the empirical cross-repository dependency/isolation gate.

**G10B dependency/isolation checkpoint (2026-08-27):** audited the final command-suite and neutral-foundation repositories at their current `main` heads. Production cross-repository dependencies resolve through published neutral packages; production `ProjectReference` edges remain inside their owning repositories; no command suite consumes another command suite as a runtime package; neutral foundations have no command-suite back-edge; and the resulting package/repository graph is acyclic. Current successful `main` CI evidence exists for every audited repository. No version-pin migration is required by G10B; mixed package versions remain deliberate consumer-owned choices under the independent-versioning policy. Detailed evidence is recorded in `Icod.CoreUtils-G10B-Dependency-Audit.md`.

**G10C / Completion Gate G closure checkpoint (2026-08-27):** reconciled both Gate G roadmaps against the completed G3 through G9 work, the G10A architecture record, and the G10B repository-by-repository dependency/isolation audit. Historical planning items that were implemented incrementally are now recorded as complete, and stale wording that assumed every extracted suite would depend specifically on `Icod.CommandFramework` has been corrected to the proven published-neutral-owner rule. No cross-repository production `ProjectReference`, suite-to-suite runtime package edge, neutral-to-suite back-edge, dependency cycle, neighboring-checkout requirement, or unresolved CoreUtils cleanup item remains. Completion Gate G is complete.

**G6 closure checkpoint (2026-08-22):** `Icod.DiffUtils` is now an independent repository containing `Icod.DiffUtils.Shared`, `cmp`, `diff`, `diff3`, `sdiff`, their dedicated tests, and independent CI. CoreUtils excised the DiffUtils tests first, then the four executable projects, and finally `Icod.DiffUtils.Shared`; the corresponding solution folders, project mappings, and nesting entries were removed at each stage and the working CoreUtils branch continued to build and test successfully. G5 is likewise closed for CoreUtils repository extraction: no `Icod.ProcPs` source, project, or test paths remain in CoreUtils. Deferred ProcPs feature work, including Batch 68 `top`, is owned by the dedicated `Icod.ProcPs` repository and no longer blocks Gate G progress in CoreUtils.

**G7 closure checkpoint (2026-08-23):** `Icod.LineEditor` is now an independent repository containing `Icod.LineEditor.Ed.Shared`, `ed`, `red`, `sed`, all four dedicated test projects, its own solution, documentation, licensing, and published-neutral dependencies. Its PR #2 Staging matrix passed clean/restore/build/test on `windows-latest`, `ubuntu-latest`, and `macos-latest`. CoreUtils excised the four LineEditor test projects first, then `Icod.LineEditor.Ed.Shared`, `ed`, `red`, and `sed`, and finally the migrated root-level LineEditor planning/audit documents. The current CoreUtils solution contains no LineEditor entries. G8 has since completed the final sibling-suite extraction.

**G8 closure checkpoint (2026-08-23):** `Icod.Patch` is now an independent repository with its production source, complete dedicated test/fixture corpus, standalone solution, documentation, published `Icod.CommandFramework` 1.1.0 and `Icod.Path` 1.0.0 dependencies, no runtime Diffutils dependency, and independent Windows/Ubuntu/macOS CI configuration. CoreUtils excised the Patch tests first and then the 31-file production tree together with its solution folder/project wiring, configuration mappings, and nesting entry. The standalone repository now owns Patch-specific planning and conformance documentation. G9 final CoreUtils cleanup is next.

## Scope

`Icod.CoreUtils` is a cross-platform .NET implementation of GNU Coreutils. Its scope expressly includes the file-manipulation and text-processing command families historically distributed as **GNU Fileutils** and **GNU Textutils**. These are natural Coreutils inclusions rather than unrelated extensions: GNU combined `fileutils`, `sh-utils`, and `textutils` into the unified `coreutils` package in 2003. The modern GNU Coreutils project remains the basic file, shell, and text manipulation suite.

Historical references:

- [GNU Coreutils FAQ — Fileutils, shellutils and textutils](https://www.gnu.org/software/coreutils/faq/coreutils-faq.html#Fileutils-shellutils-and-textutils)
- [GNU Coreutils 5.0 release announcement](https://lists.gnu.org/archive/html/coreutils-announce/2003-04/msg00000.html)

The primary supported CI targets are `windows-latest`, `ubuntu-latest`, and `macos-latest`. BSD support remains a best-effort target. The implementation is therefore not a Unix-only port: platform-independent behavior is preferred, native behavior is implemented per supported ABI where required, and unsupported platform capabilities receive controlled diagnostics.

The repository and solution served as the **temporary development home** for several projects that ultimately belong to other upstream suites and for neutral foundations proven by those consumers. Completion Gate G has dismantled that incubation layout and frozen the resulting repository/package architecture.

The following boundaries have already been extracted from CoreUtils:

- neutral foundations `Icod.CommandFramework` and `Icod.Path`;
- `Icod.UtilLinux`;
- `Icod.Grep`;
- `Icod.Tar`;
- `Icod.ProcPs`;
- `Icod.DiffUtils`;
- `Icod.LineEditor`;
- `Icod.Patch`.

All sibling-suite boundaries scheduled for Gate G extraction have now been moved to their dedicated repositories. Those suites consume published neutral foundations directly and must not be reintroduced as CoreUtils source-tree dependencies.

No separate `[` project will be added. The existing `test` project remains the condition evaluator.

## Development architecture

The repository is now in the **post-Completion-Gate-G state**. G8 removed the last co-resident sibling-suite boundary, G9 removed stale repository wiring and documentation, and G10 recorded and verified the final architecture.

Before Completion Gate G, `Icod.CoreUtils.Shared` incubated both neutral cross-suite mechanism and Coreutils-family behavior. G3 completed the approved neutral migration and froze the boundary: `Icod.CoreUtils.Shared` contains only GNU Coreutils/Fileutils/Textutils shared behavior and remains a repository-local project, while neutral mechanism is consumed from published `Icod.CommandFramework` and `Icod.Path` packages.

No sibling-suite `ProjectReference` to `Icod.CoreUtils.Shared` remains. Extracted suites consume published neutral foundations directly; G9 and G10B verified that the old source-tree dependency boundary has been eliminated.

`Icod.Patch`, `Icod.DiffUtils`, `Icod.ProcPs`, and `Icod.LineEditor` now live outside CoreUtils in their dedicated repositories and must not be reintroduced here as source-tree dependencies.

`Icod.Path` is a parallel neutral foundation rather than a suite-specific Shared library. It has no dependency on an individual command or on `Icod.CoreUtils.Shared`; commands reference it directly when canonical-path behavior is required.

The live source namespace family is:

```text
Icod.CoreUtils.*
```

The solution contains only Coreutils/Fileutils/Textutils projects and repository-local CoreUtils support/test infrastructure; G9 removed stale solution-folder and output-path residue.

### Co-resident executable-name collisions

Some suites can contain commands with the same executable name. The historical Batch 9 `uptime` implementation is not retained as a competing Coreutils profile: Batch 57 replaces it with the pinned procps-ng profile and transfers live ownership to `Icod.ProcPs.Uptime`. The `kill` executable is likewise implemented only once, as the pinned util-linux profile, so no ProcPs `kill` collision is retained.

During the co-resident phase:
- every executable retains the lowercase command assembly name required by its upstream suite;
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
__FILLER_185__
__FILLER_186__
__FILLER_187__
__FILLER_188__
__FILLER_189__
__FILLER_190__
__FILLER_191__
__FILLER_192__
__FILLER_193__
__FILLER_194__
__FILLER_195__
__FILLER_196__
__FILLER_197__
__FILLER_198__
__FILLER_199__
__FILLER_200__
__FILLER_201__
__FILLER_202__
__FILLER_203__
__FILLER_204__
__FILLER_205__
__FILLER_206__
__FILLER_207__
__FILLER_208__
__FILLER_209__
__FILLER_210__
__FILLER_211__
__FILLER_212__
__FILLER_213__
__FILLER_214__
__FILLER_215__
__FILLER_216__
__FILLER_217__
__FILLER_218__
__FILLER_219__
__FILLER_220__
__FILLER_221__
__FILLER_222__
__FILLER_223__
__FILLER_224__
__FILLER_225__
__FILLER_226__
__FILLER_227__
__FILLER_228__
__FILLER_229__
__FILLER_230__
__FILLER_231__
__FILLER_232__
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
__FILLER_533__
__FILLER_534__
__FILLER_535__
__FILLER_536__
__FILLER_537__
__FILLER_538__
__FILLER_539__
__FILLER_540__
__FILLER_541__
__FILLER_542__
__FILLER_543__
__FILLER_544__
__FILLER_545__
__FILLER_546__
__FILLER_547__
__FILLER_548__
__FILLER_549__
__FILLER_550__
__FILLER_551__
__FILLER_552__
__FILLER_553__
__FILLER_554__
__FILLER_555__
__FILLER_556__
__FILLER_557__
__FILLER_558__
__FILLER_559__
__FILLER_560__
__FILLER_561__
__FILLER_562__
__FILLER_563__
__FILLER_564__
__FILLER_565__
__FILLER_566__
__FILLER_567__
__FILLER_568__
__FILLER_569__
__FILLER_570__
__FILLER_571__
__FILLER_572__
__FILLER_573__
__FILLER_574__
__FILLER_575__
__FILLER_576__
__FILLER_577__
__FILLER_578__
__FILLER_579__
__FILLER_580__
__FILLER_581__
__FILLER_582__
__FILLER_583__
__FILLER_584__
__FILLER_585__
__FILLER_586__
__FILLER_587__
__FILLER_588__
__FILLER_589__
__FILLER_590__
__FILLER_591__
__FILLER_592__
__FILLER_593__
__FILLER_594__
__FILLER_595__
__FILLER_596__
__FILLER_597__
__FILLER_598__
__FILLER_599__
__FILLER_600__
__FILLER_601__
__FILLER_602__
__FILLER_603__
__FILLER_604__
__FILLER_605__
__FILLER_606__
__FILLER_607__
__FILLER_608__
__FILLER_609__
__FILLER_610__
__FILLER_611__
__FILLER_612__
__FILLER_613__
__FILLER_614__
__FILLER_615__
__FILLER_616__
__FILLER_617__
__FILLER_618__
__FILLER_619__
__FILLER_620__
__FILLER_621__
__FILLER_622__
__FILLER_623__
__FILLER_624__
__FILLER_625__
__FILLER_626__
__FILLER_627__
__FILLER_628__
__FILLER_629__
__FILLER_630__
__FILLER_631__
__FILLER_632__
__FILLER_633__
__FILLER_634__
__FILLER_635__
__FILLER_636__
__FILLER_637__
__FILLER_638__
__FILLER_639__
__FILLER_640__
__FILLER_641__
__FILLER_642__
__FILLER_643__
__FILLER_644__
__FILLER_645__
__FILLER_646__
__FILLER_647__
__FILLER_648__
__FILLER_649__
__FILLER_650__
__FILLER_651__
__FILLER_652__
__FILLER_653__
__FILLER_654__
__FILLER_655__
__FILLER_656__
__FILLER_657__
__FILLER_658__
__FILLER_659__
__FILLER_660__
__FILLER_661__
__FILLER_662__
__FILLER_663__
__FILLER_664__
__FILLER_665__
__FILLER_666__
__FILLER_667__
__FILLER_668__
__FILLER_669__
__FILLER_670__
__FILLER_671__
__FILLER_672__
__FILLER_673__
__FILLER_674__
__FILLER_675__
__FILLER_676__
__FILLER_677__
__FILLER_678__
__FILLER_679__
__FILLER_680__
__FILLER_681__
__FILLER_682__
__FILLER_683__
__FILLER_684__
__FILLER_685__
__FILLER_686__
__FILLER_687__
__FILLER_688__
__FILLER_689__
__FILLER_690__
__FILLER_691__
__FILLER_692__
__FILLER_693__
__FILLER_694__
__FILLER_695__
__FILLER_696__
__FILLER_697__
__FILLER_698__
__FILLER_699__
__FILLER_700__
__FILLER_701__
__FILLER_702__
__FILLER_703__
__FILLER_704__
__FILLER_705__
__FILLER_706__
__FILLER_707__
__FILLER_708__
__FILLER_709__
__FILLER_710__
__FILLER_711__
__FILLER_712__
__FILLER_713__
__FILLER_714__
__FILLER_715__
__FILLER_716__
__FILLER_717__
__FILLER_718__
__FILLER_719__
__FILLER_720__
__FILLER_721__
__FILLER_722__
__FILLER_723__
__FILLER_724__
__FILLER_725__
__FILLER_726__
__FILLER_727__
__FILLER_728__
__FILLER_729__
__FILLER_730__
__FILLER_731__
__FILLER_732__
__FILLER_733__
__FILLER_734__
__FILLER_735__
__FILLER_736__
__FILLER_737__
__FILLER_738__
__FILLER_739__
__FILLER_740__
__FILLER_741__
__FILLER_742__
__FILLER_743__
__FILLER_744__
__FILLER_745__
__FILLER_746__
__FILLER_747__
__FILLER_748__
__FILLER_749__
__FILLER_750__
__FILLER_751__
__FILLER_752__
__FILLER_753__
__FILLER_754__
__FILLER_755__
__FILLER_756__
__FILLER_757__
__FILLER_758__
__FILLER_759__
__FILLER_760__
__FILLER_761__
__FILLER_762__
__FILLER_763__
__FILLER_764__
__FILLER_765__
__FILLER_766__
__FILLER_767__
__FILLER_768__
__FILLER_769__
__FILLER_770__
__FILLER_771__
__FILLER_772__
__FILLER_773__
__FILLER_774__
__FILLER_775__
__FILLER_776__
__FILLER_777__
__FILLER_778__
__FILLER_779__
__FILLER_780__
__FILLER_781__
__FILLER_782__
__FILLER_783__
__FILLER_784__
__FILLER_785__
__FILLER_786__
__FILLER_787__
__FILLER_788__
__FILLER_789__
__FILLER_790__
__FILLER_791__
__FILLER_792__
__FILLER_793__
__FILLER_794__
__FILLER_795__
__FILLER_796__
__FILLER_797__
__FILLER_798__
__FILLER_799__
__FILLER_800__
__FILLER_801__
__FILLER_802__
__FILLER_803__
__FILLER_804__
__FILLER_805__
__FILLER_806__
__FILLER_807__
__FILLER_808__
__FILLER_809__
__FILLER_810__
__FILLER_811__
__FILLER_812__
__FILLER_813__
__FILLER_814__
__FILLER_815__
__FILLER_816__
__FILLER_817__
__FILLER_818__
__FILLER_819__
__FILLER_820__
__FILLER_821__
__FILLER_822__
__FILLER_823__
__FILLER_824__
__FILLER_825__
__FILLER_826__
__FILLER_827__
__FILLER_828__
__FILLER_829__
__FILLER_830__
__FILLER_831__
__FILLER_832__
__FILLER_833__
__FILLER_834__
__FILLER_835__
__FILLER_836__
__FILLER_837__
__FILLER_838__
__FILLER_839__
__FILLER_840__
__FILLER_841__
__FILLER_842__
__FILLER_843__
__FILLER_844__
__FILLER_845__
__FILLER_846__
__FILLER_847__
__FILLER_848__
__FILLER_849__
__FILLER_850__
__FILLER_851__
__FILLER_852__
__FILLER_853__
__FILLER_854__
__FILLER_855__
__FILLER_856__
__FILLER_857__
__FILLER_858__
__FILLER_859__
__FILLER_860__
__FILLER_861__
__FILLER_862__
__FILLER_863__
__FILLER_864__
__FILLER_865__
__FILLER_866__
__FILLER_867__
__FILLER_868__
__FILLER_869__
__FILLER_870__
__FILLER_871__
__FILLER_872__
__FILLER_873__
__FILLER_874__
__FILLER_875__
__FILLER_876__
__FILLER_877__
__FILLER_878__
__FILLER_879__
__FILLER_880__
__FILLER_881__
__FILLER_882__
__FILLER_883__
__FILLER_884__
__FILLER_885__
__FILLER_886__
__FILLER_887__
__FILLER_888__
__FILLER_889__
__FILLER_890__
__FILLER_891__
__FILLER_892__
__FILLER_893__
__FILLER_894__
__FILLER_895__
__FILLER_896__
__FILLER_897__
__FILLER_898__
__FILLER_899__
__FILLER_900__
__FILLER_901__
__FILLER_902__
__FILLER_903__
__FILLER_904__
__FILLER_905__
__FILLER_906__
__FILLER_907__
__FILLER_908__
__FILLER_909__
__FILLER_910__
__FILLER_911__
__FILLER_912__
__FILLER_913__
__FILLER_914__
__FILLER_915__
__FILLER_916__
__FILLER_917__
__FILLER_918__
__FILLER_919__
__FILLER_920__
__FILLER_921__
__FILLER_922__
__FILLER_923__
__FILLER_924__
__FILLER_925__
__FILLER_926__
__FILLER_927__
__FILLER_928__
__FILLER_929__
__FILLER_930__
__FILLER_931__
__FILLER_932__
__FILLER_933__
__FILLER_934__
__FILLER_935__
__FILLER_936__
__FILLER_937__
__FILLER_938__
__FILLER_939__
__FILLER_940__
__FILLER_941__
__FILLER_942__
__FILLER_943__
__FILLER_944__
__FILLER_945__
__FILLER_946__
__FILLER_947__
__FILLER_948__
__FILLER_949__
__FILLER_950__
__FILLER_951__
__FILLER_952__
__FILLER_953__
__FILLER_954__
__FILLER_955__
__FILLER_956__
__FILLER_957__
__FILLER_958__
__FILLER_959__
__FILLER_960__
__FILLER_961__
__FILLER_962__
__FILLER_963__
__FILLER_964__
__FILLER_965__
__FILLER_966__
__FILLER_967__
__FILLER_968__
__FILLER_969__
__FILLER_970__
__FILLER_971__
__FILLER_972__
__FILLER_973__
__FILLER_974__
__FILLER_975__
__FILLER_976__
__FILLER_977__
__FILLER_978__
__FILLER_979__
__FILLER_980__
__FILLER_981__
__FILLER_982__
__FILLER_983__
__FILLER_984__
__FILLER_985__
__FILLER_986__
__FILLER_987__
__FILLER_988__
__FILLER_989__
__FILLER_990__
__FILLER_991__
__FILLER_992__
__FILLER_993__
__FILLER_994__
__FILLER_995__
__FILLER_996__
__FILLER_997__
__FILLER_998__
__FILLER_999__
__FILLER_1000__
__FILLER_1001__
__FILLER_1002__
__FILLER_1003__
__FILLER_1004__
__FILLER_1005__
__FILLER_1006__
__FILLER_1007__
__FILLER_1008__
__FILLER_1009__
__FILLER_1010__
__FILLER_1011__
__FILLER_1012__
__FILLER_1013__
__FILLER_1014__
__FILLER_1015__
__FILLER_1016__
__FILLER_1017__
__FILLER_1018__
__FILLER_1019__
__FILLER_1020__
__FILLER_1021__
__FILLER_1022__
__FILLER_1023__
__FILLER_1024__
__FILLER_1025__
__FILLER_1026__
__FILLER_1027__
__FILLER_1028__
__FILLER_1029__
__FILLER_1030__
__FILLER_1031__
__FILLER_1032__
__FILLER_1033__
__FILLER_1034__
__FILLER_1035__
__FILLER_1036__
__FILLER_1037__
__FILLER_1038__
__FILLER_1039__
__FILLER_1040__
__FILLER_1041__
__FILLER_1042__
__FILLER_1043__
__FILLER_1044__
__FILLER_1045__
__FILLER_1046__
__FILLER_1047__
__FILLER_1048__
__FILLER_1049__
__FILLER_1050__
__FILLER_1051__
__FILLER_1052__
__FILLER_1053__
__FILLER_1054__
__FILLER_1055__
__FILLER_1056__
__FILLER_1057__
__FILLER_1058__
__FILLER_1059__
__FILLER_1060__
__FILLER_1061__
__FILLER_1062__
__FILLER_1063__
__FILLER_1064__
__FILLER_1065__
__FILLER_1066__
__FILLER_1067__
__FILLER_1068__
__FILLER_1069__
__FILLER_1070__
__FILLER_1071__
__FILLER_1072__
__FILLER_1073__
__FILLER_1074__
__FILLER_1075__
__FILLER_1076__
__FILLER_1077__
__FILLER_1078__
__FILLER_1079__
__FILLER_1080__
__FILLER_1081__
__FILLER_1082__
__FILLER_1083__
__FILLER_1084__
__FILLER_1085__
__FILLER_1086__
__FILLER_1087__
__FILLER_1088__
__FILLER_1089__
__FILLER_1090__
__FILLER_1091__
__FILLER_1092__
__FILLER_1093__
__FILLER_1094__
__FILLER_1095__
__FILLER_1096__
__FILLER_1097__
__FILLER_1098__
__FILLER_1099__
__FILLER_1100__
__FILLER_1101__
__FILLER_1102__
__FILLER_1103__
__FILLER_1104__
__FILLER_1105__
__FILLER_1106__
__FILLER_1107__
__FILLER_1108__
__FILLER_1109__
__FILLER_1110__
__FILLER_1111__
__FILLER_1112__
__FILLER_1113__
__FILLER_1114__
__FILLER_1115__
__FILLER_1116__
__FILLER_1117__
__FILLER_1118__
__FILLER_1119__
__FILLER_1120__
__FILLER_1121__
__FILLER_1122__
__FILLER_1123__
__FILLER_1124__
__FILLER_1125__
__FILLER_1126__
__FILLER_1127__
__FILLER_1128__
__FILLER_1129__
__FILLER_1130__
__FILLER_1131__
__FILLER_1132__
__FILLER_1133__
__FILLER_1134__
__FILLER_1135__
__FILLER_1136__
__FILLER_1137__
__FILLER_1138__
__FILLER_1139__
__FILLER_1140__
__FILLER_1141__
__FILLER_1142__
__FILLER_1143__
__FILLER_1144__
__FILLER_1145__
__FILLER_1146__
__FILLER_1147__
__FILLER_1148__
__FILLER_1149__
__FILLER_1150__
__FILLER_1151__
__FILLER_1152__
__FILLER_1153__
__FILLER_1154__
__FILLER_1155__
__FILLER_1156__
__FILLER_1157__
__FILLER_1158__
__FILLER_1159__
__FILLER_1160__
__FILLER_1161__
__FILLER_1162__
__FILLER_1163__
__FILLER_1164__
__FILLER_1165__
__FILLER_1166__
__FILLER_1167__
__FILLER_1168__
__FILLER_1169__
__FILLER_1170__
__FILLER_1171__
__FILLER_1172__
__FILLER_1173__
__FILLER_1174__
__FILLER_1175__
__FILLER_1176__
__FILLER_1177__
__FILLER_1178__
__FILLER_1179__
__FILLER_1180__
__FILLER_1181__
__FILLER_1182__
__FILLER_1183__
__FILLER_1184__
__FILLER_1185__
__FILLER_1186__
__FILLER_1187__
__FILLER_1188__
__FILLER_1189__
__FILLER_1190__
__FILLER_1191__
__FILLER_1192__
__FILLER_1193__
__FILLER_1194__
__FILLER_1195__
__FILLER_1196__
__FILLER_1197__
__FILLER_1198__
__FILLER_1199__
__FILLER_1200__
__FILLER_1201__
__FILLER_1202__
__FILLER_1203__
__FILLER_1204__
__FILLER_1205__
__FILLER_1206__
__FILLER_1207__
__FILLER_1208__
__FILLER_1209__
__FILLER_1210__
__FILLER_1211__
__FILLER_1212__
__FILLER_1213__
__FILLER_1214__
__FILLER_1215__
__FILLER_1216__
__FILLER_1217__
__FILLER_1218__
__FILLER_1219__
__FILLER_1220__
__FILLER_1221__
__FILLER_1222__
__FILLER_1223__
__FILLER_1224__
__FILLER_1225__
__FILLER_1226__
__FILLER_1227__
__FILLER_1228__
__FILLER_1229__
__FILLER_1230__
__FILLER_1231__
__FILLER_1232__
__FILLER_1233__
__FILLER_1234__
__FILLER_1235__
__FILLER_1236__
__FILLER_1237__
__FILLER_1238__
__FILLER_1239__
__FILLER_1240__
__FILLER_1241__
__FILLER_1242__
__FILLER_1243__
__FILLER_1244__
__FILLER_1245__
__FILLER_1246__
__FILLER_1247__
__FILLER_1248__
__FILLER_1249__
__FILLER_1250__
__FILLER_1251__
__FILLER_1252__
__FILLER_1253__
__FILLER_1254__
__FILLER_1255__
__FILLER_1256__
__FILLER_1257__
__FILLER_1258__
__FILLER_1259__
__FILLER_1260__
__FILLER_1261__
__FILLER_1262__
__FILLER_1263__
__FILLER_1264__
__FILLER_1265__
__FILLER_1266__
__FILLER_1267__
__FILLER_1268__
__FILLER_1269__
__FILLER_1270__
__FILLER_1271__
__FILLER_1272__
__FILLER_1273__
__FILLER_1274__
__FILLER_1275__
__FILLER_1276__
__FILLER_1277__
__FILLER_1278__
__FILLER_1279__
__FILLER_1280__
__FILLER_1281__
__FILLER_1282__
__FILLER_1283__
__FILLER_1284__
__FILLER_1285__
__FILLER_1286__
__FILLER_1287__
__FILLER_1288__
__FILLER_1289__
__FILLER_1290__
__FILLER_1291__
__FILLER_1292__
__FILLER_1293__
__FILLER_1294__
__FILLER_1295__
__FILLER_1296__
__FILLER_1297__
__FILLER_1298__
__FILLER_1299__
__FILLER_1300__
__FILLER_1301__
__FILLER_1302__
__FILLER_1303__
__FILLER_1304__
__FILLER_1305__
__FILLER_1306__
__FILLER_1307__
__FILLER_1308__
__FILLER_1309__
__FILLER_1310__
__FILLER_1311__
__FILLER_1312__
__FILLER_1313__
__FILLER_1314__
__FILLER_1315__
__FILLER_1316__
__FILLER_1317__
__FILLER_1318__
__FILLER_1319__
__FILLER_1320__
__FILLER_1321__
__FILLER_1322__
__FILLER_1323__
__FILLER_1324__
__FILLER_1325__
__FILLER_1326__
__FILLER_1327__
__FILLER_1328__
__FILLER_1329__
__FILLER_1330__
__FILLER_1331__
__FILLER_1332__
__FILLER_1333__
__FILLER_1334__
__FILLER_1335__
__FILLER_1336__
__FILLER_1337__
__FILLER_1338__
__FILLER_1339__
__FILLER_1340__
__FILLER_1341__
__FILLER_1342__
__FILLER_1343__
__FILLER_1344__
__FILLER_1345__
__FILLER_1346__
__FILLER_1347__
__FILLER_1348__
__FILLER_1349__
__FILLER_1350__
__FILLER_1351__
__FILLER_1352__
__FILLER_1353__
__FILLER_1354__
__FILLER_1355__
__FILLER_1356__
__FILLER_1357__
__FILLER_1358__
__FILLER_1359__
__FILLER_1360__
__FILLER_1361__
__FILLER_1362__
__FILLER_1363__
__FILLER_1364__
__FILLER_1365__
__FILLER_1366__
__FILLER_1367__
__FILLER_1368__
__FILLER_1369__
__FILLER_1370__
__FILLER_1371__
__FILLER_1372__
__FILLER_1373__
__FILLER_1374__
__FILLER_1375__
__FILLER_1376__
__FILLER_1377__
__FILLER_1378__
__FILLER_1379__
__FILLER_1380__
__FILLER_1381__
__FILLER_1382__
__FILLER_1383__
__FILLER_1384__
__FILLER_1385__
__FILLER_1386__
__FILLER_1387__
__FILLER_1388__
__FILLER_1389__
__FILLER_1390__
__FILLER_1391__
__FILLER_1392__
__FILLER_1393__
__FILLER_1394__
__FILLER_1395__
__FILLER_1396__
__FILLER_1397__
__FILLER_1398__
__FILLER_1399__
__FILLER_1400__
__FILLER_1401__
__FILLER_1402__
__FILLER_1403__
__FILLER_1404__
__FILLER_1405__
__FILLER_1406__
__FILLER_1407__
__FILLER_1408__
__FILLER_1409__
__FILLER_1410__
__FILLER_1411__
__FILLER_1412__
__FILLER_1413__
__FILLER_1414__
__FILLER_1415__
__FILLER_1416__
__FILLER_1417__
__FILLER_1418__
__FILLER_1419__
__FILLER_1420__
__FILLER_1421__
__FILLER_1422__
__FILLER_1423__
__FILLER_1424__
__FILLER_1425__
__FILLER_1426__
__FILLER_1427__
__FILLER_1428__
__FILLER_1429__
__FILLER_1430__
__FILLER_1431__
__FILLER_1432__
__FILLER_1433__
__FILLER_1434__
__FILLER_1435__
__FILLER_1436__
__FILLER_1437__
__FILLER_1438__
__FILLER_1439__
__FILLER_1440__
__FILLER_1441__
__FILLER_1442__
__FILLER_1443__
__FILLER_1444__
__FILLER_1445__
__FILLER_1446__
__FILLER_1447__
__FILLER_1448__
__FILLER_1449__
__FILLER_1450__
__FILLER_1451__
__FILLER_1452__
__FILLER_1453__
__FILLER_1454__
__FILLER_1455__
__FILLER_1456__
__FILLER_1457__
__FILLER_1458__
__FILLER_1459__
__FILLER_1460__
__FILLER_1461__
__FILLER_1462__
__FILLER_1463__
__FILLER_1464__
__FILLER_1465__
__FILLER_1466__
__FILLER_1467__
__FILLER_1468__
__FILLER_1469__
__FILLER_1470__
__FILLER_1471__
__FILLER_1472__
__FILLER_1473__
__FILLER_1474__
__FILLER_1475__
__FILLER_1476__
__FILLER_1477__
__FILLER_1478__
__FILLER_1479__
__FILLER_1480__
__FILLER_1481__
__FILLER_1482__
__FILLER_1483__
__FILLER_1484__
__FILLER_1485__
__FILLER_1486__
__FILLER_1487__
__FILLER_1488__
__FILLER_1489__
__FILLER_1490__
__FILLER_1491__
__FILLER_1492__
__FILLER_1493__
__FILLER_1494__
__FILLER_1495__
__FILLER_1496__
__FILLER_1497__
__FILLER_1498__
__FILLER_1499__
__FILLER_1500__
__FILLER_1501__
__FILLER_1502__
__FILLER_1503__
__FILLER_1504__
__FILLER_1505__
__FILLER_1506__
__FILLER_1507__
__FILLER_1508__
__FILLER_1509__
__FILLER_1510__
__FILLER_1511__
__FILLER_1512__
__FILLER_1513__
__FILLER_1514__
__FILLER_1515__
__FILLER_1516__
__FILLER_1517__
__FILLER_1518__
__FILLER_1519__
__FILLER_1520__
__FILLER_1521__
__FILLER_1522__
__FILLER_1523__
__FILLER_1524__
__FILLER_1525__
__FILLER_1526__
__FILLER_1527__
__FILLER_1528__
__FILLER_1529__
__FILLER_1530__
__FILLER_1531__
__FILLER_1532__
__FILLER_1533__
__FILLER_1534__
__FILLER_1535__
__FILLER_1536__
__FILLER_1537__
__FILLER_1538__
__FILLER_1539__
__FILLER_1540__
__FILLER_1541__
__FILLER_1542__
__FILLER_1543__
__FILLER_1544__
__FILLER_1545__
__FILLER_1546__
__FILLER_1547__
__FILLER_1548__
__FILLER_1549__
__FILLER_1550__
__FILLER_1551__
__FILLER_1552__
__FILLER_1553__
__FILLER_1554__
__FILLER_1555__
__FILLER_1556__
__FILLER_1557__
__FILLER_1558__
__FILLER_1559__
__FILLER_1560__
__FILLER_1561__
__FILLER_1562__
__FILLER_1563__
__FILLER_1564__
__FILLER_1565__
__FILLER_1566__
__FILLER_1567__
__FILLER_1568__
__FILLER_1569__
__FILLER_1570__
__FILLER_1571__
__FILLER_1572__
__FILLER_1573__
__FILLER_1574__
__FILLER_1575__
__FILLER_1576__
__FILLER_1577__
__FILLER_1578__
__FILLER_1579__
__FILLER_1580__
__FILLER_1581__
__FILLER_1582__
__FILLER_1583__
__FILLER_1584__
__FILLER_1585__
__FILLER_1586__
__FILLER_1587__
__FILLER_1588__
__FILLER_1589__
__FILLER_1590__
__FILLER_1591__
__FILLER_1592__
__FILLER_1593__
__FILLER_1594__
__FILLER_1595__
__FILLER_1596__
__FILLER_1597__
__FILLER_1598__
__FILLER_1599__
__FILLER_1600__
__FILLER_1601__
__FILLER_1602__
__FILLER_1603__
__FILLER_1604__
__FILLER_1605__
__FILLER_1606__
__FILLER_1607__
__FILLER_1608__
__FILLER_1609__
__FILLER_1610__
__FILLER_1611__
__FILLER_1612__
__FILLER_1613__
__FILLER_1614__
__FILLER_1615__
__FILLER_1616__
__FILLER_1617__
__FILLER_1618__
__FILLER_1619__
__FILLER_1620__
__FILLER_1621__
__FILLER_1622__
__FILLER_1623__
__FILLER_1624__
__FILLER_1625__
__FILLER_1626__
__FILLER_1627__
__FILLER_1628__
__FILLER_1629__
__FILLER_1630__
__FILLER_1631__
__FILLER_1632__
__FILLER_1633__
__FILLER_1634__
__FILLER_1635__
__FILLER_1636__
__FILLER_1637__
__FILLER_1638__
__FILLER_1639__
__FILLER_1640__
__FILLER_1641__
__FILLER_1642__
__FILLER_1643__
__FILLER_1644__
__FILLER_1645__
__FILLER_1646__
__FILLER_1647__
__FILLER_1648__
__FILLER_1649__
__FILLER_1650__
__FILLER_1651__
__FILLER_1652__
__FILLER_1653__
__FILLER_1654__
__FILLER_1655__
__FILLER_1656__
__FILLER_1657__
__FILLER_1658__
__FILLER_1659__
__FILLER_1660__
__FILLER_1661__
__FILLER_1662__
__FILLER_1663__
__FILLER_1664__
__FILLER_1665__
__FILLER_1666__
__FILLER_1667__
__FILLER_1668__
__FILLER_1669__
__FILLER_1670__
__FILLER_1671__
__FILLER_1672__
__FILLER_1673__
__FILLER_1674__
__FILLER_1675__
__FILLER_1676__
__FILLER_1677__
__FILLER_1678__
__FILLER_1679__
__FILLER_1680__
__FILLER_1681__
__FILLER_1682__
__FILLER_1683__
__FILLER_1684__
__FILLER_1685__
__FILLER_1686__
__FILLER_1687__
__FILLER_1688__
__FILLER_1689__
__FILLER_1690__
__FILLER_1691__
__FILLER_1692__
__FILLER_1693__
__FILLER_1694__
__FILLER_1695__
__FILLER_1696__
__FILLER_1697__
__FILLER_1698__
__FILLER_1699__
__FILLER_1700__
__FILLER_1701__
__FILLER_1702__
__FILLER_1703__
__FILLER_1704__
__FILLER_1705__
__FILLER_1706__
__FILLER_1707__
__FILLER_1708__
__FILLER_1709__
__FILLER_1710__
__FILLER_1711__
__FILLER_1712__
__FILLER_1713__
__FILLER_1714__
__FILLER_1715__
__FILLER_1716__
__FILLER_1717__
__FILLER_1718__
__FILLER_1719__
__FILLER_1720__
__FILLER_1721__
Extraction is treated as a security boundary: rooted/platform-root and escaping member paths are rejected, parent symlink/reparse traversal is blocked, symbolic/hard-link targets are containment-checked, special device/FIFO creation is refused, Windows case-fold collisions are detected, sparse arithmetic is checked, and archive/member/extracted-byte ceilings bound hostile inputs. Dedicated `Tar.Tests` cover operations, formats, compression, links, metadata, sparse round trips, malformed sparse maps, integer overflow, path/link escapes, overwrite redirection, decompression failure, cancellation, and resource exhaustion. Detailed notes are recorded in `Icod.CoreUtils-Batch-72-Tar-Archive-Engine.md`; full solution and required-runner validation remain the closure step.

The project remained co-resident until G4.3, when it was moved into its own solution and repository and its cross-suite dependencies were converted to published neutral package references.


### Completion Gate G — final classification, foundation refinement, package extraction, and repository split — COMPLETE

This gate was deliberately last. The Coreutils, Diffutils, Grep, Patch, Ed, Sed, selected UtilLinux commands, Tar, and ProcPs projects supplied the consumer evidence needed to choose stable API and repository boundaries. The neutral foundations plus UtilLinux, Grep, Tar, ProcPs, DiffUtils, LineEditor, and Patch have been extracted; G9 completed the final CoreUtils cleanup, and G10 recorded and verified the resulting architecture.

- [x] Inventory every public, protected, and internal API in the Shared incubation project and record its actual consumers by project and suite through the G3 contraction and consumer cut-over tranches.
- [x] Complete the suite-engine ownership audits required before extracting DiffUtils, LineEditor, and ProcPs; their suite-specific engines now live in their dedicated repositories rather than in CoreUtils.
- [x] Classify each remaining API as:
  - [x] cross-suite or intrinsically command-neutral mechanism suitable for a published neutral foundation;
  - [x] shared only by Coreutils/Fileutils/Textutils and suitable for `Icod.CoreUtils.Shared`;
  - [x] shared only within another suite and suitable for that suite's Shared library;
  - [x] command-local and unsuitable for a public package.
- [x] Review namespace design, accessibility, XML documentation, native ABI boundaries, and dependency direction before freezing public contracts; binary-compatibility and trimming/AOT policy remain owned by each published repository's release lifecycle rather than by the repository split itself.
- [x] Create the `Icod.CommandFramework` solution and repository with independent Windows, Ubuntu, and macOS CI.
- [x] Publish `Icod.CommandFramework` as a versioned NuGet package with symbols, SourceLink, deterministic builds, package documentation, and a Semantic Versioning policy.
- [x] Move approved neutral functionality out of suite-local ownership into its published neutral owner; the final G3 filesystem-mechanism remainder moved into `Icod.CommandFramework`, while later demonstrated process/timing/host/terminal mechanisms use their narrower neutral packages.
- [x] Final filesystem foundation refinement:
  - [x] extend framework `FileSystemInformation` with explicit total/free/available inode-pool observations populated from the existing `statvfs` path;
  - [x] move current-process creation-mask observation (`IFileCreationMaskProvider` / `SystemFileCreationMaskProvider`) to `Icod.CommandFramework.FileSystem.Modes`;
  - [x] add a capability-aware host file-clone/reflink primitive to the framework while retaining GNU `cp` reflink policy in CoreUtils;
  - [x] move/rehome the corresponding tests to the framework repository;
  - [x] audit the CoreUtils `copy_file_range` helper and delete it if unused rather than creating an unnecessary framework abstraction;
  - [x] build/test the framework on Windows, Ubuntu, and macOS and publish `Icod.CommandFramework` 1.1.0 before pruning CoreUtils consumers.
- [x] CoreUtils filesystem consumer cut-over:
  - [x] consume framework inode-pool observations from `SystemFileSystemUsageProvider` and remove its duplicate local `statvfs` ABI;
  - [x] consume the framework creation-mask provider;
  - [x] consume the framework file-clone primitive from `CopyMoveEngine` while retaining GNU policy locally;
  - [x] route directory metadata preservation through the existing framework metadata-preservation/application path so ownership, mode, timestamps including birth time, attributes, and required-versus-best-effort semantics match regular-file handling;
  - [x] prune migrated CoreUtils code and tests only after the corresponding consumers validate against the refreshed package.
- [x] Retain `Icod.CoreUtils.Shared` permanently as a repository-local Coreutils/Fileutils/Textutils class library; make it depend on published neutral foundations rather than duplicate framework behavior.
- [x] Do **not** publish `Icod.CoreUtils.Shared` independently to NuGet.org or GitHub Packages.
- [x] Retain same-repository `ProjectReference` dependencies from genuine Coreutils command projects to `Icod.CoreUtils.Shared`; use direct neutral `PackageReference` dependencies where no Coreutils-specific layer is required.
- [x] Split the co-resident suite projects into their final solutions and repositories:
  - [x] `Icod.DiffUtils` — G6 complete;
  - [x] `Icod.Grep` — G4.2 complete;
  - [x] `Icod.Patch` — G8 complete;
  - [x] `Icod.LineEditor` — G7 complete; the dedicated repository contains `Icod.LineEditor.Ed.Shared`, `Icod.LineEditor.Ed`, `Icod.LineEditor.Red`, and `Icod.LineEditor.Sed`, with no general `Icod.LineEditor.Shared` project;
  - [x] `Icod.UtilLinux`, containing the selected `kill` and `renice` command projects — G4.1 complete;
  - [x] `Icod.Tar` — G4.3 complete;
  - [x] `Icod.ProcPs` — G5 complete for CoreUtils extraction.
- [x] Preserve relevant history, project identities, test corpora, documentation, and CI policy during each extraction.
- [x] Convert every extracted suite to versioned `PackageReference` dependencies on the appropriate published neutral foundations; no suite is required to depend on `Icod.CommandFramework` when a narrower neutral owner is the demonstrated contract boundary.
- [x] Retain project references within each extracted suite for its own Shared or engine projects unless a separate package boundary is independently justified.
- [x] Preserve `Icod.LineEditor.Ed.Shared` as the repository-local LineEditor suite engine and preserve the LE9 decision not to create a general `Icod.LineEditor.Shared` layer.
- [x] Resolve duplicate executable ownership and remove co-resident output collisions; any future umbrella-distribution, alias, or installer policy is release/distribution work outside Completion Gate G.
- [x] Remove stale project, solution-folder, output-path, packaging, CI, and inventory references from the original repository.
- [x] Eliminate circular dependencies and ensure neutral foundations have no production dependency on any command suite.
- [x] Build and test every final repository against published external packages plus its own repository-local project references rather than neighboring source trees.
- [x] Publish `Icod.CoreUtils-Architecture-and-Migration.md` explaining the final package boundaries, repository split, executable ownership, independent versioning, release/CI ownership, and replacement of transitional Shared dependencies.

**Completion Gate G closure checkpoint (2026-08-27):** G10C reconciled this historical checklist against the completed G3 through G9 work and the G10A/G10B evidence. The final architecture has no cross-repository production project edge, no command-suite runtime package dependency, no neutral-foundation back-edge, and no dependency cycle; every audited repository has current independent CI evidence. Completion Gate G is complete.

Completion of this gate has established the published neutral-foundation layer and completed the repository split. It does not require `Icod.CoreUtils.Shared` to disappear; that repository-local library remains appropriate for behavior genuinely specific to the Coreutils, Fileutils, and Textutils family.

## Why the tools are scheduled this way

- `Icod.CoreUtils` retains GNU Coreutils together with the historical GNU Fileutils and GNU Textutils command families because those packages were merged into GNU Coreutils and now form one natural upstream suite.

- The repository served as a multi-suite incubation workspace while shared ownership was being proven. Completion Gate G dismantled that layout in validated stages; G8 completed the final sibling-suite extraction, and no sibling-suite project remains co-resident in CoreUtils.
__FILLER_1783__
__FILLER_1784__
__FILLER_1785__
__FILLER_1786__
__FILLER_1787__
__FILLER_1788__
__FILLER_1789__
__FILLER_1790__
__FILLER_1791__
__FILLER_1792__
__FILLER_1793__
__FILLER_1794__
__FILLER_1795__
__FILLER_1796__
__FILLER_1797__
__FILLER_1798__
__FILLER_1799__
__FILLER_1800__
__FILLER_1801__
__FILLER_1802__
__FILLER_1803__
__FILLER_1804__
__FILLER_1805__
__FILLER_1806__
__FILLER_1807__
__FILLER_1808__
__FILLER_1809__
__FILLER_1810__
__FILLER_1811__
__FILLER_1812__
__FILLER_1813__
__FILLER_1814__
__FILLER_1815__
__FILLER_1816__
__FILLER_1817__
__FILLER_1818__
__FILLER_1819__
__FILLER_1820__
__FILLER_1821__
__FILLER_1822__
__FILLER_1823__
__FILLER_1824__
__FILLER_1825__
__FILLER_1826__
__FILLER_1827__
__FILLER_1828__
__FILLER_1829__
__FILLER_1830__
__FILLER_1831__
__FILLER_1832__
__FILLER_1833__
__FILLER_1834__
__FILLER_1835__
- `chroot`, the SELinux commands, and `stdbuf` follow the co-resident ProcPs run because they are specialized privilege, security-context, or preload concerns and provide no foundational provider capability required by the ProcPs family. Deferring `top` therefore does not block these Coreutils batches.

- `Icod.Tar` remains the final major suite before Completion Gate G. Archive correctness depends on the mature filesystem foundation and also benefits from the completed process, signal, terminal, provider, and capability work. Tar-specific archive formats and extraction state stay outside the general Shared project.

- Completion Gate G was the final architecture gate. The early `Icod.ProcPs` extraction before Batch 68 was a deliberate exception intended to let `top` target the final ProcPs boundary. All planned suite extractions are complete; G9 and G10 completed CoreUtils cleanup and final architecture validation.

- Except for the explicit pre-Batch-68 `Icod.ProcPs` migration, the final repository split occurs together with the framework/package extraction. This avoids maintaining multiple repositories against unstable shared APIs during the heaviest refactoring period, while still ensuring that every project already has its final suite namespace and a clean ownership boundary.

- Complex parsers, state machines, security boundaries, platform-specialized commands, and shared-library foundations receive focused batches. Commands are grouped only where they share an actual engine or directly validate the same new infrastructure.

## Per-batch workflow

1. Pin the authoritative upstream package and version.
2. Record synopsis, options, operands, environment variables, locale effects, signals, output grammar, exit statuses, and platform-dependent behavior.
3. Produce a conformance matrix marking each item as required, intentionally deferred, platform-limited, or not applicable.
4. Compare the current implementation and tests against that matrix.
5. Design or extend shared infrastructure before adding command-local duplicates, and record whether each new abstraction is provisionally cross-suite, suite-specific, or command-local.
6. Implement BCL behavior first, then focused native interop where required for semantics.
7. Add synchronous and asynchronous unit tests using injected streams.
8. Add CLI integration tests through `ProcessTestHost`.
9. Add differential tests against the pinned upstream utility where licensing and runner availability permit.
10. Add large-input, bounded-memory, cancellation, broken-pipe, standard-stream, multiple-file, invalid-input, and cleanup tests.
11. Add platform capability and native-ABI tests for `windows-latest`, `ubuntu-latest`, and `macos-latest`.
12. Run Debug and Release builds, then the entire applicable solution test suite on all three required runners.
13. Verify UTF-8 encoding and the repository line-ending policy defined by the authoritative `.editorconfig`, lowercase assembly names, required project configuration, and absence of generated artifacts.
14. Update this roadmap’s living status and record any deliberately deferred behavior.
15. For a co-resident suite milestone, verify the final namespace, solution folder, output path, test coverage, suite-specific Shared boundary, transitional project references, and public-format compatibility.
16. For a LineEditor milestone, verify the exact public command classes, preserve Sed and Ed execution-model boundaries, consume the current Shared regex/record/process/filesystem contracts rather than wrapping them, enforce Red and Sed security policies at both parse and capability layers, and preserve the Phase LE9 decision not to create `Icod.LineEditor.Shared` without new consumer evidence.
17. For a ProcPs milestone, verify that common processor, process, signal, priority, waiting, timing, status, host, and terminal mechanics are consumed from the appropriate published neutral packages rather than duplicated in `Icod.ProcPs.Shared`.
18. For Completion Gate G, verify every final repository against its applicable published neutral packages plus repository-local projects before declaring the architecture stable.

## Batch completion checklist

A batch is complete only when:

- every scheduled command has a complete option/operand matrix;
- all required behavior is implemented or explicitly documented as platform-limited;
- no unknown option is silently ignored;
- no unsupported operation throws `NotImplementedException`;
- no production path delegates to the same native utility;
- all command, suite-specific Shared, and applicable framework tests pass;
- large inputs satisfy the stated memory strategy;
- cancellation and broken-pipe behavior are deterministic;
- exit statuses match the upstream contract;
- `windows-latest`, `ubuntu-latest`, and `macos-latest` CI expectations are green;
- the full solution builds in Debug and Release;
- source encoding and line-ending checks pass;
- lowercase assembly names and PascalCase project/namespace conventions are preserved;
- the target framework and project configuration satisfy the current completion gate;
- roadmap status and documentation are updated;
- co-resident suite batches preserve suite-correct namespaces, isolated output paths, tests, and dependency direction; the selected `Icod.UtilLinux` commands remain separate from both Coreutils and ProcPs ownership; Completion Gate G leaves no stale solution, packaging, CI, or inventory references after extraction;
- LineEditor milestones preserve the exact public command classes `Icod.LineEditor.Ed.Command`, `Icod.LineEditor.Red.Command`, and `Icod.LineEditor.Sed.Command`; keep Ed/Red state in `Icod.LineEditor.Ed.Shared`; keep Sed cycle state in `Icod.LineEditor.Sed`; and create `Icod.LineEditor.Shared` only after the evidence-based sharing audit;
- LineEditor tests cover GNU BRE/ERE, byte-preserving LF/NUL and final-record semantics, script-source diagnostics, Sed sandbox denial, Red shell and path denial, in-place/write atomicity, rollback, links, races, cancellation, and cleanup as applicable;
- ProcPs batches consume the published neutral processor/process/timing/host/terminal foundations without duplicating their identities, targets, launch, wait, signal, priority, timing, status, host, or terminal contracts;
- Completion Gate G leaves every neutral foundation free of command-suite dependencies, preserves `Icod.CoreUtils.Shared` only where Coreutils/Fileutils/Textutils-specific reuse remains, and verifies all consumers against published neutral packages.
