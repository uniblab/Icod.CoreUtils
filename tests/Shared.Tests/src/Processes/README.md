# Process tests

These tests cover Completion Gate F4 process contracts and their controlled platform boundaries.

- Model tests validate identity/reuse tokens, explicit target kinds, signal parsing, and termination translation.
- Environment and lookup tests use isolated temporary directories.
- Runner integration tests execute `ProcessTestHost` through `dotnet` to verify argument fidelity, environment and working-directory construction, stream capture, timeout classification, and child cleanup on Windows, Linux, and macOS.
- System-provider tests restrict live operations to the current test process and signal zero, avoiding destructive mutation of unrelated processes.
