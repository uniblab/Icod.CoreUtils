# `ptx`

This project implements GNU Coreutils `ptx` for .NET 10. It generates a sorted permuted index from one or more byte-oriented input sources.

The authoritative compatibility baseline for Batch 25 is GNU Coreutils 9.11. The command supports GNU and traditional invocation forms, automatic and input references, break/ignore/only parameter files, case folding, configurable sentence and word regular expressions, width/gap/truncation controls, and dumb, roff, and TeX output.

Input and output use `CommandContext` and cancellation-aware TAP APIs. Contexts are written once to a secure Shared temporary workspace. Lightweight occurrence records are sorted stably with `Icod.CoreUtils.Shared.Ordering.ExternalOrderingEngine<T>`, permitting run spilling and bounded merge fan-in without depending on another command project.

## Source layout

- `Program.cs` provides the asynchronous process entry point.
- `src/Command.cs` parses GNU-style options and owns command-level diagnostics and streams.
- `src/PtxEngine.cs` reads parameter files and input contexts and drives external ordering.
- `src/PtxContextReader.cs` implements default sentence, line, whole-input, and custom-regexp context selection.
- `src/PtxPatterns.cs` adapts byte data to the Shared GNU Emacs regular-expression engine and implements bytewise word ordering.
- `src/PtxStorage.cs` owns the context spool and the occurrence run codec.
- `src/PtxFormatter.cs` plans and writes dumb, roff, and TeX fields.
- `src/PtxModel.cs` contains command-local settings and records.

No command project references this project, and this project references no other command project.

## Memory boundary

Default GNU sentence recognition and traditional line recognition stream their source contexts. Custom sentence regular expressions require whole-source matching because the Shared managed GNU engine operates on a complete searchable string; occurrence ordering and context storage remain externally spooled and bounded.
