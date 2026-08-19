# Icod.ProcPs.PidWait

`pidwait` applies the same procps-ng 4.0.6 selection grammar as `pgrep`, then waits through the reuse-aware arbitrary-process wait contract. Procps-ng 4.0.6 does not install a `pwait` alias.

The executable targets `net10.0`, uses C# 13, and remains in the co-resident
`Icod.CoreUtils.sln` workspace until the final suite extraction. Linux procfs is
the authoritative procps-ng profile; Windows and macOS use only observations
whose semantics are defensible on those hosts, with unavailable criteria
producing no fabricated matches.
