namespace Icod.Patch;

using System.Security;
using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Contains validated Wave B1 invocation options.</summary>
internal sealed class PatchOptions {
	/// <summary>Gets or initializes the optional original-file operand.</summary>
	public string? OriginalFile { get; init; }

	/// <summary>Gets or initializes the selected patch source, or <see langword="null"/> for standard input.</summary>
	public string? PatchFile { get; init; }

	/// <summary>Gets or initializes whether binary mode was requested.</summary>
	public bool Binary { get; init; }

	/// <summary>Gets or initializes an explicitly selected input format.</summary>
	public PatchFormat? ForcedFormat { get; init; }

	/// <summary>Gets or initializes whether automatic reversal is suppressed.</summary>
	public bool Force { get; init; }

	/// <summary>Gets or initializes whether reversed or already-applied patches are skipped.</summary>
	public bool ForwardOnly { get; init; }

	/// <summary>Gets or initializes whether reverse application is explicit.</summary>
	public bool Reverse { get; init; }

	/// <summary>Gets or initializes whether interactive questions use batch defaults.</summary>
	public bool Batch { get; init; }

	/// <summary>Gets or initializes the maximum context fuzz factor.</summary>
	public int Fuzz { get; init; } = 2;

	/// <summary>Gets or initializes whether horizontal blank runs compare canonically.</summary>
	public bool IgnoreWhitespace { get; init; }

	/// <summary>Gets or initializes optional merge-conflict output.</summary>
	public PatchMergeStyle MergeStyle { get; init; }

	/// <summary>Gets whether later interactive prompts may own standard input.</summary>
	public bool PromptInputAvailable => null != this.PatchFile && "-" != this.PatchFile;
}

/// <summary>Coordinates patch-source acquisition and pure Wave B1 engine configuration.</summary>
internal static class PatchApplication {
	/// <summary>Parses the selected patch source without mutating target files.</summary>
	/// <param name="options">The validated invocation options.</param>
	/// <param name="context">The command context.</param>
	/// <returns>The process status.</returns>
	public static async Task<int> ExecuteAsync( PatchOptions options, CommandContext context ) {
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( context );
		Stream? ownedInput = null;
		try {
			var input = context.StandardInputStream;
			if ( null != options.PatchFile && "-" != options.PatchFile ) {
				ownedInput = new FileStream(
					options.PatchFile,
					FileMode.Open,
					FileAccess.Read,
					FileShare.Read,
					64 * 1024,
					FileOptions.Asynchronous | FileOptions.SequentialScan
				);
				input = ownedInput;
			}
			if ( null == input ) {
				throw new InvalidOperationException( "a binary standard-input stream was not supplied" );
			}
			await using var source = await PatchSource.ReadAsync(
				input,
				PatchScanLimits.Default,
				context.CancellationToken
			).ConfigureAwait( false );
			var result = PatchScanner.Detect(
				source.Records,
				source.Probes,
				options.ForcedFormat
			);
			if ( !result.HasPatch ) {
				await context.Diagnostics.ErrorAsync(
					"Only garbage was found in the patch input.",
					context.CancellationToken
				).ConfigureAwait( false );
				return (int)PatchExitStatus.Trouble;
			}
			_ = await PatchDocumentParser.ParseAsync(
				source,
				result,
				PatchParseLimits.Default,
				context.CancellationToken
			).ConfigureAwait( false );
			var prerequisite = await PatchPrerequisite.ExtractAsync(
				source,
				result.LeadingText,
				context.CancellationToken
			).ConfigureAwait( false );
			_ = CreateEngineOptions( options, prerequisite );
			await context.Diagnostics.ErrorAsync(
				"patch input was recognized and parsed; Wave B1 virtual application is available, but live path integration begins in phase P7",
				context.CancellationToken
			).ConfigureAwait( false );
			return (int)PatchExitStatus.Trouble;
		} catch ( PatchInputException exception ) {
			await context.Diagnostics.ErrorAsync(
				string.Concat(
					"patch input line ",
					exception.Location.LineNumber.ToString( System.Globalization.CultureInfo.InvariantCulture ),
					": ",
					exception.Message
				),
				CancellationToken.None
			).ConfigureAwait( false );
			return (int)PatchExitStatus.Trouble;
		} finally {
			if ( null != ownedInput ) {
				await ownedInput.DisposeAsync().ConfigureAwait( false );
			}
		}
	}

	/// <summary>Maps validated command options into pure application-engine policy.</summary>
	/// <param name="options">The validated command options.</param>
	/// <param name="prerequisiteToken">The optional prerequisite token from leading patch text.</param>
	/// <param name="decisionProvider">An optional interactive decision provider.</param>
	/// <returns>The corresponding engine options.</returns>
	public static PatchEngineOptions CreateEngineOptions(
		PatchOptions options,
		string? prerequisiteToken,
		IPatchDecisionProvider? decisionProvider = null
	) {
		ArgumentNullException.ThrowIfNull( options );
		return new PatchEngineOptions {
			Reverse = options.Reverse,
			Force = options.Force,
			ForwardOnly = options.ForwardOnly,
			Batch = options.Batch,
			Fuzz = options.Fuzz,
			IgnoreWhitespace = options.IgnoreWhitespace,
			MergeStyle = options.MergeStyle,
			PrerequisiteToken = prerequisiteToken,
			DecisionProvider = decisionProvider
		};
	}

	/// <summary>Determines whether an exception represents an expected operational failure.</summary>
	/// <param name="exception">The exception to classify.</param>
	/// <returns><see langword="true"/> for a controlled operational failure.</returns>
	public static bool IsOperationalException( Exception exception ) {
		return exception is IOException
			or PatchApplicationException
			or UnauthorizedAccessException
			or ArgumentException
			or InvalidOperationException
			or NotSupportedException
			or SecurityException;
	}
}
