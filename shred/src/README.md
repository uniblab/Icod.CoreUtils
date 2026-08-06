# `shred`

`Icod.CoreUtils.Shred` implements the GNU Coreutils overwrite command for `net10.0` on Windows, Linux, and macOS.

## Design

The command is split into four responsibilities:

- `ShredOptions` parses the GNU option surface and byte-size suffixes;
- `ShredPassPlanner` chooses random and fixed-pattern passes;
- `IShredRandomSource` supplies cryptographic or finite file-backed random bytes;
- `ShredEngine` performs overwrite, synchronization, progress, recovery, and removal policy.

Each overwrite pass starts at byte zero, writes the selected size, and flushes a named file to durable storage before another pass begins. Removal is attempted only after every pass succeeds. `--remove=unlink` deletes the current name directly. `wipe` and `wipesync` first replace the name with random progressively shorter names; diagnostics report the current recovery name if a rename fails. Directory synchronization is attempted for `wipesync` where the host BCL exposes it and is best effort elsewhere.

Regular files are rounded to a 4096-byte boundary unless `--exact` is selected. Explicit `--size` is required for device-style paths and streams whose length cannot be determined. Seekable standard output uses its current length. Device paths are not treated as ordinary regular files and therefore are not rounded merely because the stream is seekable.

## Erasure limits

`shred` can overwrite only the logical blocks exposed by the operating system for the opened pathname or device. It cannot prove destruction of earlier copies retained by:

- SSD, flash, or storage-controller remapping and wear leveling;
- copy-on-write filesystems and reflinked extents;
- filesystem journals, RAID/controller caches, and remapped sectors;
- snapshots, backups, replicas, or remote storage;
- compressed, deduplicated, encrypted, or virtualized storage layers.

For media with secure-erase or cryptographic-erase support, use the storage vendor or operating-system facility appropriate to that device and threat model.
