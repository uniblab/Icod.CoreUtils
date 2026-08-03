# Transactional replacement tests

These tests exercise Completion Gate E6 independently of command policy. They cover secure sibling staging through the injectable provider boundary, GNU backup naming, per-file recovery units, rollback after partial unit commit, continuation across independent units, atomicity requirements, containment rejection, and deterministic cleanup retry.

The in-memory provider intentionally changes stable E3 identity whenever a staged file is published. This makes the tests verify the transaction's observation and revalidation contract without depending on host-specific filesystem timing.
