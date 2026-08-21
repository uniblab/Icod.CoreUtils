# Filesystem usage reporting

This directory contains the reusable Batch 47 contracts used by `df` and `du`.

- `UsageSizePolicy` implements GNU block-size environment precedence, integral output units, and human/SI presentation.
- `SystemFileSystemUsageProvider` combines framework filesystem observations with mounted-drive discovery and forwards the framework `FileSystemInformation` inode-pool observations unchanged. Host ABI ownership, including POSIX `statvfs` and explicit Windows unsupported results, remains in `Icod.CommandFramework`.
- `DiskUsageCalculator` consumes the Completion Gate E1 traversal engine and E3 allocated-byte metadata. It provides postorder totals, hard-link deduplication, apparent-size mode, inode mode, symbolic-link policies, filesystem boundaries, exclusions, and controlled diagnostics.

Command projects retain option parsing, diagnostics, output-column selection, NUL input, and command-specific defaults.
