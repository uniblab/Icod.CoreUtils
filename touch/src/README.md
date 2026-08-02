# touch source

This directory contains the command front end and the command-local POSIX `touch` timestamp parser. Timestamp observation and mutation are delegated to the authoritative Shared metadata provider, while GNU free-form date operands reuse the Shared GNU date parser.

The implementation updates access and modification timestamps independently, supports reference-relative dates and directories, and applies the E3R no-follow policy to symbolic-link objects where the host provider advertises that capability. Missing operands are created only when dereference policy permits ordinary file creation.
