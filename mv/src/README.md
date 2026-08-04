# mv implementation

`mv` is the GNU-facing policy and diagnostic layer over the Batch 44 shared copy/move engine.

The command prefers direct same-filesystem renames. Existing-destination backups and rename fallbacks use the E5/E6 copy path, and the source is removed only after the destination operation completes. `--no-copy` disables cross-filesystem fallback.
