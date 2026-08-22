namespace Icod.DiffUtils.SDiff;

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Invokes <c>EDITOR</c>, or <c>ed</c> when unset, without shell interpolation.</summary>
public sealed class ProcessSDiffEditor : ISDiffEditor {
	/// <inheritdoc />
	public async Task<IReadOnlyList<ComparisonLine>> EditAsync(
		IReadOnlyList<ComparisonLine> initialContent,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( initialContent );
		cancellationToken.ThrowIfCancellationRequested();
		var path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"sdiff-{Guid.NewGuid():N}.tmp" );
		try {
			await WriteLinesAsync( path, initialContent, cancellationToken ).ConfigureAwait( false );
			var commandLine = Environment.GetEnvironmentVariable( "EDITOR" );
			if ( string.IsNullOrWhiteSpace( commandLine ) ) {
				commandLine = "ed";
			}
			var command = SplitCommandLine( commandLine );
			if ( 0 == command.Count ) {
				throw new IOException( "the selected editor command is empty" );
			}
			var startInfo = new ProcessStartInfo {
				FileName = command[0],
				UseShellExecute = false
			};
			for ( var index = 1; index < command.Count; index++ ) {
				startInfo.ArgumentList.Add( command[index] );
			}
			startInfo.ArgumentList.Add( path );
			using var process = new Process { StartInfo = startInfo };
			try {
				if ( !process.Start() ) {
					throw new IOException( $"cannot start editor '{command[0]}'" );
				}
			} catch ( Win32Exception exception ) {
				throw new IOException( $"cannot start editor '{command[0]}': {exception.Message}", exception );
			}
			try {
				await process.WaitForExitAsync( cancellationToken ).ConfigureAwait( false );
			} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
				try {
					if ( !process.HasExited ) {
						process.Kill( entireProcessTree: true );
					}
				} catch ( InvalidOperationException ) {
					// The editor exited between the state check and termination request.
				} catch ( Win32Exception ) {
					// Best-effort termination must not mask cancellation.
				} catch ( NotSupportedException ) {
					// Best-effort termination must not mask cancellation.
				}
				throw;
			}
			if ( 0 != process.ExitCode ) {
				throw new IOException( $"editor exited with status {process.ExitCode}" );
			}
			await using var source = new FileStream(
				path,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				65536,
				FileOptions.Asynchronous | FileOptions.SequentialScan
			);
			var document = await ComparisonDocumentReader.ReadAsync( source, cancellationToken ).ConfigureAwait( false );
			return document.Lines;
		} finally {
			try {
				File.Delete( path );
			} catch ( IOException ) {
				// Best-effort cleanup must not mask the editor result.
			} catch ( UnauthorizedAccessException ) {
				// Best-effort cleanup must not mask the editor result.
			}
		}
	}

	private static async Task WriteLinesAsync(
		string path,
		IReadOnlyList<ComparisonLine> lines,
		CancellationToken cancellationToken
	) {
		await using var stream = new FileStream(
			path,
			FileMode.CreateNew,
			FileAccess.Write,
			FileShare.None,
			65536,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		await using var writer = new StreamWriter( stream, new UTF8Encoding( false ), 65536, leaveOpen: false ) {
			NewLine = Environment.NewLine
		};
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
		await writer.FlushAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static IReadOnlyList<string> SplitCommandLine( string commandLine ) {
		var arguments = new List<string>();
		var builder = new StringBuilder();
		char quote = '\0';
		for ( var index = 0; index < commandLine.Length; index++ ) {
			var character = commandLine[index];
			if ( '\0' != quote ) {
				if ( character == quote ) {
					quote = '\0';
					continue;
				}
				if ( '\'' != quote
					&& '\\' == character
					&& index + 1 < commandLine.Length
					&& commandLine[index + 1] is '"' or '\\' ) {
					builder.Append( commandLine[++index] );
					continue;
				}
				builder.Append( character );
				continue;
			}
			if ( character is '\'' or '"' ) {
				quote = character;
				continue;
			}
			if ( char.IsWhiteSpace( character ) ) {
				if ( 0 < builder.Length ) {
					arguments.Add( builder.ToString() );
					builder.Clear();
				}
				continue;
			}
			if ( '\\' == character && index + 1 < commandLine.Length ) {
				var next = commandLine[index + 1];
				if ( char.IsWhiteSpace( next ) || next is '\'' or '"' or '\\' ) {
					builder.Append( next );
					index++;
					continue;
				}
			}
			builder.Append( character );
		}
		if ( '\0' != quote ) {
			throw new IOException( "unterminated quote in editor command" );
		}
		if ( 0 < builder.Length ) {
			arguments.Add( builder.ToString() );
		}
		return arguments.AsReadOnly();
	}
}
