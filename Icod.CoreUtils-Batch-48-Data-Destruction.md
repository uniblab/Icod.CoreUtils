# Batch 48 — Data destruction

## Scope

Batch 48 replaces the original minimal `shred` seed with an asynchronous command boundary and a testable overwrite/removal engine. The implementation covers the roadmap requirements for pass selection, random sources, size policy, synchronization, removal, devices, progress, and failure recovery without placing destructive-I/O policy in the general Shared library.

## Command surface

The implementation accepts:

- `-f`, `--force`;
- `-n`, `--iterations=N`;
- `--random-source=FILE`;
- `-s`, `--size=N` with GNU byte-size suffixes;
- `-u`, `--remove[=unlink|wipe|wipesync]`;
- `-v`, `--verbose`;
- `-x`, `--exact`;
- `-z`, `--zero`;
- `--help` and `--version`.

The `-` operand writes to standard output. A seekable output uses its current length; a non-seekable output requires an explicit size. Removal cannot be combined with standard output.

## Overwrite policy

The default is three cryptographically random passes. Larger pass counts use randomized fixed-pattern passes bracketed by random passes, while a requested final-zero pass is always last and is not included in the iteration count. A file-backed random source is finite: premature end of file is an error and never falls back silently to the operating-system random provider.

Regular-file lengths are rounded to a complete 4096-byte block unless exact mode is selected. Device-style paths and non-seekable streams are not regular-file rounded. Every named-file pass receives both an asynchronous flush and a durable `FileStream.Flush(true)` before the next pass or removal begins.

## Removal and recovery

Removal is a postcondition of successful overwriting, not part of a `finally` block. Cancellation, random-source exhaustion, or write/flush failure leaves the target in place.

- `unlink` deletes the current pathname directly;
- `wipe` changes the pathname to random progressively shorter names before deletion;
- `wipesync` uses the same rename sequence and attempts to synchronize the containing directory after each metadata change.

When a rename fails, the diagnostic includes the pathname at which the overwritten data remains. Failures are isolated per operand so later operands are still attempted.

## Platform boundary

Named regular files use the same managed stream mechanics on Windows, Linux, and macOS. Unix `/dev/...` and Windows raw-device prefixes are recognized as device-style paths, avoiding regular-file block rounding. Device access remains subject to host permissions, and an explicit size is required for every device-style path so an unavailable or misleading stream length cannot produce a no-op overwrite.

Directory flush is exposed directly by the BCL on some POSIX hosts. It is best effort on hosts where opening or flushing a directory handle is unavailable, including ordinary Windows configurations. File-content flush remains mandatory and failure-producing.

## Security limitation

The command overwrites logical addresses exposed by the selected file or device. It cannot guarantee erasure of copies retained outside those addresses. In particular, the implementation cannot defeat SSD and flash remapping or wear leveling, copy-on-write extents, reflinks, filesystem journals, controller and RAID caches, snapshots, backups, deduplication stores, remote replicas, virtual-disk layers, or remapped bad sectors.

These are storage-architecture limits rather than missing overwrite passes. Secure erase, cryptographic erase, key destruction, snapshot deletion, backup lifecycle controls, or physical media destruction may be required depending on the storage system and threat model.

## Tests

`Icod.CoreUtils.Shred.Tests` covers:

- default, short, and long option parsing;
- byte-size suffixes and invalid removal modes;
- exact external-random overwriting;
- finite-source exhaustion without fallback;
- final-zero and explicit-size behavior;
- regular-file block rounding;
- unlink and wipe-sync removal;
- continuation after a failed operand;
- seekable and explicitly sized non-seekable standard-output operation;
- verbose progress and removal reports.

Final acceptance remains the full Debug and Release solution build and applicable test suite on `windows-latest`, `ubuntu-latest`, and `macos-latest`.
