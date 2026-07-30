# tsort source

This directory contains the Batch 24 implementation internals.

- `Command.cs` parses GNU-compatible command-line options, opens the selected binary input, streams token pairs into the graph, maps cancellation and expected failures to command statuses, and owns no injected standard stream.
- `TSortGraph.cs` interns byte tokens, preserves duplicate relations, performs deterministic FIFO topological ordering, identifies GNU-compatible loops, removes one relation per recovery pass, and continues output after loop diagnostics.

Reusable byte tokenization lives in `Icod.CoreUtils.Shared.IO`; graph state and `tsort`-specific ordering policy deliberately remain in the command project.
