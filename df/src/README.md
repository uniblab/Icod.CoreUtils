# df command

`Command` owns diagnostics and tabular output. `DfOptionParser` implements GNU-style filesystem filtering, block-size and human formats, inode mode, filesystem types, totals, selected output fields, and path operands. Filesystem observations come from `Icod.CoreUtils.Shared.FileSystem.Usage`. POSIX `--sync` is performed before observation; Windows reports that mode as unsupported.
