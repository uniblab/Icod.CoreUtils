# Icod.ProcPs.Pgrep

`pgrep` selects process IDs using the shared procps-ng 4.0.6 process-matching grammar. It delegates observation to `Icod.ProcPs.Shared` and does not signal or wait for targets.

The executable targets `net10.0`, uses C# 13, and remains in the co-resident
`Icod.CoreUtils.sln` workspace until the final suite extraction. Linux procfs is
the authoritative procps-ng profile; Windows and macOS use only observations
whose semantics are defensible on those hosts, with unavailable criteria
producing no fabricated matches.
