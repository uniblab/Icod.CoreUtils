# readlink source

`Command.cs` owns option policy, diagnostics, output delimiters, and exit-status accumulation. The project-root `Program.cs` is the asynchronous process host. Canonical path construction, link inspection, loop detection, roots, volumes, and missing-component behavior remain in `Icod.Path`.
