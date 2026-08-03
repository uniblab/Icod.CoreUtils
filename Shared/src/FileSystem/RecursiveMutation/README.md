# Recursive mutation and copying

This directory implements Completion Gate E5 as an extension of the existing E1 traversal and E4 single-path mutation contracts.

`RecursiveMutationTraversalEngine` consumes `ReadOnlyPathTraversalEngine` events. It preserves operand provenance, preorder/postorder directory phases, selectors, depth and resource limits, cycle and filesystem-boundary events, and structured traversal failures with their continuation scopes. Every mutable entry is paired with an identity-bearing `FileSystemMutationPrecondition`; no second filesystem walker is introduced.

The layer also provides:

- E2-backed physical preserve-root and destination-inside-source preflight, with an explicit lexical mode for deterministic synthetic providers;
- root-relative destination mapping;
- repeated hard-link identity tracking;
- sparse-file range copying through `IFileSystemOperations`;
- explicit requested-versus-required metadata policy over E3 observations;
- deterministic reverse-order cleanup and an E6-compatible rollback seam.

Recursive command policy remains with each consumer. For example, `chmod` chooses preorder or postorder phases and symlink policy, `rm` selects postorder directory deletion and prompt behavior, and `cp` decides which metadata classes are requested or mandatory.
