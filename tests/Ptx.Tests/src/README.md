# Test source layout

- `CommandTests.cs` exercises command parsing, indexing, ordering, formatting, references, parameter files, cancellation, and ownership.
- `NativeAppHostTests.cs` protects the lowercase `ptx` assembly identity.

The tests invoke the managed command directly so failures can distinguish parser, engine, formatter, and I/O behavior without delegating production work to a native utility.
