# ProcPs shared tests

These tests are fixture-driven where procfs syntax is involved so Linux kernel
text formats can be checked identically on Windows, Linux, and macOS. System
provider tests use only the current process and capability assertions. Sampling
and selection tests are deterministic and do not depend on wall-clock time.
