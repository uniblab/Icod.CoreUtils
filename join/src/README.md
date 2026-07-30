# `join` implementation

`Command.cs` performs a sorted two-way relational merge using Shared byte records and collation. It retains only current records and the equal-key groups needed for GNU duplicate-key Cartesian products. Field parsing, headers, paired/unpaired policy, and output formats remain local to `join`.
