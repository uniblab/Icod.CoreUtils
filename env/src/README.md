# env implementation

`Icod.CoreUtils.Env` follows GNU Coreutils 9.11. `Command` owns GNU command-line policy and environment mutation while the current Shared process foundation owns executable lookup, child launch, environment transfer, signal launch policy, asynchronous stream forwarding, cancellation, and termination translation.

`EnvOptions` implements GNU option parsing, including recursive `-S` reparsing. `EnvSplitStringParser` implements the documented shebang split-string grammar and expands `${VARNAME}` against the original environment, before `-i`, `-u`, or `NAME=VALUE` mutations are applied.

The normal POSIX command-line path uses the Shared native launch path with the original command spelling as `argv[0]`, preserving native environment-vector edge cases. An explicit `-a`/`--argv0` uses the same path because `System.Diagnostics.ProcessStartInfo` does not expose a portable independent native `argv[0]`. Windows therefore reports that native launch capability as unsupported rather than silently changing semantics.
