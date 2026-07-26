namespace Icod.CoreUtils.Shared.CommandLine;

/// <summary>
/// Formats structured option errors as conventional command-line diagnostics.
/// </summary>
public static class OptionDiagnosticFormatter {

	/// <summary>
	/// Formats an error with a program-name prefix.
	/// </summary>
	public static string Format(
		string programName,
		OptionParseError error
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace(
			programName
		);
		ArgumentNullException.ThrowIfNull(
			error
		);

		var detail = error.Kind switch {
			OptionParseErrorKind.UnknownShortOption => $"invalid option -- '{error.OptionName}'",
			OptionParseErrorKind.UnknownLongOption => $"unrecognized option '{error.OptionName}'",
			OptionParseErrorKind.AmbiguousLongOption => $"option '{error.OptionName}' is ambiguous; possibilities: {string.Join( ", ", error.Candidates.Select( candidate => $"'--{candidate}'" ) )}",
			OptionParseErrorKind.MissingOptionValue => error.OptionName.StartsWith( "--", StringComparison.Ordinal )
				? $"option '{error.OptionName}' requires an argument"
				: $"option requires an argument -- '{error.OptionName}'",
			OptionParseErrorKind.UnexpectedOptionValue => $"option '{error.OptionName}' does not allow an argument",
			OptionParseErrorKind.DuplicateOption => $"option '{error.OptionName}' may not be repeated",
			_ => $"invalid option '{error.OptionName}'"
		};
		return string.Concat(
			programName,
			": ",
			detail
		);
	}

}
