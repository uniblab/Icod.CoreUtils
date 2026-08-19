# Icod.ProcPs.Watch

`Icod.ProcPs.Watch` is the procps-ng-style periodic command display for the Icod ProcPs family. It executes children through the shared cross-platform process executor and presents captured output through the reusable ProcPs full-screen terminal lifecycle introduced by `Icod.ProcPs.Tload`.

The implementation supports fixed-delay and precise fixed-rate cadence, direct `--exec` argument-safe launches, the default shell form, merged child standard output/error, visible-screen change detection, difference highlighting, ANSI SGR color preservation, resize-aware rendering, beep/error-exit behavior, equexit/chgexit, headers, wrapping control, cancellation, suspension/resume, and guaranteed best-effort terminal restoration.

`watch` requires an interactive standard-output terminal. Redirected standard output is rejected so a full-screen refresh stream is not silently emitted into a pipe or regular file. Interactive keyboard commands and screenshot capture are outside Batch 66; `--shotsdir` is accepted for command-line compatibility but has no effect until screenshot-key handling is introduced.
