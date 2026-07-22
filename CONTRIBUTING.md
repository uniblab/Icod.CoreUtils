# Contributing to Icod.CoreUtils

Thank you for contributing. This document describes the project's conventions and the steps to add or modify utilities.

## Standards
- Follow the repository `.editorconfig` exactly. It defines formatting and C# rules enforced by analyzers.
- Target framework: `.NET 9` (`net9.0`).
- Nullable annotations enabled.
- Use file-scoped namespaces.
- Types and public members: PascalCase. Locals/parameters: camelCase.
- Prefer `var` when the type is obvious from the right-hand side.
- Keep lines <= 120 characters.

## Project layout
- `Shared/` contains `Icod.CoreUtils.Shared` (class library) with common helpers.
- Each utility lives in its own project directory with project name `Icod.CoreUtils.<name>` and is a console app producing a standalone executable.
- A solution file `Icod.CoreUtils.sln` includes all projects.
- Centralize common SDK properties in `Directory.Build.props` at repository root.

## Build and run
- To scaffold: run `.\setup-coreutils.ps1` from the repository root (PowerShell).
- To build: `dotnet build Icod.CoreUtils.sln -c Release`
- To run a single utility: `dotnet run --project <utility-folder> -- <args>` or run the generated executable from `bin\Release\net9.0`.

## Workflow and PRs
- Fork, create a branch named `feature/<short-desc>` or `fix/<short-desc>`.
- Write unit tests where appropriate and ensure they pass.
- Run `dotnet format` and `dotnet build` before submitting PR.
- PR description should explain the change, include before/after behavior, and reference any related issues.

## Commit messages
- Use imperative, present-tense subject: `Add mkdir -p support`.
- Keep the first line concise (<= 72 chars); add body if needed.

## CI
- Pushes to `main` must pass formatting, build, and tests.

## Contact
- For questions open an issue.