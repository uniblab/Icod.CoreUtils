# Copy/move shared engine

This directory contains the Coreutils-specific Batch 44 copy/move engine used by `cp` and `mv`.

The engine consumes framework recursive traversal for containment checks, stable source identity, hard-link provenance, metadata planning, and sparse-file policy. Ordinary-file replacement and retained backups are delegated to the framework transactional-replacement boundary. GNU `--reflink=never|auto|always` selection remains Coreutils policy, while the actual copy-on-write clone attempt is delegated to `IFileSystemOperations.CloneFileAsync`. Directory metadata is applied through the same framework metadata-preservation path used by regular-file replacement. Command-specific option parsing, prompts, wording, and exit status remain in the individual tools.

A same-filesystem `mv` first attempts a direct rename. When the host reports a cross-device or otherwise unavailable rename and fallback is permitted, the engine performs a complete E5/E6 copy and removes the source only after the destination succeeds.

Completion Gate G retains this engine in `Icod.CoreUtils.Shared`: GNU copy/move policy is shared by Coreutils file-manipulation commands, while neutral host cloning, traversal, metadata, mutation, sparse-file, and transactional-replacement mechanisms are consumed from `Icod.CommandFramework`.
