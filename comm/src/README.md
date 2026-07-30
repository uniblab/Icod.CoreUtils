# `comm` implementation

`Command.cs` is a byte-preserving, two-way streaming merge over sorted inputs. It consumes Shared record framing, locale collation, option parsing, diagnostics, and standard-stream adapters. Column suppression, order-check policy, totals, and GNU output formatting remain command-local.
