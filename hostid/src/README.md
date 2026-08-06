# `hostid` command boundary

`hostid` is intentionally a thin consumer of `Icod.CoreUtils.Shared.Host`.
The shared provider obtains and normalizes the factual 32-bit identifier; the
command owns only GNU-compatible option validation, lowercase eight-digit
hexadecimal presentation, diagnostics, cancellation, and exit status.

The command never prints the raw Windows `MachineGuid`, Linux `machine-id`, or
other stable textual source used by a provider fallback.
