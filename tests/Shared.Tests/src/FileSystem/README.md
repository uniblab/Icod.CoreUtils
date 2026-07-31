# Shared filesystem tests

The `Traversal` directory validates Completion Gate E1 at two levels.

- `SyntheticReadOnlyFileSystemProvider` supplies deterministic identities, links, filesystem boundaries, provider order, and failures without invoking native APIs.
- Host integration tests exercise file, directory, link, enumeration, and identity behavior on the required Windows, Linux, and macOS runners.

The suite covers pathname wildcard and bracket syntax, zero-or-more-segment `**`, leading-period policy, unmatched expansion policies, provenance and ordering, explicit-intermediate and expanded-root link resolution, iterative preorder/postorder traversal, independent yielding and pruning, active-ancestry cycles, repeated identities reached independently, filesystem boundaries, structured continuation, resource limits, and cancellation.

Tests do not assert command diagnostics or Grep-specific include/exclude presentation. Those remain consumer responsibilities.
