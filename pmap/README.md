# Icod.ProcPs.Pmap

`Icod.ProcPs.Pmap` implements the procps-ng 4.0.6 `pmap` profile over the
reuse-aware memory-map provider in `Icod.ProcPs.Shared`.

Linux is the authoritative compatibility platform and uses `/proc/PID/maps`
and `/proc/PID/smaps`. The command supports the basic, extended (`-x`),
dynamic extended (`-X`/`-XX`), device, quiet, path/kernel-name, hexadecimal
range, totals, permission, offset, device, mapping-name, UTF-8, vanished
process, and access-diagnostic behavior required by Batch 61.

Windows and macOS currently receive a controlled unsupported diagnostic for
Linux-equivalent process maps. The shared provider deliberately does not
substitute loaded-module lists or other partial observations for complete
address-space mappings.
