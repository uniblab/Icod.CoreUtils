# `tty` command implementation

Batch 50 implements terminal identification as a thin consumer of Completion Gate F3. The command always inspects standard input, reports the provider-supplied terminal pathname or stable Windows console alias, and supports `-s`, `--silent`, and `--quiet` status-only operation.

Exit statuses intentionally preserve GNU's distinctions: `0` for terminal input, `1` for nonterminal input, `2` for invalid usage, `3` for an output failure, and `4` when the terminal name cannot be determined. The command does not infer terminal attachment from redirected managed streams and does not own or close the borrowed standard-input descriptor.
