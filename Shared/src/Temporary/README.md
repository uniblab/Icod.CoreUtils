# Shared temporary-object infrastructure

`Icod.CoreUtils.Shared.Temporary` centralizes secure temporary-name generation and exclusive object creation.

## Security properties

- Candidate substitutions use cryptographically secure, unbiased base-62 values.
- Regular files use exclusive create-new semantics and are opened read/write with no sharing.
- Unix regular files request mode `0600`; Unix directories use native `mkdir(..., 0700)`. The process umask may remove permissions but cannot add group or other access.
- Existing files, directories, and symbolic links are collisions and are never followed or replaced.
- Collisions are retried up to at least `62^3` attempts, matching the GNU minimum search bound.
- Name-only generation checks path existence but cannot reserve the name. It is inherently subject to a time-of-check/time-of-use race and must not be used for security-sensitive creation.

## Platforms

Windows, Linux, and macOS are the required validation platforms. FreeBSD uses the documented POSIX `mkdir` and `lstat` ABIs as **best effort** support. Unknown platforms fall back to managed filesystem APIs where an equivalent controlled operation is available.

Callers own all created objects after a successful return. If a command cannot report a newly created pathname, it should remove the object before returning failure.
