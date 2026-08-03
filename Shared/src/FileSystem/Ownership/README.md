# Shared ownership mutation

This directory supplies the common Batch 42 policy used by `chown` and `chgrp` without creating a dependency between command projects.

`OwnershipIdentityResolver` applies GNU/POSIX name-versus-number rules through `IIdentityProvider`. Names are attempted first, a leading `+` forces numeric interpretation, and an owner followed by an empty group selects that user's primary login group. It accepts owner-only, group-only, `owner:group`, `owner:`, `:group`, and no-op `:` forms. The legacy `owner.group` form remains available only when the complete text is not an existing user name, and emits a compatibility warning.

`OwnershipCommandRunner` combines identity resolution with E3 metadata, E4 identity-bearing ownership mutation, and E5 recursive traversal. `--reference` observations always dereference the reference pathname. `--from` is evaluated against the same E3 observation used to construct an ownership-aware mutation precondition, which narrows—but cannot eliminate—the race window before the E4 provider revalidates stable identity and current UID/GID.

Traversal (`-H`, `-L`, and `-P`) remains separate from terminal mutation dereferencing (`--dereference` and `--no-dereference`). Recursive physical traversal (`-P`, the default) uses no-follow mutation. `-H` and `-L` retain referent mutation unless `-h` is supplied, while explicit recursive `--dereference` requires `-H` or `-L`. Directories are mutated in postorder so ownership changes do not remove traversal access before descendants are processed.

The reporting policy preserves option encounter order for `--changes` and `--verbose`. Quiet mode suppresses ordinary error diagnostics but does not suppress verbose per-entry status reports. Unsupported no-follow mutation of a symbolic link follows GNU behavior by reporting that neither the link nor its referent changed instead of substituting a different target.

On supported Unix hosts, `SystemFileSystemMutationProvider` uses `chown` and `lchown`. On Windows, account lookup remains available for other commands, but POSIX ownership mutation reports a controlled unsupported result. No ACL, SID, or read-only-attribute approximation is substituted for Unix UID/GID ownership.
