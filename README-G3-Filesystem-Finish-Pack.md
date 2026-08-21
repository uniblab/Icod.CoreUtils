# G3 filesystem contraction finish pack

Prepared for `Icod.CoreUtils` branch `Gate_G3`.

Observed starting HEAD:

`17dc47022f22e1b82fdd3a5eef71e82bca49fed5`

This pack contains four deliberately separate stages. Build/test and commit after
each stage before running the next one.

1. `Apply-G3H1.cmd` — cut surviving RecursiveMutation consumers to
   `Icod.CommandFramework.FileSystem.RecursiveMutation`.
2. `Apply-G3H2.cmd` — delete the duplicate CoreUtils RecursiveMutation source
   and duplicated Shared tests.
3. `Apply-G3I1.cmd` — cut surviving TransactionalReplacement consumers to
   `Icod.CommandFramework.FileSystem.TransactionalReplacement`.
4. `Apply-G3I2.cmd` — delete the duplicate CoreUtils TransactionalReplacement
   source and duplicated Shared tests.

Each stage supports `-DryRun`.

Example:

```powershell
Apply-G3H1.cmd -DryRun
Apply-G3H1.cmd
git diff --check
git status --short
git diff
# build/test, then commit

Apply-G3H2.cmd -DryRun
...
```

## Safety changes after the G3G1 incident

These scripts do **not** perform generic C# `using` de-duplication.

Consumer stages make only exact namespace-string replacements, excluding the
duplicate implementation/test directories scheduled for the following excision
stage.

Excision stages verify exact directory contents before recursive deletion and
refuse to proceed if surviving code still references the old namespace.

Git cleanliness checks ignore untracked helper files but reject tracked or
staged changes.

The Gate G Unicode filename is constructed with `[char]0x2014`; it is never
round-tripped through `git diff --name-only`.

## After this pack

The duplicated RecursiveMutation and TransactionalReplacement families should
be gone from CoreUtils Shared. The roadmap advances to `G3J`, the root
filesystem operations audit/contraction. That audit is intentionally separate
because `SystemFileSystemOperations.cs` is not an exact duplicate of the
framework copy.
