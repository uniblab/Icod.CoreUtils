# `shuf` tests

The dedicated test project covers deterministic and cryptographic execution paths through the public command boundary. It verifies byte preservation, partial and full permutations, echo and range inputs, repeat mode, NUL records, safe in-place output, zero-count input short-circuiting and repeat-mode random-source validation, random-source exhaustion, cancellation, and temporary-spool cleanup.

The suite also fixes GNU option repetition rules, random-source opening and exhaustion order, rejection of biased raw values, preservation of an existing output after finite selection failure, and cleanup after success, cancellation, and output failure.
