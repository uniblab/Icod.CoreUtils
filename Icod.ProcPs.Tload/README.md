# Icod.ProcPs.Tload

`Icod.ProcPs.Tload` is the procps-ng-style terminal load-average graph command for the Icod ProcPs family.

The command uses `Icod.ProcPs.Shared` for cross-platform load-average observation, deterministic monotonic refresh sampling, and the reusable full-screen terminal lifecycle. Linux and macOS can provide native load averages through the shared system metrics provider. Platforms without a defensible load-average observation receive a controlled diagnostic rather than fabricated data.

The default output endpoint is the process standard-output terminal. An optional terminal operand selects another writable terminal. Redirected standard output is rejected unless a terminal operand is supplied so an unbounded full-screen refresh stream is not silently emitted into a pipe or regular file.

Supported procps-ng 4.0.6 command options are `-d`/`--delay`, `-s`/`--scale`, `-h`/`--help`, and `-V`/`--version`.
