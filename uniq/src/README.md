# `uniq` implementation

`Command.cs` is an adjacent-record streaming state machine over Shared byte records. Comparison slices preserve GNU's exact-byte semantics, with locale-aware case folding only for `--ignore-case`; skip and width counts respect multibyte characters. Ordinary filtering retains one representative and a count, while `--all-repeated` and `--group` emit records as soon as group status is known. Shared temporary workspaces make in-place output safe without coupling `uniq` to another command.
