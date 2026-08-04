# install implementation

The Batch 45 implementation is split into command parsing, installation planning, staged-file policy, and platform adaptation.

- `Command.cs` provides the public synchronous and asynchronous command surface.
- `InstallArgumentParser.cs` owns GNU-facing option precedence and validation.
- `InstallEngine.cs` performs directory creation through E4 and file publication through E6.
- `InstallStripper.cs` invokes only the explicitly requested strip program, without a command shell.
- `InstallSecurityContext.cs` implements SELinux context preservation and explicit labeling through `libselinux`, including destination-policy lookup without shelling to `restorecon`; disabled SELinux policies receive GNU-style warnings.

File replacement is staged beside the destination. Strip, ownership, mode, timestamp, and context operations are completed on the private stage before E6 flushes and atomically publishes it. Existing destinations are never modified in place.
## Path indirection

An explicitly named directory symlink or eligible directory reparse point may anchor target-directory interpretation and `-D` creation of missing descendants. The terminal destination is different: E6 commits against a no-follow ordinary-file observation. A final symbolic link, junction, or other reparse object is therefore rejected with a controlled diagnostic rather than dereferenced or removed non-atomically. The referenced target is left unchanged.

`--debug` implies verbose output and additionally reports whether E6 published a configured private sibling stage or retained an equivalent destination. An explicitly selected strip program is invoked only with `--strip`; otherwise GNU-compatible warning behavior is used.
