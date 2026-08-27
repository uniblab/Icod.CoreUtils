# Icod.CoreUtils G10B Cross-Repository Dependency and Isolation Audit

**Status:** Complete
**Audit date:** 2026-08-27
**CoreUtils working branch checkpoint:** `Gate_G10` commit `151cc91bf27bfe339bc9f1e5c933b9a527ec527c`

## Scope

G10B verifies the final production dependency graph established by Completion Gate G. It distinguishes repository-local `ProjectReference` edges from cross-repository `PackageReference` edges and verifies that independent repository CI succeeds without neighboring source-tree checkouts.

Test-only project references, sample projects, package-smoke projects, and ordinary third-party packages do not create production architecture edges and are excluded from the graph below.

## Result

The audit found no architecture defect requiring a code or project-file change.

- No production `ProjectReference` crosses a repository boundary.
- No command suite has a runtime package dependency on another command suite.
- Neutral foundations have no production dependency back into a command suite.
- All cross-repository production reuse is expressed through published neutral packages.
- The resulting repository/package graph is acyclic.
- Every final repository has current successful `main` CI/build-validation evidence at the audited head.
- No G10B version-pin migration is required. Consumers remain free to pin different compatible neutral-package versions under the independent-versioning policy.

## Neutral-foundation graph

| Repository | Audited `main` head | Production Icod dependency edges |
|---|---|---|
| `Icod.Path` | `587ad155ce8036bac744170ddf8c06ea950f761e` | none |
| `Icod.Timing` | `9364b93d3bbca2e19a3be5fae850e2b77bcb39f1` | none |
| `Icod.Host` | `ec54a43b7fb6267f93aee8decf93fa85d241b4aa` | none |
| `Icod.TermInfo` | `e24e5da8b67acb95b42732a4252de0251f012045` | runtime root: none; `Source -> TermInfo` and `Compiler -> TermInfo + Source` are repository-local project edges |
| `Icod.CommandFramework` | `c5591bf29fb6ce84b18ede42b0f49733a55001e2` | `Icod.Path 1.0.0` |
| `Icod.Processes` | `d9a0828262d4fb88856807e9c75851d73ad43532` | `Icod.Timing 1.0.0` |
| `Icod.Terminal` | `888f9f6e9a5f043af4ed993583f11a9f13cd697d` | `Icod.TermInfo 1.0.0`; `Icod.Timing 1.0.0` |
| `Icod.DCurses` | `73fda2ac75f0adc4b5673c3d1b6cd109ba11efdb` | `Icod.Terminal 0.1.0-alpha.11`; `Icod.TermInfo 1.0.0` |

This subgraph is acyclic:

```text
Icod.Path
    ↑
Icod.CommandFramework

Icod.Timing
    ↑
Icod.Processes

Icod.TermInfo      Icod.Timing
      ↑                ↑
      └── Icod.Terminal ┘
              ↑
      Icod.DCurses
          └── Icod.TermInfo
```

`Icod.Path`, `Icod.Timing`, `Icod.Host`, and the `Icod.TermInfo` runtime root are leaves with respect to production Icod dependencies.

## Command-suite graph

| Repository | Audited `main` head | Cross-repository production package edges | Repository-local production project edges |
|---|---|---|---|
| `Icod.CoreUtils` | `26d3f4bd9f587eb7d7c32bff03f37fd69138779d` | Shared -> `Icod.CommandFramework 1.1.0`, `Icod.Path 1.0.0`; Timeout -> `Icod.Timing 1.0.0` | CoreUtils commands -> `Icod.CoreUtils.Shared` where required |
| `Icod.UtilLinux` | `c0841f6f7007e5c2bd0415f5153e56e6582f1520` | `Icod.CommandFramework 1.1.0` | none |
| `Icod.Grep` | `7005d0fe5c20f6c392be7ef395be749e3978d5d7` | `Icod.CommandFramework 1.1.0` | none |
| `Icod.Tar` | `9d12900813eb9ed759a3cb6ded44ed7b58001124` | `Icod.CommandFramework 1.1.0` | none |
| `Icod.DiffUtils` | `38ee77a1796aa686a49fb9b5d498b4541cb08a2b` | `Icod.CommandFramework 1.1.0` | `cmp`, `diff`, `diff3`, `sdiff`, and `diffutil` -> `Icod.DiffUtils.Shared` |
| `Icod.LineEditor` | `8f07999631634767385d6eddf4efff1171fbbc6c` | `Icod.CommandFramework 1.1.0` | `ed`, `red` -> `Icod.LineEditor.Ed.Shared`; `sed` has no production project edge |
| `Icod.Patch` | `995a99bbff68dc00dcb9f1e0a80b677542fa9d40` | `Icod.CommandFramework 1.1.0`; `Icod.Path 1.0.0` | none |
| `Icod.ProcPs` | `cb869c6db2e88bfd2b83c0e5e80bb28b87aa603c` | Shared -> `Icod.Processes 1.0.0`, `Icod.Timing 1.0.0`; pgrep/pkill/pidwait -> `Icod.CommandFramework 2.0.0`; terminal tools -> `Icod.DCurses 0.1.0-Alpha-14` plus neutral process/timing packages as required; top -> `Icod.Host 1.0.0` | command projects -> `Icod.ProcPs.Shared` where required; `procps` router -> same-repository command projects |

The command-suite graph contains no package edge from one command suite to another. DiffUtils/Patch/LineEditor interoperability remains textual rather than a runtime assembly dependency.

## ProcPs post-extraction refinement

ProcPs demonstrates why package versions are intentionally not lockstep.

Its current repository has migrated general process, timing, host, and terminal mechanism to the narrower neutral packages created after the original Gate G extraction plan. In particular:

```text
Icod.ProcPs.Shared
    -> Icod.Processes 1.0.0
    -> Icod.Timing 1.0.0

Icod.ProcPs.Top
    -> Icod.DCurses 0.1.0-Alpha-14
    -> Icod.Host 1.0.0
    -> Icod.Processes 1.0.0
    -> Icod.Timing 1.0.0
    -> repository-local Icod.ProcPs.Shared

Icod.ProcPs.Watch
    -> Icod.DCurses 0.1.0-Alpha-14
    -> Icod.Processes 1.0.0
    -> Icod.Timing 1.0.0
    -> repository-local Icod.ProcPs.Shared
```

The pgrep/pkill/pidwait family has deliberately moved to `Icod.CommandFramework 2.0.0`, while CoreUtils remains validated against 1.1.0. This is not dependency drift: it is the expected result of independent consumer versioning.

## Forbidden-edge sweep

The audit searched the final repository set for production package references to command-suite identities:

- `Icod.CoreUtils`
- `Icod.UtilLinux`
- `Icod.Grep`
- `Icod.Tar`
- `Icod.ProcPs`
- `Icod.DiffUtils`
- `Icod.LineEditor`
- `Icod.Patch`

No command-suite runtime package edge was found. The only `Icod.CoreUtils` package-reference search hit was historical/documentation text rather than a project file.

Production `ProjectReference` searches likewise found only paths within the owning repository. Test, sample, and package-smoke references were classified separately and do not affect production dependency direction.

## Current independent CI evidence

| Repository | Audited `main` head | Successful current-main workflow/run |
|---|---|---:|
| `Icod.CoreUtils` | `26d3f4bd9f587eb7d7c32bff03f37fd69138779d` | `33046053954` |
| `Icod.UtilLinux` | `c0841f6f7007e5c2bd0415f5153e56e6582f1520` | `32536085110` |
| `Icod.Grep` | `7005d0fe5c20f6c392be7ef395be749e3978d5d7` | `32814681657` |
| `Icod.Tar` | `9d12900813eb9ed759a3cb6ded44ed7b58001124` | `32891206784` |
| `Icod.ProcPs` | `cb869c6db2e88bfd2b83c0e5e80bb28b87aa603c` | `33035940773` |
| `Icod.DiffUtils` | `38ee77a1796aa686a49fb9b5d498b4541cb08a2b` | `32803374304` |
| `Icod.LineEditor` | `8f07999631634767385d6eddf4efff1171fbbc6c` | `32605768511` |
| `Icod.Patch` | `995a99bbff68dc00dcb9f1e0a80b677542fa9d40` | `32633191397` |
| `Icod.CommandFramework` | `c5591bf29fb6ce84b18ede42b0f49733a55001e2` | `33021922381` |
| `Icod.Path` | `587ad155ce8036bac744170ddf8c06ea950f761e` | `32809654546` |
| `Icod.Timing` | `9364b93d3bbca2e19a3be5fae850e2b77bcb39f1` | `32898425530` |
| `Icod.Host` | `ec54a43b7fb6267f93aee8decf93fa85d241b4aa` | `32919231408` |
| `Icod.Processes` | `d9a0828262d4fb88856807e9c75851d73ad43532` | `32912102860` |
| `Icod.TermInfo` | `e24e5da8b67acb95b42732a4252de0251f012045` | `33051233632` |
| `Icod.Terminal` | `888f9f6e9a5f043af4ed993583f11a9f13cd697d` | `33044822296` |
| `Icod.DCurses` | `73fda2ac75f0adc4b5673c3d1b6cd109ba11efdb` | `32908777275` |

These are repository-owned GitHub Actions runs from clean hosted checkouts. Combined with the absence of cross-repository `ProjectReference` edges, they provide current isolation evidence: no neighboring source checkout is required for the audited production graph.

## Version-pin disposition

No version change is required to close G10B.

The audit intentionally does not normalize versions merely because newer packages exist. CoreUtils remains on its validated `Icod.CommandFramework 1.1.0` boundary; ProcPs consumers that require the 2.0.0 contract remain on that line; DCurses and Terminal retain their current compatible pins. Future upgrades are ordinary changes in the consuming repository and must be validated there.

## G10B exit criterion

G10B is complete.

The final architecture satisfies the required dependency direction:

```text
neutral foundations
        ↓ published PackageReference
suite-local Shared / engine, where required
        ↓ repository-local ProjectReference
command / router
```

No cross-repository project edge, command-suite runtime package edge, neutral-to-suite back-edge, or dependency cycle was found. G10C may now reconcile the final roadmaps and close Completion Gate G.