# TSort tests

`CommandTests.cs` contains command-level and byte-stream tests for the Batch 24 implementation. `CliTests.cs` executes the copied native apphost to verify the real process boundary. The ordering and loop fixtures mirror GNU Coreutils 9.11 `tests/misc/tsort.pl`, with host-generated line endings as required by repository policy.

Additional coverage verifies:

- exact space/tab/line-feed tokenization and byte-preserving node identity;
- equal-pair declarations and duplicate relations;
- GNU option permutation, `-w`, `--`, help, version, and invalid syntax;
- odd-token, missing-file, input-failure, and output-failure diagnostics;
- cancellation and caller-owned stream lifetime;
- deep acyclic graphs without recursive traversal.
