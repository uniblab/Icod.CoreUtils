# Icod.Patch package and extraction metadata

## Identity

- Project: `Icod.Patch`
- Command/assembly: `patch`
- Target framework: `net10.0`
- Language policy: C# 13
- Behavioral baseline: GNU patch 2.8
- Public API facade: `Icod.Patch.Command`
- Exit classes: GNU-compatible `0`, `1`, and `2`, plus the repository-wide canceled
  status for cooperative cancellation

## Direct dependencies

| Dependency | Classification for Completion Gate G |
|---|---|
| `Icod.CoreUtils.Shared.CommandLine` | Neutral command-framework candidate; not Patch-specific. |
| `Icod.CoreUtils.Shared.Diagnostics` | Neutral command-framework candidate. |
| `Icod.CoreUtils.Shared.IO` | Neutral byte-stream and text-adapter infrastructure. |
| `Icod.CoreUtils.Shared.FileSystem.Metadata` | E3 neutral filesystem contract. |
| `Icod.CoreUtils.Shared.FileSystem.Mutation` and related ownership/mode providers | E4 neutral filesystem contract. |
| `Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement` | E6 neutral transaction contract. |
| `Icod.Path` | Neutral lexical/physical path, identity, link, reparse, and containment contract. |

## Patch-owned behavior

The following must remain with Patch during any later extraction:

- patch-source scanning and all four syntax parsers;
- immutable patch models and byte-preserving source maps;
- hunk matching, offset, fuzz, whitespace, reversal, prerequisite, and merge policy;
- filename evidence and GNU/POSIX candidate ranking;
- backup, reject, alternate-output, and partial-application policy;
- Patch-to-E6 artifact translation;
- GNU option semantics, diagnostics, quoting, and exit-status aggregation.

## Packaging conditions

- Preserve UTF-8 with LF line endings.
- Preserve XML documentation generation and the Debug/Staging/Release configuration
  policy.
- Keep the dedicated `Icod.Patch.Tests` project and provenance-separated fixture tree.
- Do not introduce a runtime dependency on Diffutils or LineEditor.
- Do not extract the project before Completion Gate G decides the final neutral shared
  package boundaries.
- Publish the documented limitations from
  `upstream/P12-closure-audit.md` with any package release.
