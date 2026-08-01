namespace Icod.DiffUtils.Diff;

using System.IO.Enumeration;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.DiffUtils.Shared;
using Icod.DiffUtils.Shared.Edits;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Coordinates file, directory, and standard-input comparisons.</summary>
internal sealed class DiffCoordinator {
	private enum PathKind {
		Missing,
		File,
		Directory,
		SymbolicLink,
		StandardInput
	}

	private readonly CommandContext context;
	private readonly DiffOptions options;
	private readonly DiffOutputWriter writer;
	private ComparisonDocument? standardInputDocument;

	/// <summary>Initializes a comparison coordinator.</summary>
	public DiffCoordinator( DiffOptions options, CommandContext context ) {
		this.options = options ?? throw new ArgumentNullException( nameof( options ) );
		this.context = context ?? throw new ArgumentNullException( nameof( context ) );
		this.writer = new DiffOutputWriter(
			options,
			context.StandardOutput,
			context.StandardError,
			context.CancellationToken
		);
	}

	/// <summary>Compares one ordered pair of operands.</summary>
	public async Task<ComparisonStatus> CompareAsync( string oldPath, string newPath ) {
		try {
			this.context.CancellationToken.ThrowIfCancellationRequested();
			var oldKind = GetPathKind( oldPath, this.options.NoDereference );
			var newKind = GetPathKind( newPath, this.options.NoDereference );
			if ( PathKind.Directory == oldKind && PathKind.Directory == newKind ) {
				return await this.CompareDirectoriesAsync( oldPath, newPath, oldExists: true, newExists: true ).ConfigureAwait( false );
			}
			if ( PathKind.Missing == oldKind && PathKind.Directory == newKind && ( this.options.NewFile || this.options.UnidirectionalNewFile ) ) {
				return await this.CompareDirectoriesAsync( oldPath, newPath, oldExists: false, newExists: true ).ConfigureAwait( false );
			}
			if ( PathKind.Directory == oldKind && PathKind.Missing == newKind && this.options.NewFile ) {
				return await this.CompareDirectoriesAsync( oldPath, newPath, oldExists: true, newExists: false ).ConfigureAwait( false );
			}
			if ( PathKind.Directory == oldKind ) {
				if ( PathKind.StandardInput == newKind ) {
					throw new DiffUsageException( "standard input cannot be compared to a directory" );
				}
				oldPath = Path.Combine( oldPath, GetFileNameRequired( newPath ) );
				oldKind = GetPathKind( oldPath, this.options.NoDereference );
			} else if ( PathKind.Directory == newKind ) {
				if ( PathKind.StandardInput == oldKind ) {
					throw new DiffUsageException( "standard input cannot be compared to a directory" );
				}
				newPath = Path.Combine( newPath, GetFileNameRequired( oldPath ) );
				newKind = GetPathKind( newPath, this.options.NoDereference );
			}
			return await this.CompareLeafAsync( oldPath, oldKind, newPath, newKind ).ConfigureAwait( false );
		} catch ( DiffUsageException ) {
			throw;
		} catch ( OperationCanceledException ) {
			throw;
		} catch ( Exception exception ) when ( Command.IsOperationalException( exception ) ) {
			await this.context.Diagnostics.ErrorAsync( exception.Message, CancellationToken.None ).ConfigureAwait( false );
			return ComparisonStatus.Trouble;
		}
	}

	private async Task<ComparisonStatus> CompareDirectoriesAsync(
		string oldDirectory,
		string newDirectory,
		bool oldExists,
		bool newExists
	) {
		var comparer = this.options.IgnoreFileNameCase
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal;
		var oldEntries = oldExists ? EnumerateDirectory( oldDirectory, comparer ) : new Dictionary<string, string>( comparer );
		var newEntries = newExists ? EnumerateDirectory( newDirectory, comparer ) : new Dictionary<string, string>( comparer );
		var names = oldEntries.Keys
			.Concat( newEntries.Keys )
			.Distinct( comparer )
			.Where( this.ShouldIncludeDirectoryEntry )
			.OrderBy( name => name, comparer )
			.ToArray();
		var status = ComparisonStatus.Equal;
		foreach ( var name in names ) {
			this.context.CancellationToken.ThrowIfCancellationRequested();
			var hasOld = oldEntries.TryGetValue( name, out var oldPath );
			var hasNew = newEntries.TryGetValue( name, out var newPath );
			PathKind oldKind;
			PathKind newKind;
			if ( !hasOld || !hasNew ) {
				if ( this.options.NewFile || ( this.options.UnidirectionalNewFile && !hasOld ) ) {
					oldPath ??= Path.Combine( oldDirectory, name );
					newPath ??= Path.Combine( newDirectory, name );
					oldKind = hasOld ? GetPathKind( oldPath, this.options.NoDereference ) : PathKind.Missing;
					newKind = hasNew ? GetPathKind( newPath, this.options.NoDereference ) : PathKind.Missing;
					ComparisonStatus pairStatus;
					if ( ( PathKind.Directory == oldKind && PathKind.Missing == newKind )
						|| ( PathKind.Missing == oldKind && PathKind.Directory == newKind ) ) {
						if ( this.options.Recursive ) {
							pairStatus = await this.CompareDirectoriesAsync(
								oldPath,
								newPath,
								PathKind.Directory == oldKind,
								PathKind.Directory == newKind
							).ConfigureAwait( false );
						} else {
							await this.context.StandardOutput.WriteLineAsync(
								$"Common subdirectories: {oldPath} and {newPath}".AsMemory(),
								this.context.CancellationToken
							).ConfigureAwait( false );
							pairStatus = ComparisonStatus.Equal;
						}
					} else {
						pairStatus = await this.CompareLeafAsync( oldPath, oldKind, newPath, newKind ).ConfigureAwait( false );
					}
					status = Combine( status, pairStatus );
				} else {
					var containing = hasOld ? oldDirectory : newDirectory;
					await this.context.StandardOutput.WriteLineAsync(
						$"Only in {containing}: {name}".AsMemory(),
						this.context.CancellationToken
					).ConfigureAwait( false );
					status = Combine( status, ComparisonStatus.Different );
				}
				continue;
			}
			oldKind = GetPathKind( oldPath!, this.options.NoDereference );
			newKind = GetPathKind( newPath!, this.options.NoDereference );
			if ( PathKind.Directory == oldKind && PathKind.Directory == newKind ) {
				if ( this.options.Recursive ) {
					status = Combine( status, await this.CompareDirectoriesAsync( oldPath!, newPath!, oldExists: true, newExists: true ).ConfigureAwait( false ) );
				} else {
					await this.context.StandardOutput.WriteLineAsync(
						$"Common subdirectories: {oldPath} and {newPath}".AsMemory(),
						this.context.CancellationToken
					).ConfigureAwait( false );
				}
				continue;
			}
			if ( PathKind.Directory == oldKind && PathKind.Missing == newKind && this.options.NewFile ) {
				status = Combine( status, await this.CompareDirectoriesAsync( oldPath!, newPath!, oldExists: true, newExists: false ).ConfigureAwait( false ) );
				continue;
			}
			if ( PathKind.Missing == oldKind && PathKind.Directory == newKind && ( this.options.NewFile || this.options.UnidirectionalNewFile ) ) {
				status = Combine( status, await this.CompareDirectoriesAsync( oldPath!, newPath!, oldExists: false, newExists: true ).ConfigureAwait( false ) );
				continue;
			}
			if ( oldKind != newKind && ( PathKind.Directory == oldKind || PathKind.Directory == newKind ) ) {
				await this.context.StandardOutput.WriteLineAsync(
					$"File {oldPath} is a {( PathKind.Directory == oldKind ? "directory" : "regular file" )} while file {newPath} is a {( PathKind.Directory == newKind ? "directory" : "regular file" )}".AsMemory(),
					this.context.CancellationToken
				).ConfigureAwait( false );
				status = Combine( status, ComparisonStatus.Different );
				continue;
			}
			status = Combine( status, await this.CompareLeafAsync( oldPath!, oldKind, newPath!, newKind ).ConfigureAwait( false ) );
		}
		return status;
	}

	private async Task<ComparisonStatus> CompareLeafAsync(
		string oldPath,
		PathKind oldKind,
		string newPath,
		PathKind newKind
	) {
		if ( PathKind.Missing == oldKind && !this.options.NewFile && !this.options.UnidirectionalNewFile ) {
			throw new FileNotFoundException( $"{oldPath}: No such file or directory", oldPath );
		}
		if ( PathKind.Missing == newKind && !this.options.NewFile ) {
			throw new FileNotFoundException( $"{newPath}: No such file or directory", newPath );
		}
		if ( PathKind.Directory == oldKind || PathKind.Directory == newKind ) {
			throw new IOException( "cannot compare a directory with an absent file" );
		}
		var oldInput = await this.LoadInputAsync( oldPath, oldKind, labelIndex: 0 ).ConfigureAwait( false );
		var newInput = await this.LoadInputAsync( newPath, newKind, labelIndex: 1 ).ConfigureAwait( false );
		if ( PathKind.SymbolicLink == oldKind || PathKind.SymbolicLink == newKind ) {
			if ( PathKind.SymbolicLink == oldKind && PathKind.SymbolicLink == newKind && oldInput.Document.Bytes.Span.SequenceEqual( newInput.Document.Bytes.Span ) ) {
				await this.writer.WriteIdenticalAsync( oldInput, newInput ).ConfigureAwait( false );
				return ComparisonStatus.Equal;
			}
			var message = PathKind.SymbolicLink == oldKind && PathKind.SymbolicLink == newKind
				? $"Symbolic links {oldInput.HeaderName} and {newInput.HeaderName} differ"
				: $"File {oldInput.HeaderName} is a {DescribeKind( oldKind )} while file {newInput.HeaderName} is a {DescribeKind( newKind )}";
			await this.context.StandardOutput.WriteLineAsync( message.AsMemory(), this.context.CancellationToken ).ConfigureAwait( false );
			return ComparisonStatus.Different;
		}
		var binary = !this.options.TreatAsText && ( oldInput.Document.ContainsNullByte || newInput.Document.ContainsNullByte );
		var edInputTrouble = !binary
			&& await this.writer.WriteEdIncompleteWarningsAsync( oldInput, newInput ).ConfigureAwait( false );
		if ( oldInput.Document.Bytes.Span.SequenceEqual( newInput.Document.Bytes.Span ) ) {
			if ( !binary && EmitsUnchangedContent( this.options.OutputStyle ) ) {
				var identicalScript = LineDiffEngine.Compare(
					oldInput.Document.Lines,
					newInput.Document.Lines,
					this.options.ComparisonPolicy,
					this.context.CancellationToken
				);
				await this.writer.WriteAsync( oldInput, newInput, identicalScript ).ConfigureAwait( false );
			}
			await this.writer.WriteIdenticalAsync( oldInput, newInput ).ConfigureAwait( false );
			return edInputTrouble ? ComparisonStatus.Trouble : ComparisonStatus.Equal;
		}
		if ( binary ) {
			if ( DiffOutputStyle.Brief == this.options.OutputStyle ) {
				await this.writer.WriteAsync( oldInput, newInput, EmptyDifferentScript() ).ConfigureAwait( false );
			} else {
				await this.writer.WriteBinaryDifferenceAsync( oldInput, newInput ).ConfigureAwait( false );
			}
			return ComparisonStatus.Different;
		}
		var script = LineDiffEngine.Compare(
			oldInput.Document.Lines,
			newInput.Document.Lines,
			this.options.ComparisonPolicy,
			this.context.CancellationToken
		);
		script = this.writer.ApplyDifferenceFilters( script );
		if ( !script.HasDifferences ) {
			if ( EmitsUnchangedContent( this.options.OutputStyle ) ) {
				await this.writer.WriteAsync( oldInput, newInput, script ).ConfigureAwait( false );
			}
			await this.writer.WriteIdenticalAsync( oldInput, newInput ).ConfigureAwait( false );
			return edInputTrouble ? ComparisonStatus.Trouble : ComparisonStatus.Equal;
		}
		await this.writer.WriteAsync( oldInput, newInput, script ).ConfigureAwait( false );
		return edInputTrouble ? ComparisonStatus.Trouble : ComparisonStatus.Different;
	}

	private async Task<DiffInput> LoadInputAsync( string path, PathKind kind, int labelIndex ) {
		if ( PathKind.Missing == kind ) {
			return new DiffInput(
				path,
				path,
				GetHeaderName( path, labelIndex ),
				DateTimeOffset.UnixEpoch,
				ComparisonDocumentReader.Decode( ReadOnlySpan<byte>.Empty ),
				isMissing: true
			);
		}
		if ( PathKind.SymbolicLink == kind ) {
			var attributes = File.GetAttributes( path );
			var target = attributes.HasFlag( FileAttributes.Directory )
				? new DirectoryInfo( path ).LinkTarget
				: new FileInfo( path ).LinkTarget;
			target ??= string.Empty;
			var bytes = System.Text.Encoding.UTF8.GetBytes( target );
			return new DiffInput(
				path,
				path,
				GetHeaderName( path, labelIndex ),
				GetModifiedTime( path ),
				ComparisonDocumentReader.Decode( bytes )
			);
		}
		ComparisonDocument document;
		if ( PathKind.StandardInput == kind ) {
			this.standardInputDocument ??= await ReadStandardInputAsync( this.context ).ConfigureAwait( false );
			document = this.standardInputDocument;
		} else {
			var comparisonInput = ComparisonInput.Create( path );
			await using var source = comparisonInput.OpenBinary( this.context );
			document = await ComparisonDocumentReader.ReadAsync(
				source.BinaryStream ?? throw new InvalidOperationException( "A binary input stream was not supplied." ),
				this.context.CancellationToken
			).ConfigureAwait( false );
		}
		return new DiffInput(
			path,
			PathKind.StandardInput == kind ? "standard input" : path,
			GetHeaderName( path, labelIndex ),
			PathKind.StandardInput == kind ? null : GetModifiedTime( path ),
			document
		);
	}

	private static async Task<ComparisonDocument> ReadStandardInputAsync( CommandContext context ) {
		var stream = context.StandardInputStream ?? throw new InvalidOperationException( "A binary standard-input stream was not supplied." );
		return await ComparisonDocumentReader.ReadAsync( stream, context.CancellationToken ).ConfigureAwait( false );
	}

	private string GetHeaderName( string path, int labelIndex ) {
		return labelIndex < this.options.Labels.Count
			? this.options.Labels[labelIndex]
			: "-" == path ? "-" : path;
	}

	private bool ShouldIncludeDirectoryEntry( string name ) {
		if ( null != this.options.StartingFile ) {
			var comparison = this.options.IgnoreFileNameCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			if ( 0 > string.Compare( name, this.options.StartingFile, comparison ) ) {
				return false;
			}
		}
		foreach ( var pattern in this.options.ExcludePatterns ) {
			if ( FileSystemName.MatchesSimpleExpression( pattern, name, this.options.IgnoreFileNameCase ) ) {
				return false;
			}
		}
		return true;
	}

	private static Dictionary<string, string> EnumerateDirectory( string directory, StringComparer comparer ) {
		var result = new Dictionary<string, string>( comparer );
		foreach ( var path in Directory.EnumerateFileSystemEntries( directory ) ) {
			var name = GetFileNameRequired( path );
			if ( !result.TryAdd( name, path ) ) {
				throw new IOException( $"{directory}: duplicate entry name under the selected case policy: {name}" );
			}
		}
		return result;
	}

	private static string GetFileNameRequired( string path ) {
		var name = Path.GetFileName( path );
		if ( string.IsNullOrEmpty( name ) ) {
			throw new IOException( $"cannot determine a file name for '{path}'" );
		}
		return name;
	}

	private static PathKind GetPathKind( string path, bool noDereference ) {
		if ( "-" == path ) {
			return PathKind.StandardInput;
		}
		if ( noDereference ) {
			try {
				var attributes = File.GetAttributes( path );
				if ( attributes.HasFlag( FileAttributes.ReparsePoint ) ) {
					return PathKind.SymbolicLink;
				}
				return attributes.HasFlag( FileAttributes.Directory ) ? PathKind.Directory : PathKind.File;
			} catch ( FileNotFoundException ) {
				return PathKind.Missing;
			} catch ( DirectoryNotFoundException ) {
				return PathKind.Missing;
			}
		}
		if ( Directory.Exists( path ) ) {
			return PathKind.Directory;
		}
		if ( File.Exists( path ) ) {
			return PathKind.File;
		}
		return PathKind.Missing;
	}

	private static DateTimeOffset? GetModifiedTime( string path ) {
		try {
			return new DateTimeOffset( File.GetLastWriteTimeUtc( path ), TimeSpan.Zero );
		} catch ( IOException ) {
			return null;
		} catch ( UnauthorizedAccessException ) {
			return null;
		}
	}

	private static string DescribeKind( PathKind kind ) {
		return PathKind.SymbolicLink == kind ? "symbolic link" : "regular file";
	}

	private static bool EmitsUnchangedContent( DiffOutputStyle style ) {
		return style is DiffOutputStyle.SideBySide or DiffOutputStyle.IfDef;
	}

	private static EditScript EmptyDifferentScript() {
		var line = new ComparisonLine( string.Empty, true );
		var delete = new EditOperation( EditOperationKind.Delete, 0, null, line );
		var insert = new EditOperation( EditOperationKind.Insert, null, 0, line );
		var operations = Array.AsReadOnly( new[] { delete, insert } );
		var block = new DifferenceBlock( 0, 1, 0, 1, operations );
		return new EditScript( operations, Array.AsReadOnly( new[] { block } ) );
	}

	private static ComparisonStatus Combine( ComparisonStatus first, ComparisonStatus second ) {
		return (ComparisonStatus)Math.Max( (int)first, (int)second );
	}
}
