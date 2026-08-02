# Icod.Path

`Icod.Path` is the neutral canonical-path foundation shared by the Icod utility suites. Completion Gate E2 introduces a single model for lexical normalization, physical path resolution, symbolic-link and reparse-point inspection, missing-component policy, loop detection, relative path computation, containment checks, and platform-aware root and volume semantics.

The project is intentionally independent of individual commands and of `Icod.CoreUtils.Shared`. GNU `readlink` and `realpath` will consume this model in Batch 35, and `Icod.Patch` filename selection will consume it in Patch P7 after Batch 35 establishes the command-level conformance profile.

The public resolver returns structured success or failure results. It never reports unresolved input as a successful canonical path. Physical resolution processes pathname components in filesystem order so symbolic links are resolved before a following `..`, matching filesystem traversal semantics rather than merely applying lexical simplification.

See [`src/README.md`](src/README.md) for the contract and platform profile.
