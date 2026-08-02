namespace Icod.Patch;

using System.Security;
using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>Contains validated Wave B2 invocation options.</summary>
internal sealed class PatchOptions {
	/// <summary>Gets or initializes the optional original-file operand.</summary>
	public string? OriginalFile { get; init; }

	/// <summary>Gets or initializes the selected patch source, or <see langword="null"/> for standard input.</summary>
	public string? PatchFile { get; init; }


	/// <summary>Gets or initializes the optional working directory selected by <c>-d</c>.</summary>
	public string? Directory { get; init; }

	/// <summary>Gets or initializes the explicit component strip count, or <see langword="null"/> for basename selection.</summary>
	public int? StripCount { get; init; }

	/// <summary>Gets or initializes whether POSIX filename-selection policy is active.</summary>
	public bool Posix { get; init; }

	/// <summary>Gets or initializes whether target symbolic links are followed.</summary>
	public bool FollowSymbolicLinks { get; init; }

	/// <summary>Gets or initializes GNU version-control retrieval policy.</summary>
	public int Get { get; init; }

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

/// <summary>Coordinates patch-source acquisition and Wave B2 path planning.</summary>
internal static class PatchApplication {
	/// <summary>Parses the selected patch source without mutating target files.</summary>
	/// <param name="options">The validated invocation options.</param>
	/// <param name="context">The command context.</param>
	/// <param name="planner">An optional injected path planner.</param>
	/// <returns>The process status.</returns>
	public static async Task<int> ExecuteAsync(
		PatchOptions options,
		CommandContext context,
		PatchApplicationPlanner? planner = null
	) {
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( context );
		planner ??= new PatchApplicationPlanner();
		Stream? ownedInput = null;
		try {
			var input = context.StandardInputStream;
			if ( null != options.PatchFile && "-" != options.PatchFile ) {
				ownedInput = new FileStream(
					ResolvePatchFilePath( options ),
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
			var document = await PatchDocumentParser.ParseAsync(
				source,
				result,
				PatchParseLimits.Default,
				context.CancellationToken
			).ConfigureAwait( false );
			await using var plan = await planner.BuildAsync(
				source,
				document,
				new PatchPathPlanningOptions {
					OriginalFile = options.OriginalFile,
					Directory = options.Directory,
					StripCount = options.StripCount,
					Posix = options.Posix,
					FollowSymbolicLinks = options.FollowSymbolicLinks,
					Get = options.Get,
					EngineOptions = CreateEngineOptions( options, prerequisiteToken: null )
				},
				context.CancellationToken
			).ConfigureAwait( false );
			foreach ( var file in plan.Files.Where( value => null != value.FailureMessage ) ) {
				await context.Diagnostics.ErrorAsync(
					file.FailureMessage!,
					context.CancellationToken
				).ConfigureAwait( false );
			}
			await context.Diagnostics.ErrorAsync(
				"patch paths and virtual results were planned; filesystem artifacts and transactional replacement begin in phase P8",
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

	/// <summary>Resolves a relative patch-source argument as though <c>-d</c> changed directory first.</summary>
	private static string ResolvePatchFilePath( PatchOptions options ) {
		var patchFile = options.PatchFile!;
		if ( System.IO.Path.IsPathFullyQualified( patchFile ) || null == options.Directory ) {
			return patchFile;
		}
		var directory = System.IO.Path.GetFullPath( options.Directory );
		return System.IO.Path.GetFullPath( patchFile, directory );
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
