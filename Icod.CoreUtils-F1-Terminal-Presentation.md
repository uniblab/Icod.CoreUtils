# Completion Gate F1 — Terminal-aware presentation

## Scope

Completion Gate F1 adds the presentation observations required by GNU Coreutils `dircolors`, `ls`, `dir`, and `vdir`. It does not implement their command grammars or listing engine, and it does not introduce terminal-control mechanics reserved for later gates.

## API boundary

The implementation resides in `Icod.CoreUtils.Shared.Terminal` and is provisionally classified as an `Icod.CommandFramework` candidate.

- `ITerminalDeviceProvider` reports attached, redirected, unavailable, and failed standard-stream observations.
- `SystemTerminalDeviceProvider` uses managed console APIs and converts unsupported or failed probes into controlled results.
- `IEnvironmentVariableProvider` and `TerminalEnvironmentSnapshot` isolate `TERM`, `COLORTERM`, `COLUMNS`, `LINES`, `SHELL`, and `QUOTING_STYLE` from command policy.
- `TerminalPresentationProvider` resolves geometry with explicit environment, terminal, and fallback provenance.
- `TerminalColorPolicy` resolves never, auto, and always modes and infers ANSI 16-color, 256-color, and true-color capability.
- `FileNamePresentationPolicy` and `FileNamePresenter` implement the directory-listing quoting vocabulary and deterministic control-character presentation.

## Conformance choices

GNU listing defaults are represented above this neutral observation layer:

- output attached to a terminal defaults to `shell-escape` quoting; redirected output defaults to `literal`;
- automatic color requires an attached non-`dumb` terminal;
- forced color remains enabled for redirected output and uses a conservative ANSI 16-color fallback when no terminal name is available;
- `TERM` is retained as the `dircolors` terminal-name input, while `COLORTERM` remains a separate color-depth hint;
- `COLUMNS` and `LINES` override host dimensions when they contain positive decimal values;
- unavailable terminal dimensions fall back deterministically to 80 columns and 24 rows unless the caller supplies different positive values;
- an unrecognized `QUOTING_STYLE` value remains available in the environment snapshot so the consuming command can issue its GNU-compatible diagnostic, while the convenience resolver uses the normal terminal-sensitive fallback;
- control-character escape policy is accepted only with a quoting style that provides escape syntax.

The presenter supplies deterministic, pasteable GNU-style forms rather than command-specific quote minimization. The locale and C-locale quote styles use stable incubation delimiters. A future locale quotation provider may enrich those delimiters without changing the presentation-policy contract.

## Validation

Dedicated Shared tests cover injected attachment and redirection, environment and terminal geometry precedence, unavailable-host fallback, terminal names, color depth, never/auto/always behavior, every documented quoting-style token, control-character replacement, shell escaping, C escaping, and a system-provider smoke test suitable for all three required CI runners.
