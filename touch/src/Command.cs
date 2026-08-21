namespace Icod.CoreUtils.Touch;

using Icod.CommandFramework.CommandLine;
using Icod.CommandFramework.Diagnostics;
using Icod.CommandFramework.FileSystem.Metadata;
using Icod.CommandFramework.FileSystem.Traversal;
using Icod.CommandFramework.Platform;
using Icod.CoreUtils.Shared.Time;

/// <summary>Implements GNU-compatible <c>touch</c> timestamp mutation.</summary>
public static class Command {
	private const string PROGRAM = "touch";
	private const string VERSION = "touch (Icod.CoreUtils) 1.0";

	/// <summary>Executes <c>touch</c> synchronously.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The standard input reader, or <see langword="null"/> for <see cref="Console.In"/>.</param>
	/// <param name="stdout">The standard output writer, or <see langword="null"/> for <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The standard error writer, or <see langword="null"/> for <see cref="Console.Error"/>.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) => RunAsync( args, stdin, stdout, stderr ).GetAwaiter().GetResult();

	/// <summary>Executes <c>touch</c> asynchronously.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="stdin">The standard input reader, or <see langword="null"/> for <see cref="Console.In"/>.</param>
	/// <param name="stdout">The standard output writer, or <see langword="null"/> for <see cref="Console.Out"/>.</param>
	/// <param name="stderr">The standard error writer, or <see langword="null"/> for <see cref="Console.Error"/>.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static Task<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		CancellationToken cancellationToken = default
	) => RunAsync(
		args ?? Array.Empty<string>(),
		new CommandContext(
			PROGRAM,
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error,
			cancellationToken: cancellationToken
		),
		SystemFileSystemMetadataProvider.Instance,
		new SystemDateTimeProvider()
	);

	/// <summary>Executes <c>touch</c> using a complete command context.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static Task<int> RunAsync( string[] args, CommandContext context ) => RunAsync(
		args,
		context,
		SystemFileSystemMetadataProvider.Instance,
		new SystemDateTimeProvider()
	);

	/// <summary>Executes <c>touch</c> using injected filesystem and clock providers.</summary>
	/// <param name="args">The command-line arguments, excluding the executable name.</param>
	/// <param name="context">The command context.</param>
	/// <param name="metadataProvider">The authoritative metadata and timestamp provider.</param>
	/// <param name="dateTimeProvider">The current-time provider.</param>
	/// <returns>Zero when every operand succeeds; otherwise one.</returns>
	public static async Task<int> RunAsync(
		string[] args,
		CommandContext context,
		IFileSystemMetadataProvider metadataProvider,
		IDateTimeProvider dateTimeProvider
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( metadataProvider );
		ArgumentNullException.ThrowIfNull( dateTimeProvider );
		try {
			var result = CreateParser().Parse( args ?? Array.Empty<string>() );
			if ( await WriteParseErrorsAsync( result, context ).ConfigureAwait( false ) ) {
				return 1;
			}
			if ( result.HasOption( "help" ) ) {
				await WriteHelpAsync( context ).ConfigureAwait( false );
				return 0;
			}
			if ( result.HasOption( "version" ) ) {
				await context.StandardOutput.WriteLineAsync(
					VERSION.AsMemory(), context.CancellationToken
				).ConfigureAwait( false );
				return 0;
			}
			if ( 0 == result.Operands.Count ) {
				await context.Diagnostics.ErrorAsync(
					"missing file operand", context.CancellationToken
				).ConfigureAwait( false );
				return 1;
			}

			var updateAccess = result.HasOption( "access" );
			var updateModification = result.HasOption( "modification" );
			foreach ( var option in result.Options.Where( item => item.Definition.Key == "time" ) ) {
				if ( !ApplyTimeWord( option.Value, ref updateAccess, ref updateModification ) ) {
					await context.Diagnostics.ErrorAsync(
						$"invalid argument '{option.Value}' for '--time'",
						context.CancellationToken
					).ConfigureAwait( false );
					return 1;
				}
			}
			if ( !updateAccess && !updateModification ) {
				updateAccess = true;
				updateModification = true;
			}

			var noCreate = result.HasOption( "no-create" );
			var noDereference = result.HasOption( "no-dereference" );
			var dereferenceMode = noDereference
				? PathDereferenceMode.NoFollow
				: PathDereferenceMode.FollowEligiblePathIndirection;
			var dateText = result.GetLastValue( "date" );
			var referencePath = result.GetLastValue( "reference" );
			var timestampText = result.GetLastValue( "timestamp" );
			if ( null != timestampText && (null != dateText || null != referencePath) ) {
				await context.Diagnostics.ErrorAsync(
					"the options to specify dates are mutually exclusive",
					context.CancellationToken
				).ConfigureAwait( false );
				return 1;
			}

			var parseBaseTime = dateTimeProvider.Now;
			DateTimeOffset? timestampValue = null;
			if ( null != timestampText ) {
				if ( !TouchTimestampParser.TryParse(
					timestampText,
					parseBaseTime,
					TimeZoneInfo.Local,
					out var parsedTimestamp,
					out var diagnostic
				) ) {
					await context.Diagnostics.ErrorAsync(
						$"invalid date format '{timestampText}': {diagnostic}",
						context.CancellationToken
					).ConfigureAwait( false );
					return 1;
				}
				timestampValue = parsedTimestamp;
			}

			FileSystemMetadata? reference = null;
			if ( null != referencePath ) {
				try {
					reference = await metadataProvider.GetMetadataAsync(
						referencePath, dereferenceMode, context.CancellationToken
					).ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
					await context.Diagnostics.ErrorAsync(
						$"failed to get attributes of '{referencePath}': {exception.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					return 1;
				}
			}

			var accessChange = FileTimestampChange.Unchanged;
			var modificationChange = FileTimestampChange.Unchanged;
			if ( null != timestampValue ) {
				accessChange = FileTimestampChange.At( timestampValue.Value );
				modificationChange = FileTimestampChange.At( timestampValue.Value );
			} else if ( null != dateText ) {
				if ( updateAccess ) {
					if ( null != reference && !reference.AccessTime.IsAvailable ) {
						await context.Diagnostics.ErrorAsync(
							$"reference file '{referencePath}' has no available access time",
							context.CancellationToken
						).ConfigureAwait( false );
						return 1;
					}
					var baseTime = null != reference
						? reference.AccessTime.GetRequiredValue()
						: parseBaseTime;
					var parsed = GnuDateParser.Parse( dateText, baseTime, TimeZoneInfo.Local );
					if ( !parsed.Success ) {
						await context.Diagnostics.ErrorAsync(
							$"invalid date format '{dateText}': {parsed.Diagnostic}",
							context.CancellationToken
						).ConfigureAwait( false );
						return 1;
					}
					accessChange = FileTimestampChange.At( parsed.Value );
				}
				if ( updateModification ) {
					if ( null != reference && !reference.ModificationTime.IsAvailable ) {
						await context.Diagnostics.ErrorAsync(
							$"reference file '{referencePath}' has no available modification time",
							context.CancellationToken
						).ConfigureAwait( false );
						return 1;
					}
					var baseTime = null != reference
						? reference.ModificationTime.GetRequiredValue()
						: parseBaseTime;
					var parsed = GnuDateParser.Parse( dateText, baseTime, TimeZoneInfo.Local );
					if ( !parsed.Success ) {
						await context.Diagnostics.ErrorAsync(
							$"invalid date format '{dateText}': {parsed.Diagnostic}",
							context.CancellationToken
						).ConfigureAwait( false );
						return 1;
					}
					modificationChange = FileTimestampChange.At( parsed.Value );
				}
			} else if ( null != reference ) {
				if ( updateAccess ) {
					if ( !reference.AccessTime.IsAvailable ) {
						await context.Diagnostics.ErrorAsync(
							$"reference file '{referencePath}' has no available access time",
							context.CancellationToken
						).ConfigureAwait( false );
						return 1;
					}
					accessChange = FileTimestampChange.At( reference.AccessTime.GetRequiredValue() );
				}
				if ( updateModification ) {
					if ( !reference.ModificationTime.IsAvailable ) {
						await context.Diagnostics.ErrorAsync(
							$"reference file '{referencePath}' has no available modification time",
							context.CancellationToken
						).ConfigureAwait( false );
						return 1;
					}
					modificationChange = FileTimestampChange.At( reference.ModificationTime.GetRequiredValue() );
				}
			} else {
				accessChange = FileTimestampChange.CurrentTime;
				modificationChange = FileTimestampChange.CurrentTime;
			}

			var exitCode = 0;
			foreach ( var operand in result.Operands ) {
				context.CancellationToken.ThrowIfCancellationRequested();
				var operationalPath = ResolveOperationalPath( operand );
				if ( null == operationalPath ) {
					await context.Diagnostics.ErrorAsync(
						"cannot touch '-': standard-output timestamp mutation is unsupported on Windows",
						context.CancellationToken
					).ConfigureAwait( false );
					exitCode = 1;
					continue;
				}
				bool exists;
				try {
					exists = await ExistsAsync(
						metadataProvider, operationalPath, dereferenceMode, context.CancellationToken
					).ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
					await context.Diagnostics.ErrorAsync(
						$"cannot touch '{operand}': {exception.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					exitCode = 1;
					continue;
				}
				if ( !exists ) {
					if ( noCreate ) {
						continue;
					}
					if ( noDereference ) {
						await context.Diagnostics.ErrorAsync(
							$"cannot touch '{operand}': the pathname does not exist",
							context.CancellationToken
						).ConfigureAwait( false );
						exitCode = 1;
						continue;
					}
					try {
						await using var stream = new FileStream(
							operationalPath,
							FileMode.OpenOrCreate,
							FileAccess.Write,
							FileShare.ReadWrite | FileShare.Delete,
							1,
							FileOptions.Asynchronous
						);
					} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
						await context.Diagnostics.ErrorAsync(
							$"cannot touch '{operand}': {exception.Message}",
							context.CancellationToken
						).ConfigureAwait( false );
						exitCode = 1;
						continue;
					}
				}

				var request = new FileTimestampMutationRequest {
					AccessTime = updateAccess ? accessChange : FileTimestampChange.Unchanged,
					ModificationTime = updateModification ? modificationChange : FileTimestampChange.Unchanged,
				};
				PlatformOperationResult operation;
				try {
					operation = await metadataProvider.SetTimestampsAsync(
						operationalPath, request, dereferenceMode, context.CancellationToken
					).ConfigureAwait( false );
				} catch ( Exception exception ) when ( IsFileSystemException( exception ) ) {
					await context.Diagnostics.ErrorAsync(
						$"cannot touch '{operand}': {exception.Message}",
						context.CancellationToken
					).ConfigureAwait( false );
					exitCode = 1;
					continue;
				}
				if ( !operation.Succeeded ) {
					var detail = operation.Message ?? (operation.Supported
						? "timestamp update failed"
						: "timestamp update is unsupported");
					await context.Diagnostics.ErrorAsync(
						$"cannot touch '{operand}': {detail}",
						context.CancellationToken
					).ConfigureAwait( false );
					exitCode = 1;
				}
			}
			return exitCode;
		} catch ( OperationCanceledException ) {
			return CommandExitCodes.Canceled;
		}
	}

	private static string? ResolveOperationalPath( string operand ) {
		if ( "-" != operand ) {
			return operand;
		}
		return OperatingSystem.IsWindows() ? null : "/dev/stdout";
	}

	private static async ValueTask<bool> ExistsAsync(
		IFileSystemMetadataProvider provider,
		string path,
		PathDereferenceMode dereferenceMode,
		CancellationToken cancellationToken
	) {
		try {
			_ = await provider.GetMetadataAsync( path, dereferenceMode, cancellationToken ).ConfigureAwait( false );
			return true;
		} catch ( FileNotFoundException ) {
			return false;
		} catch ( DirectoryNotFoundException ) {
			return false;
		}
	}

	private static bool ApplyTimeWord(
		string? value,
		ref bool updateAccess,
		ref bool updateModification
	) {
		switch ( value ) {
			case "access":
			case "atime":
			case "use":
				updateAccess = true;
				return true;
			case "modify":
			case "mtime":
				updateModification = true;
				return true;
			default:
				return false;
		}
	}

	private static bool IsFileSystemException( Exception exception ) => exception is
		IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

	private static OptionParser CreateParser() => new(
		new[] {
			new OptionDefinition( "access", 'a', new[] { "access" } ),
			new OptionDefinition( "no-create", 'c', new[] { "no-create" } ),
			new OptionDefinition( "date", 'd', new[] { "date" }, OptionValueArity.Required ),
			new OptionDefinition( "ignore-f", 'f' ),
			new OptionDefinition( "no-dereference", 'h', new[] { "no-dereference" } ),
			new OptionDefinition( "modification", 'm', new[] { "modification" } ),
			new OptionDefinition( "reference", 'r', new[] { "reference" }, OptionValueArity.Required ),
			new OptionDefinition(
				"time", longNames: new[] { "time" }, valueArity: OptionValueArity.Required
			),
			new OptionDefinition( "timestamp", 't', valueArity: OptionValueArity.Required ),
			new OptionDefinition( "help", longNames: new[] { "help" } ),
			new OptionDefinition( "version", longNames: new[] { "version" } ),
		},
		new OptionParserSettings {
			AllowLongOptionAbbreviations = true,
			Ordering = OptionOrdering.Permute
		}
	);

	private static async Task<bool> WriteParseErrorsAsync(
		OptionParseResult result,
		CommandContext context
	) {
		if ( result.IsSuccess ) {
			return false;
		}
		foreach ( var error in result.Errors ) {
			await context.StandardError.WriteLineAsync(
				OptionDiagnosticFormatter.Format( context.ProgramName, error ).AsMemory(),
				context.CancellationToken
			).ConfigureAwait( false );
		}
		return true;
	}

	private static async Task WriteHelpAsync( CommandContext context ) {
		const string text = """
Usage: touch [OPTION]... FILE...
Update the access and modification times of each FILE to the current time.
  -a                     change only the access time
  -c, --no-create        do not create any files
  -d, --date=STRING      parse STRING and use it instead of the current time
  -f                     ignored for compatibility
  -h, --no-dereference   affect each symbolic link instead of its referent
  -m                     change only the modification time
  -r, --reference=FILE   use this file's times instead of the current time
      --time=WORD        change the specified time: access, atime, use, modify, or mtime
  -t STAMP               use [[CC]YY]MMDDhhmm[.ss] instead of the current time
      --help             display this help and exit
      --version          output version information and exit
""";
		await context.StandardOutput.WriteAsync(
			text.ReplaceLineEndings( Environment.NewLine ).AsMemory(),
			context.CancellationToken
		).ConfigureAwait( false );
	}
}
