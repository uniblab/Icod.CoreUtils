# Copy/move shared engine

This directory contains the Coreutils-specific Batch 44 copy/move engine used by `cp` and `mv`.

The engine consumes Completion Gate E5 for recursive traversal, containment checks, stable source identity, hard-link provenance, metadata planning, and sparse-file policy. Ordinary-file replacement and retained backups are delegated to Completion Gate E6. Command-specific option parsing, prompts, wording, and exit status remain in the individual tools.

A same-filesystem `mv` first attempts a direct rename. When the host reports a cross-device or otherwise unavailable rename and fallback is permitted, the engine performs a complete E5/E6 copy and removes the source only after the destination succeeds.

The provisional Completion Gate G classification is `Icod.CoreUtils.Shared`: the policy is shared by Coreutils file-manipulation commands, but no cross-suite consumer has yet demonstrated that it belongs in `Icod.CommandFramework`.
