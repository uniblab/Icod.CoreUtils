namespace Icod.DiffUtils.SDiff;

using System.Text;
using Icod.CommandFramework.Diagnostics;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Runs interactive merge commands and commits the output only after a complete merge.</summary>
internal static class SDiffMergeEngine {
	/// <summary>Runs an interactive merge into a transactional output path.</summary>
	/// <param name="comparison">The aligned comparison.</param>
	/// <param name="left">The materialized left input.</param>
	/// <param name="right">The materialized right input.</param>
	/// <param name="options">The validated command options.</param>
	/// <param name="context">The injected command context.</param>
	/// <param name="editor">The interactive editor implementation.</param>
	/// <returns>A task representing the merge operation.</returns>
	public static async Task MergeAsync(
		SDiffComparison comparison,
		SDiffInput left,
		SDiffInput right,
		SDiffOptions options,
		CommandContext context,
		ISDiffEditor editor
	) {
		ArgumentNullException.ThrowIfNull( comparison );
		ArgumentNullException.ThrowIfNull( left );
		ArgumentNullException.ThrowIfNull( right );
		ArgumentNullException.ThrowIfNull( options );
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( editor );
		var outputPath = options.OutputPath ?? throw new InvalidOperationException( "An output path is required." );
		var fullPath = System.IO.Path.GetFullPath( outputPath );
		var directory = System.IO.Path.GetDirectoryName( fullPath ) ?? Directory.GetCurrentDirectory();
		var temporaryPath = System.IO.Path.Combine(
			directory,
			$".{System.IO.Path.GetFileName( fullPath )}.sdiff-{Guid.NewGuid():N}.tmp"
		);
		var committed = false;
		try {
			await using ( var stream = new FileStream(
				temporaryPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				65536,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			) ) {
				await using var writer = new StreamWriter( stream, new UTF8Encoding( false ), 65536, leaveOpen: true ) {
					NewLine = Environment.NewLine
				};
				var display = new SDiffOutputWriter( context.StandardOutput, options, context.CancellationToken );
				var verboseCommonLines = true;
				foreach ( var group in comparison.Groups ) {
					context.CancellationToken.ThrowIfCancellationRequested();
					if ( !group.IsDifferent ) {
						await display.WriteGroupAsync( group, verboseCommonLines ).ConfigureAwait( false );
						await WriteLinesAsync( writer, group.LeftLines, context.CancellationToken ).ConfigureAwait( false );
						continue;
					}
					await display.WriteGroupAsync( group, showCommon: true ).ConfigureAwait( false );
					var selected = await SelectDifferenceAsync(
						group,
						left,
						right,
						context,
						editor,
						verboseCommonLines
					).ConfigureAwait( false );
					verboseCommonLines = selected.VerboseCommonLines;
					await WriteLinesAsync( writer, selected.Lines, context.CancellationToken ).ConfigureAwait( false );
				}
				await writer.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
				await stream.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
			}
			File.Move( temporaryPath, fullPath, overwrite: true );
			committed = true;
		} finally {
			if ( !committed ) {
				try {
					File.Delete( temporaryPath );
				} catch ( IOException ) {
					// Cleanup must not mask the original failure or quit.
				} catch ( UnauthorizedAccessException ) {
					// Cleanup must not mask the original failure or quit.
				}
			}
		}
	}

	private static async Task<Selection> SelectDifferenceAsync(
		SDiffGroup group,
		SDiffInput left,
		SDiffInput right,
		CommandContext context,
		ISDiffEditor editor,
		bool verboseCommonLines
	) {
		while ( true ) {
			context.CancellationToken.ThrowIfCancellationRequested();
			await context.StandardOutput.WriteAsync( "%".AsMemory(), context.CancellationToken ).ConfigureAwait( false );
			await context.StandardOutput.FlushAsync( context.CancellationToken ).ConfigureAwait( false );
			var command = await context.StandardInput.ReadLineAsync( context.CancellationToken ).ConfigureAwait( false );
			if ( null == command ) {
				throw new IOException( "end of file on standard input" );
			}
			command = string.Concat( command.Where( character => !char.IsWhiteSpace( character ) ) ).ToLowerInvariant();
			switch ( command ) {
				case "l":
				case "1":
					return new Selection( group.LeftLines, verboseCommonLines );
				case "r":
				case "2":
					return new Selection( group.RightLines, verboseCommonLines );
				case "s":
					verboseCommonLines = false;
					continue;
				case "v":
					verboseCommonLines = true;
					continue;
				case "q":
					throw new SDiffQuitException();
				case "e":
					return await EditAsync( Array.Empty<ComparisonLine>(), editor, context, verboseCommonLines ).ConfigureAwait( false );
				case "eb":
					return await EditAsync(
						group.LeftLines.Concat( group.RightLines ).ToArray(),
						editor,
						context,
						verboseCommonLines
					).ConfigureAwait( false );
				case "el":
				case "e1":
					return await EditAsync( group.LeftLines, editor, context, verboseCommonLines ).ConfigureAwait( false );
				case "er":
				case "e2":
					return await EditAsync( group.RightLines, editor, context, verboseCommonLines ).ConfigureAwait( false );
				case "ed":
					return await EditAsync(
						BuildDecoratedEditorInput( group, left, right ),
						editor,
						context,
						verboseCommonLines
					).ConfigureAwait( false );
				default:
					await WriteMergeHelpAsync( context.StandardError, CancellationToken.None ).ConfigureAwait( false );
					break;
			}
		}
	}

	private static async Task<Selection> EditAsync(
		IReadOnlyList<ComparisonLine> initial,
		ISDiffEditor editor,
		CommandContext context,
		bool verboseCommonLines
	) {
		var result = await editor.EditAsync( initial, context.CancellationToken ).ConfigureAwait( false );
		return new Selection( result, verboseCommonLines );
	}

	private static IReadOnlyList<ComparisonLine> BuildDecoratedEditorInput(
		SDiffGroup group,
		SDiffInput left,
		SDiffInput right
	) {
		var lines = new List<ComparisonLine>();
		lines.Add( new ComparisonLine(
			$"--- {left.DisplayName} {FormatRange( group.LeftStart, group.LeftLines.Count )}",
			true
		) );
		lines.AddRange( group.LeftLines );
		lines.Add( new ComparisonLine(
			$"+++ {right.DisplayName} {FormatRange( group.RightStart, group.RightLines.Count )}",
			true
		) );
		lines.AddRange( group.RightLines );
		return lines.AsReadOnly();
	}

	private static string FormatRange( int start, int length ) {
		if ( 0 == length ) {
			return start.ToString( System.Globalization.CultureInfo.InvariantCulture );
		}
		var first = start + 1;
		return 1 == length
			? first.ToString( System.Globalization.CultureInfo.InvariantCulture )
			: $"{first},{first + length - 1}";
	}

	private static async Task WriteLinesAsync(
		TextWriter writer,
		IReadOnlyList<ComparisonLine> lines,
		CancellationToken cancellationToken
	) {
		foreach ( var line in lines ) {
			cancellationToken.ThrowIfCancellationRequested();
			var content = line.Content;
			if ( line.HasLineTerminator
				&& writer.NewLine.StartsWith( '\r' )
				&& content.EndsWith( '\r' ) ) {
				content = content[..^1];
			}
			await writer.WriteAsync( content.AsMemory(), cancellationToken ).ConfigureAwait( false );
			if ( line.HasLineTerminator ) {
				await writer.WriteLineAsync( ReadOnlyMemory<char>.Empty, cancellationToken ).ConfigureAwait( false );
			}
		}
	}

	private static async Task WriteMergeHelpAsync( TextWriter error, CancellationToken cancellationToken ) {
		var lines = new[] {
			"ed:\tEdit then use both versions, each decorated with a header.",
			"eb:\tEdit then use both versions.",
			"el or e1:\tEdit then use the left version.",
			"er or e2:\tEdit then use the right version.",
			"e:\tDiscard both versions then edit a new one.",
			"l or 1:\tUse the left version.",
			"r or 2:\tUse the right version.",
			"s:\tSilently include common lines.",
			"v:\tVerbosely include common lines.",
			"q:\tQuit."
		};
		foreach ( var line in lines ) {
			await error.WriteLineAsync( line.AsMemory(), cancellationToken ).ConfigureAwait( false );
		}
	}

	private sealed record Selection( IReadOnlyList<ComparisonLine> Lines, bool VerboseCommonLines );
}
