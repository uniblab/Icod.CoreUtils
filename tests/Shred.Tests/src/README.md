# `Icod.CoreUtils.Shred.Tests`

These command-boundary tests exercise the destructive-I/O policies without touching raw devices. Temporary regular files and in-memory standard output cover:

- GNU option parsing and byte-size suffixes;
- cryptographic and finite external random sources;
- exact and block-rounded writes;
- zero passes, progress, and per-target failure isolation;
- force handling for read-only regular files;
- seekable standard output and non-seekable size requirements;
- unlink, wipe, and wipe-and-synchronize removal behavior;
- preservation of the current pathname when an overwrite pass fails.

Platform-specific device integration and storage-controller guarantees remain outside the deterministic unit-test boundary and require the repository's Windows, Linux, and macOS validation matrix.
