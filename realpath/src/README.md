# realpath source

`Command.cs` owns GNU option ordering, logical/physical/no-link policy, relative-output policy, diagnostics, delimiters, and status accumulation. The project-root `Program.cs` is the asynchronous process host. Canonical path construction and filesystem observation remain in `Icod.Path`.
