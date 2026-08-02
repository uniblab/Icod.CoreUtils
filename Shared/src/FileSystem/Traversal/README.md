# Shared.FileSystem.Traversal

`Icod.CoreUtils.Shared.FileSystem.Traversal` is the Completion Gate E1 read-only pathname foundation. It separates three responsibilities that command projects must not reimplement independently:

1. segment-aware pathname pattern parsing and operand expansion;
2. injectable one-level filesystem observation;
3. iterative, policy-driven recursive traversal.

The namespace is caller-independent incubation infrastructure and a provisional `Icod.CommandFramework` candidate. It contains no Grep, Diffutils, Tar, listing, accounting, diagnostic-formatting, or output policy.

## Pathname patterns and expansion

`PathnamePattern` and `PathnamePatternMatcher` implement segment-aware matching:

- `*` matches zero or more characters inside one segment;
- `?` matches one character inside one segment;
- bracket expressions support literals, ranges, and `!`/`^` negation;
- a complete `**` segment matches zero or more complete pathname segments;
- ordinary wildcards never consume a pathname separator;
- wildcard matching of a leading period is independently configurable;
- comparison is ordinal and may be host-default, explicitly sensitive, or explicitly insensitive;
- backslash quoting is enabled by default on Unix-like hosts and disabled by default on Windows, where backslash is a pathname separator.

`PathnameExpander` accepts only operands that a command has already classified as eligible pathnames. It preserves original operand text, operand order, repeated operands, result ordinals, operational paths, and display paths. Commands choose whether an unmatched pattern is preserved literally, produces no roots, or produces a structured error. Match order may preserve provider order or use deterministic ordinal ordering.

Current-directory segments are evaluated lexically without changing the operational directory and remain present in the display path; parent-directory segments navigate through the provider while rewinding the active expansion ancestry. This is pathname evaluation, not canonicalization: E1 preserves the resulting operational spelling and does not resolve a complete link chain or construct a canonical path. Those policies remain in Completion Gate E2.

`SymbolicLinkTraversalMode.RootsOnly` has a precise expansion meaning: explicitly named intermediate directory segments may be dereferenced, while wildcard-discovered intermediate directories remain physical. A terminal match is an expanded root, so a trailing separator may dereference that terminal link under `RootsOnly` to determine whether the produced root is directory-like. `**` never changes link-following policy by itself.

## Provider boundary

`IReadOnlyFileSystemProvider` exposes only:

- observation of one pathname under an explicit terminal `PathDereferenceMode`; and
- enumeration of one directory level.

The historical Boolean overload remains source-compatible, but new consumers use `NoFollow` or `FollowEligiblePathIndirection`.

`SystemReadOnlyFileSystemProvider` supplies the host implementation. Stable entry and filesystem identities are obtained through:

- Windows file identifiers and volume serial numbers;
- Linux `statx` device/inode and mount identifiers;
- macOS `stat`/`lstat` device and inode values.

An unavailable identity is represented explicitly. Recursive traversal and recursive `**` expansion require stable directory identities for active-ancestry cycle safety, and root-filesystem confinement additionally requires filesystem identities. Finite nonrecursive pathname expansion remains bounded by its segment count, can proceed without entry identities, and does not reject a finite path merely because it revisits an ancestor through an explicitly followed link.

The provider does not recurse, filter, format diagnostics, suppress exceptions, or decide command exit status. Tests can inject a deterministic provider without touching the host filesystem.

## Traversal

`ReadOnlyPathTraversalEngine` consumes provenance-preserving `PathTraversalRoot` objects and yields `PathTraversalEvent` values. Events distinguish:

- root start;
- directory preorder entry;
- nondirectory entries;
- directory postorder exit;
- structured errors;
- active-ancestry cycles; and
- filesystem boundaries.

Traversal is iterative rather than recursive, so managed call-stack depth does not grow with pathname depth. Each active directory frame retains at most one configured bounded child set, permitting deterministic ordering without materializing the complete tree.

Cycle detection uses the identities in the active directory ancestry. It is deliberately not a global visited-object set: repeated explicit roots, hard-linked nondirectories, and independently reached directory identities remain observable. A followed directory that identifies an active ancestor produces a cycle event and is not descended into again.

`IPathTraversalSelector` returns independent yield and descend decisions. `PathTraversalRuleSelector` supplies ordered last-matching-rule behavior over basename, root-relative path, whole operational path, or matching-name suffixes. Directory pruning occurs before enumeration.

## Link and boundary policy

`SymbolicLinkTraversalMode` distinguishes:

- `Never` — links remain entries and are not descended into;
- `RootsOnly` — a link supplied as a root may be followed, but descendant links are not;
- `Always` — eligible root and descendant links may be followed.

The policy applies only to eligible pathname indirection. Through the neutral `Icod.Path` inspector, Windows symbolic links, directory junctions, and mounted volumes may be followed when policy permits. Unknown name surrogates are not followed. Recognized non-name-surrogate points, including Cloud Files placeholders and opaque filter-managed objects, retain their underlying file or directory kind and are not treated as links. Reparse points whose tag cannot be characterized are quarantined and are not descended into merely because they carry directory-like attributes.

No-follow entries preserve `PathIndirectionInfo`, including the Windows tag, junction-versus-mounted-volume classification, provider-normalized and raw targets, name-surrogate status, and recall/offline attributes. `FileSystemEntryKind.SymbolicLink` is now strict; junctions and other name surrogates use `NameSurrogate`. Recognized non-name-surrogate reparse points retain their underlying file or directory kind, while only uncharacterized reparse points use `ReparsePoint`.

`FileSystemBoundaryMode.StayOnRootFileSystem` compares each directory's filesystem identity with the root identity before descent. A different identity produces a boundary event. An unavailable required identity produces a structured error.

## Error, cancellation, and ownership policy

Shared returns structured errors and continuation scopes. It does not write diagnostics. Consumers decide quoting, suppression, quiet behavior, continuation, and exit status.

Expansion and traversal expose cancellation-aware `IAsyncEnumerable<T>` APIs. Cancellation is checked before and between observations, enumerated entries, policy decisions, descents, and yielded events. The implementation does not use `Task.Run` to disguise synchronous filesystem enumeration. A host enumeration call already blocked inside the operating system may not become cancellable until it returns.

The traversal layer owns no command streams and opens no persistent caller-visible handles. The system provider's native observation handles are scoped to one observation.

## Gate boundaries

E1 intentionally supplies only the minimum metadata needed for safe traversal: effective kind, pathname-indirection/reparse characterization, stable entry identity, and filesystem identity.

- Completion Gate E2 owns lexical and physical canonicalization, missing-component policies, and complete link resolution.
- Completion Gate E3 supplies authoritative metadata, explicit availability, allocated blocks, filesystem information, and timestamp mutation through the sibling [`Metadata`](../Metadata/README.md) namespace while reusing these E1 identities. Completion Gate E3R makes E1, E2, and E3 consume the same neutral reparse-point characterization.
- Completion Gate E5 extends E1 traversal and identity policy for race-resistant mutation, preserve-root behavior, copying, moving, deletion, and cleanup.

Commands must not use E1 as an implicit `realpath` implementation or as a substitute for the later metadata and mutation contracts.
