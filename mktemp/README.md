# mktemp

`mktemp` securely creates a unique temporary file or directory and writes its pathname to standard output.

The implementation follows GNU Coreutils 9.11 `mktemp` and supports:

- templates with a final run of at least three `X` characters;
- the default `tmp.XXXXXXXXXX` template;
- `-d`, `--directory`;
- `-q`, `--quiet`;
- `-u`, `--dry-run`;
- `-p DIR`, `--tmpdir[=DIR]`;
- `--suffix=SUFF`;
- deprecated `-t` semantics;
- `--help`, `--version`, and the GNU-compatible `-V` version alias.

## Security

Names use the operating system cryptographic random generator and GNU's 62-character replacement alphabet. Regular files are created with exclusive create-new semantics; directories use native exclusive `mkdir` operations. Existing files, directories, and symbolic links are treated as collisions and are never replaced.

On Unix-like systems, files request mode `0600` and directories request `0700`, subject only to permissions removed by the process umask. Windows relies on the containing directory's access-control list, matching the host security model.

`--dry-run` only checks whether a generated pathname is unused. It does not reserve that name and is inherently unsafe for security-sensitive workflows.

If standard output fails after an object is created, the command attempts to delete that object before returning failure. GNU-compatible `--quiet` also suppresses that write-error diagnostic.

Exit status is `0` for success, `1` for invalid invocation or operational failure, and `130` when canceled through the injected command context.

## Platforms

The required CI platforms are Windows, Ubuntu, and macOS. FreeBSD uses its POSIX `mkdir` and `lstat` interfaces as **best effort** support and is not part of the required test matrix.
