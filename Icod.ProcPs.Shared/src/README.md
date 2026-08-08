# ProcPs shared source

The source is divided by responsibility:

- `Observation.cs` defines capability, provenance, availability, and observed-value contracts.
- `ProcessModels.cs` defines reusable process, terminal, namespace, container, and map models.
- `LinuxProcParsers.cs` contains fixture-testable parsers for Linux procfs text and binary NUL lists.
- `ProcessProviders.cs` provides Linux procfs and capability-driven portable process observation.
- `Selection.cs` implements ProcPs process selection and adapters over the shared process-control contracts.
- `SystemMetrics.cs` models and observes memory, swap, CPU, load, uptime, VM, slab, hugepage, and Linux login-session data.
- `Sampling.cs` provides counter-delta, wraparound, sampling-window, and refresh helpers over shared monotonic time.
- `Vmstat.cs` defines vmstat-specific capabilities, cumulative system/paging counters, Linux diskstats rows, and Linux/Windows/macOS provider adapters.
- `Presentation.cs` supplies field catalogs, sorting, personalities, display configuration, and reusable screen models.

Command-specific option parsing and output syntax remain in the individual ProcPs command projects.
