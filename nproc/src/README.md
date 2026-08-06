# `nproc` command policy

The Completion Gate F2 provider reports factual host and process observations.
This directory deliberately owns the GNU interpretation layered over those
facts:

- `--all` selects the installed/configured host count and ignores OpenMP and
  quota limits;
- otherwise affinity and the runtime process count are both process-scoped and
  the smaller available observation is used;
- `OMP_NUM_THREADS` supplies an explicit positive count, capped only by a valid
  `OMP_THREAD_LIMIT`;
- without `OMP_NUM_THREADS`, `OMP_THREAD_LIMIT` and a container, cgroup, or job
  quota may reduce the process count;
- fractional quota capacity is rounded to the nearest processor, with a minimum
  of one;
- `--ignore=N` is applied last and can never reduce the result below one.

Invalid or zero OpenMP values are ignored, matching the command's environment
fallback behavior. Provider failures produce a controlled diagnostic and a
nonzero exit status; unavailable individual facts use documented fallbacks and
ultimately the GNU-required result of one.
