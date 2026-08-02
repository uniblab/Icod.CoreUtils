# stat source

This directory contains the command front end and its command-local GNU `stat` format engine. The command consumes the authoritative Shared filesystem metadata provider introduced by Completion Gate E3; it does not derive inode-change time, link identity, allocation, ownership, or filesystem information from `FileInfo` approximations.

The E3 filesystem contract does not currently expose total/free inode counts or a native filesystem magic number, so the corresponding numeric filesystem directives report zero as an explicit portability gap. SELinux context is likewise unavailable and is rendered as `?`. `--cached=default` is supported; `always` and `never` fail with a controlled diagnostic until the provider can enforce those host cache policies.
