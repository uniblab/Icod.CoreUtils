# Icod.CoreUtils Architecture and Migration Record

**Status:** Completion Gate G — COMPLETE
**Architecture checkpoint:** 2026-08-27
**CoreUtils baseline:** `main` commit `26d3f4bd9f587eb7d7c32bff03f37fd69138779d`

## Purpose

This document records the repository, package, engine, executable, versioning, release, and interoperability boundaries that remain after Completion Gate G split the former multi-suite `Icod.CoreUtils` incubation repository.

G10A records the final architecture. G10B supplies the repository-by-repository dependency and independent-build evidence in `Icod.CoreUtils-G10B-Dependency-Audit.md`. G10C has reconciled both roadmaps against that evidence and closed Completion Gate G.

## Architectural rules

1. A command suite owns its command semantics, presentation, upstream compatibility profile, and suite-specific engines.
2. Cross-repository implementation reuse flows through a published neutral package, never through another command suite's source tree.
3. A suite-specific Shared/engine project remains a repository-local `ProjectReference` dependency unless its owning repository independently establishes a package boundary.
4. `Icod.CoreUtils.Shared` is permanently repository-local and non-packable.
5. Public command, text, file, and archive formats are preferred over runtime suite-to-suite dependencies for interoperability.
6. A neutral foundation must not depend on a command suite.
7. Repository and package versions are independent; the Icod ecosystem is not lockstep-versioned.
8. A cross-repository `ProjectReference` or dependency on a neighboring checkout fails G10B.

## Repository ownership
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
__FILLER_60__
__FILLER_61__
__FILLER_62__
__FILLER_63__
__FILLER_64__
__FILLER_65__
__FILLER_66__
__FILLER_67__
__FILLER_68__
__FILLER_69__
__FILLER_70__
__FILLER_71__
__FILLER_72__
__FILLER_73__
__FILLER_74__
__FILLER_75__
__FILLER_76__
__FILLER_77__
__FILLER_78__
__FILLER_79__
__FILLER_80__
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
## Gate G closure sequence

### G10A — Architecture record

Complete with this document. Ownership, dependency direction, versioning, release policy, and textual interoperability are written down against the post-G9 CoreUtils state.

### G10B — Cross-repository dependency and isolation verification

Complete. `Icod.CoreUtils-G10B-Dependency-Audit.md` records the audited production package/project edges, repository-local engine boundaries, successful current `main` CI evidence, acyclic graph proof, and the conclusion that no G10-required version-pin migration exists.

### G10C — Final closure

Complete. Both Gate G roadmaps have been reconciled against the G3 through G10 evidence, stale pre-extraction dependency assumptions have been corrected to the published-neutral-owner rule, and Completion Gate G is formally closed.

**Completion Gate G is complete.**
