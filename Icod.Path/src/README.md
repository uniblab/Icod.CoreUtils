# Canonical-path model

The `Icod.Path` namespace separates pathname grammar from filesystem observation.

- `PathPlatformSemantics` describes POSIX and Windows separators, root syntax, volume identity, and comparison rules independently of the host operating system.
- `PathLexicalNormalizer` creates absolute lexical paths without observing the filesystem and rejects invalid or unresolved drive-relative forms.
- `ICanonicalPathFileSystemProvider` supplies one no-follow observation per pathname component.
- `SystemCanonicalPathFileSystemProvider` maps BCL filesystem observations into structured entry, symbolic-link, reparse-point, and failure results.
- `CanonicalPathResolver` performs ordered physical resolution, link-loop and expansion-limit checks, missing-component policy, raw final-link inspection, relative path computation, and containment evaluation.
- `CanonicalPathResult`, `RelativePathResult`, and `PathContainmentResult` carry structured failures; no failure path is returned as a successful canonical result.

## Missing components

`RequireExisting` requires every component. `AllowFinalComponent` permits only the final unresolved component. `AllowMissingSuffix` permits the first missing component and the remaining lexical suffix. The result records the number of unresolved suffix components.

## Link and reparse behavior

The provider observes each component without following it. The resolver follows symbolic links itself, preserving relative-target semantics and checking both repeated resolution states and a caller-configurable expansion limit. The final link may be inspected without following it. A non-link reparse point is a controlled unsupported result when final-object rejection is requested; command policy remains outside this library.

## Platform profile

POSIX paths use `/`, a single root, and ordinal case-sensitive comparison. Windows paths recognize drive roots, UNC roots, current-volume rooted paths, and extended path prefixes, and use ordinal case-insensitive root and component comparison. Drive-relative input is resolved only when its drive matches the supplied base path; otherwise it is rejected rather than guessed.
