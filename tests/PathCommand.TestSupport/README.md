# Path command test support

This directory contains the deterministic no-follow filesystem provider linked into the `readlink` and `realpath` test projects. It lets both commands exercise POSIX path behavior identically on every CI runner without exposing test-only APIs from `Icod.Path`.
