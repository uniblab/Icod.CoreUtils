using Path = global::System.IO.Path;
namespace Icod.CoreUtils.Shared.DirectoryListing;

using System.Globalization;
using System.Text;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.Terminal;

/// <summary>Hosts <c>ls</c>, <c>dir</c>, and <c>vdir</c> over one reusable listing engine.</summary>
public static class DirectoryListingCommand {
	/// <summary>Runs one directory-listing profile.</summary>
	/// <param name="profile">The executable profile.</param>
	/// <param name="commandName">The diagnostic command name.</param>
	/// <param name="arguments">Command-line arguments.</param>
	/// <param name="standardInput">Standard input.</param>
	/// <param name="standardOutput">Standard output.</param>
	/// <param name="standardError">Standard error.</param>
	/// <param name="metadataProvider">Optional authoritative metadata provider.</param>
	/// <param name="presentationProvider">Optional terminal presentation provider.</param>
	/// <param name="environmentProvider">Optional environment provider.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The process exit status.</returns>
	public static async Task<int> RunAsync(
		DirectoryListingProfile profile,
		string commandName,
		IReadOnlyList<string> arguments,
		TextReader standardInput,
		TextWriter standardOutput,
		TextWriter standardError,
		IFileSystemMetadataProvider? metadataProvider = null,
		TerminalPresentationProvider? presentationProvider = null,
		IEnvironmentVariableProvider? environmentProvider = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( commandName );
		ArgumentNullException.ThrowIfNull( arguments );
		ArgumentNullException.ThrowIfNull( standardInput );
		ArgumentNullException.ThrowIfNull( standardOutput );
		ArgumentNullException.ThrowIfNull( standardError );
		metadataProvider ??= SystemFileSystemMetadataProvider.Instance;
		presentationProvider ??= TerminalPresentationProvider.CreateSystem();
		environmentProvider ??= SystemEnvironmentVariableProvider.Instance;
		var presentation = presentationProvider.Observe( TerminalStreamKind.StandardOutput );
		DirectoryListingOptions options;
		try {
			options = DirectoryListingOptionParser.Parse( profile, arguments, presentation );
			if ( !options.TimeStyleSpecified ) {
				var environmentTimeStyle = environmentProvider.GetValue( "TIME_STYLE" );
				if ( !string.IsNullOrWhiteSpace( environmentTimeStyle ) ) {
					options.TimeStyle = DirectoryListingOptionParser.ValidateTimeStyle( environmentTimeStyle );
				}
			}
			if ( options.QuotingStyle is null ) {
				var environmentQuotingStyle = environmentProvider.GetValue( "QUOTING_STYLE" );
				if ( !string.IsNullOrWhiteSpace( environmentQuotingStyle ) ) {
					if ( !FileNamePresentationPolicy.TryParseQuotingStyle( environmentQuotingStyle, out var environmentStyle ) ) {
						throw new DirectoryListingUsageException( $"invalid argument '{environmentQuotingStyle}' for 'QUOTING_STYLE'" );
					}
					options.QuotingStyle = environmentStyle;
				}
			}
		} catch ( DirectoryListingUsageException exception ) {
			await standardError.WriteLineAsync( $"{commandName}: {exception.Message}" ).ConfigureAwait( false );
			await standardError.WriteLineAsync( $"Try '{commandName} --help' for more information." ).ConfigureAwait( false );
			return 2;
		}
		if ( options.ShowHelp ) {
			await PrintHelpAsync( commandName, standardOutput ).ConfigureAwait( false );
			return 0;
		}
		if ( options.ShowVersion ) {
			await standardOutput.WriteLineAsync( $"{commandName} (Icod.CoreUtils) 1.0" ).ConfigureAwait( false );
			return 0;
		}
		var quotingPolicy = FileNamePresentationPolicy.ResolveDefault(
			presentation,
			options.QuotingStyle,
			options.ControlCharacters
		);
		LsColors colors;
		try {
			colors = LsColors.Parse( environmentProvider.GetValue( "LS_COLORS" ) );
		} catch ( FormatException exception ) {
			await standardError.WriteLineAsync( $"{commandName}: invalid LS_COLORS value: {exception.Message}" ).ConfigureAwait( false );
			colors = LsColors.Empty;
		}
		var useColor = TerminalColorPolicy.Resolve( options.ColorMode, presentation ).UseColor;
		var engine = new DirectoryListingEngine(
			commandName,
			options,
			metadataProvider,
			standardOutput,
			standardError,
			quotingPolicy,
			colors,
			useColor,
			options.Width ?? presentation.Width
		);
		return await engine.ExecuteAsync( cancellationToken ).ConfigureAwait( false );
	}

	private static async Task PrintHelpAsync( string commandName, TextWriter output ) {
		await output.WriteLineAsync( $"Usage: {commandName} [OPTION]... [FILE]..." ).ConfigureAwait( false );
		await output.WriteLineAsync( "List information about FILEs (the current directory by default)." ).ConfigureAwait( false );
		await output.WriteLineAsync().ConfigureAwait( false );
		await output.WriteLineAsync( "  -a, --all                  do not ignore entries starting with ." ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -A, --almost-all           do not list implied . and .." ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -B, --ignore-backups       do not list entries ending with ~" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -d, --directory            list directories themselves" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -l                         use a long listing format" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -h, --human-readable       print human-readable sizes" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --si                   use powers of 1000" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -i, --inode                print inode numbers" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -s, --size                 print allocated sizes" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -R, --recursive            list subdirectories recursively" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -r, --reverse              reverse the sort order" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -S                         sort by file size" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -t                         sort by selected time" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -X                         sort by extension" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -v                         natural version sort" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -1, -C, -x, -m             select one, vertical, horizontal, or comma layout" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -F, --classify             append file classification indicators" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --color[=WHEN]         colorize; WHEN is always, auto, or never" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --quoting-style=WORD   select GNU filename quoting" ).ConfigureAwait( false );
		await output.WriteLineAsync( "  -H, -L, -P                 command-line, all, or no dereference" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --dereference-command-line-symlink-to-dir" ).ConfigureAwait( false );
		await output.WriteLineAsync( "                             follow command-line links to directories" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --time=WORD            select modification, access, change, or birth time" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --time-style=STYLE     locale, iso, long-iso, full-iso, or +FORMAT" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --help                 display this help and exit" ).ConfigureAwait( false );
		await output.WriteLineAsync( "      --version              output version information and exit" ).ConfigureAwait( false );
	}
}

/// <summary>Renders one parsed directory-listing invocation using authoritative filesystem metadata.</summary>
internal sealed class DirectoryListingEngine {
	private readonly string commandName;
	private readonly DirectoryListingOptions options;
	private readonly IFileSystemMetadataProvider metadataProvider;
	private readonly TextWriter output;
	private readonly TextWriter error;
	private readonly FileNamePresentationPolicy quotingPolicy;
	private readonly LsColors colors;
	private readonly bool useColor;
	private readonly int outputWidth;
	private readonly HashSet<string> activeDirectories = new( StringComparer.Ordinal );
	private bool wroteAnyGroup;
	private int exitCode;

	/// <summary>Initializes one listing engine.</summary>
	/// <param name="commandName">The executable name used in diagnostics.</param>
	/// <param name="options">Parsed invocation options.</param>
	/// <param name="metadataProvider">Authoritative filesystem metadata provider.</param>
	/// <param name="output">Standard output.</param>
	/// <param name="error">Standard error.</param>
	/// <param name="quotingPolicy">Resolved filename-presentation policy.</param>
	/// <param name="colors">Parsed color database.</param>
	/// <param name="useColor">Whether terminal color is enabled.</param>
	/// <param name="outputWidth">Resolved output width.</param>
	public DirectoryListingEngine(
		string commandName,
		DirectoryListingOptions options,
		IFileSystemMetadataProvider metadataProvider,
		TextWriter output,
		TextWriter error,
		FileNamePresentationPolicy quotingPolicy,
		LsColors colors,
		bool useColor,
		int outputWidth
	) {
		this.commandName = commandName;
		this.options = options;
		this.metadataProvider = metadataProvider;
		this.output = output;
		this.error = error;
		this.quotingPolicy = quotingPolicy;
		this.colors = colors;
		this.useColor = useColor;
		this.outputWidth = Math.Max( 1, outputWidth );
	}

	/// <summary>Executes the listing.</summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The process exit status.</returns>
	public async Task<int> ExecuteAsync( CancellationToken cancellationToken ) {
		var files = new List<ListingEntry>();
		var directories = new List<ListingEntry>();
		foreach ( var operand in this.options.Operands ) {
			cancellationToken.ThrowIfCancellationRequested();
			var follow = this.ShouldFollowOperand( operand );
			var entry = await this.TryObserveAsync( operand, operand, true, follow, cancellationToken ).ConfigureAwait( false );
			if ( entry is null ) {
				continue;
			}
			if ( entry.IsDirectory && !this.options.ListDirectoriesThemselves ) {
				directories.Add( entry );
			} else {
				files.Add( entry );
			}
		}
		files = this.SortEntries( files );
		if ( 0 != files.Count ) {
			await this.WriteEntriesAsync( files ).ConfigureAwait( false );
			this.wroteAnyGroup = true;
		}
		var showHeaders = this.options.Recursive || ( directories.Count > 1 ) || ( 0 != files.Count );
		foreach ( var directory in this.SortEntries( directories ) ) {
			await this.WriteDirectoryAsync( directory, showHeaders, cancellationToken ).ConfigureAwait( false );
		}
		return this.exitCode;
	}

	private bool ShouldFollowOperand( string path ) {
		return this.options.DereferenceMode switch {
			DirectoryListingDereferenceMode.Always => true,
			DirectoryListingDereferenceMode.CommandLine => true,
			DirectoryListingDereferenceMode.CommandLineDirectory => Directory.Exists( path ),
			DirectoryListingDereferenceMode.Never => false,
			DirectoryListingDereferenceMode.Default => this.UsesDefaultDirectoryDereference() && Directory.Exists( path ),
			_ => false
		};
	}

	private bool UsesDefaultDirectoryDereference() {
		return !this.options.ListDirectoriesThemselves
			&& DirectoryListingFormat.Long != this.options.Format
			&& DirectoryListingIndicatorStyle.Classify != this.options.IndicatorStyle;
	}

	private async Task WriteDirectoryAsync(
		ListingEntry directory,
		bool showHeader,
		CancellationToken cancellationToken
	) {
		var identity = GetIdentityKey( directory );
		if ( !this.activeDirectories.Add( identity ) ) {
			await this.error.WriteLineAsync( $"{this.commandName}: {QuoteDiagnostic( directory.Path )}: recursive directory loop" ).ConfigureAwait( false );
			this.exitCode = 1;
			return;
		}
		try {
			if ( this.wroteAnyGroup ) {
				await this.output.WriteLineAsync().ConfigureAwait( false );
			}
			if ( showHeader ) {
				await this.output.WriteLineAsync( string.Concat( this.PresentName( directory, directory.DisplayName, false ), ":" ) ).ConfigureAwait( false );
			}
			var entries = await this.EnumerateDirectoryAsync( directory.Path, cancellationToken ).ConfigureAwait( false );
			if ( entries is null ) {
				this.wroteAnyGroup = true;
				return;
			}
			entries = this.SortEntries( entries );
			if ( DirectoryListingFormat.Long == this.options.Format ) {
				var totalBytes = SumAllocatedBytes( entries );
				var total = totalBytes.HasValue ? this.FormatBlockCount( totalBytes.Value ) : "?";
				await this.output.WriteLineAsync( string.Concat( "total ", total ) ).ConfigureAwait( false );
			}
			await this.WriteEntriesAsync( entries ).ConfigureAwait( false );
			this.wroteAnyGroup = true;
			if ( !this.options.Recursive ) {
				return;
			}
			foreach ( var child in entries ) {
				if ( !child.IsDirectory || child.Name is "." or ".." ) {
					continue;
				}
				if ( child.IsPathIndirection && DirectoryListingDereferenceMode.Always != this.options.DereferenceMode ) {
					continue;
				}
				await this.WriteDirectoryAsync( child with { IsOperand = true }, true, cancellationToken ).ConfigureAwait( false );
			}
		} finally {
			this.activeDirectories.Remove( identity );
		}
	}

	private async Task<List<ListingEntry>?> EnumerateDirectoryAsync(
		string directory,
		CancellationToken cancellationToken
	) {
		var entries = new List<ListingEntry>();
		try {
			if ( this.options.ShowAll ) {
				var dot = await this.TryObserveAsync( directory, ".", false, this.options.DereferenceMode == DirectoryListingDereferenceMode.Always, cancellationToken ).ConfigureAwait( false );
				if ( dot is not null ) {
					entries.Add( dot );
				}
				var parentPath = System.IO.Path.GetFullPath( System.IO.Path.Combine( directory, ".." ) );
				var dotDot = await this.TryObserveAsync( parentPath, "..", false, this.options.DereferenceMode == DirectoryListingDereferenceMode.Always, cancellationToken ).ConfigureAwait( false );
				if ( dotDot is not null ) {
					entries.Add( dotDot );
				}
			}
			var enumeration = new EnumerationOptions {
				AttributesToSkip = 0,
				IgnoreInaccessible = false,
				RecurseSubdirectories = false,
				ReturnSpecialDirectories = false
			};
			foreach ( var path in Directory.EnumerateFileSystemEntries( directory, "*", enumeration ) ) {
				cancellationToken.ThrowIfCancellationRequested();
				var name = System.IO.Path.GetFileName( path );
				if ( this.ShouldIgnore( path, name ) ) {
					continue;
				}
				var entry = await this.TryObserveAsync(
					path,
					name,
					false,
					DirectoryListingDereferenceMode.Always == this.options.DereferenceMode,
					cancellationToken
				).ConfigureAwait( false );
				if ( entry is not null ) {
					entries.Add( entry );
				}
			}
			return entries;
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException ) {
			await this.error.WriteLineAsync( $"{this.commandName}: cannot open directory {QuoteDiagnostic( directory )}: {exception.Message}" ).ConfigureAwait( false );
			this.exitCode = 1;
			return null;
		}
	}

	private bool ShouldIgnore( string path, string name ) {
		if ( !this.options.ShowAll && !this.options.AlmostAll ) {
			if ( name.StartsWith( ".", StringComparison.Ordinal ) ) {
				return true;
			}
			try {
				if ( File.GetAttributes( path ).HasFlag( FileAttributes.Hidden ) ) {
					return true;
				}
			} catch ( IOException ) {
			}
			if ( this.options.HidePatterns.Any( pattern => MatchesListingPattern( name, pattern ) ) ) {
				return true;
			}
		}
		if ( this.options.IgnoreBackups && name.EndsWith( '~' ) ) {
			return true;
		}
		return this.options.IgnorePatterns.Any( pattern => MatchesListingPattern( name, pattern ) );
	}

	private static bool MatchesListingPattern( string name, string pattern ) {
		if ( name.StartsWith( ".", StringComparison.Ordinal ) && !pattern.StartsWith( ".", StringComparison.Ordinal ) ) {
			return false;
		}
		return GlobMatcher.IsMatch( name, pattern );
	}

	private async Task<ListingEntry?> TryObserveAsync(
		string path,
		string displayName,
		bool isOperand,
		bool follow,
		CancellationToken cancellationToken
	) {
		try {
			var metadata = await this.metadataProvider.GetMetadataAsync( path, follow, cancellationToken ).ConfigureAwait( false );
			var kind = metadata.Kind.ToString();
			var isDirectory = kind.Contains( "Directory", StringComparison.OrdinalIgnoreCase );
			if ( metadata.IsPathIndirection && !metadata.WasDereferenced ) {
				isDirectory = false;
			}
			if ( !isDirectory ) {
				try {
					isDirectory = File.GetAttributes( path ).HasFlag( FileAttributes.Directory ) && ( follow || !metadata.IsPathIndirection );
				} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException ) {
				}
			}
			return new ListingEntry( path, displayName, isOperand, metadata, isDirectory );
		} catch ( Exception exception ) when ( exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException ) {
			await this.error.WriteLineAsync( $"{this.commandName}: cannot access {QuoteDiagnostic( path )}: {exception.Message}" ).ConfigureAwait( false );
			this.exitCode = 1;
			return null;
		}
	}

	private List<ListingEntry> SortEntries( IEnumerable<ListingEntry> source ) {
		var entries = source.ToList();
		if ( DirectoryListingSort.None == this.options.Sort && !this.options.GroupDirectoriesFirst ) {
			if ( this.options.Reverse ) {
				entries.Reverse();
			}
			return entries;
		}
		IOrderedEnumerable<ListingEntry> ordered;
		if ( this.options.GroupDirectoriesFirst ) {
			ordered = entries.OrderBy( entry => entry.IsDirectory ? 0 : 1 );
		} else {
			ordered = entries.OrderBy( _ => 0 );
		}
		ordered = this.options.Sort switch {
			DirectoryListingSort.None => ordered.ThenBy( _ => 0 ),
			DirectoryListingSort.Size => ordered.ThenByDescending( GetLogicalSize ).ThenBy( entry => entry, ListingNameComparer.Instance ),
			DirectoryListingSort.Time => ordered.ThenByDescending( this.GetSelectedTime ).ThenBy( entry => entry, ListingNameComparer.Instance ),
			DirectoryListingSort.Extension => ordered.ThenBy( entry => System.IO.Path.GetExtension( entry.Name ), StringComparer.Create( CultureInfo.CurrentCulture, false ) ).ThenBy( entry => entry, ListingNameComparer.Instance ),
			DirectoryListingSort.Version => ordered.ThenBy( entry => entry, VersionListingComparer.Instance ),
			DirectoryListingSort.Width => ordered.ThenByDescending( entry => DisplayWidth.Measure( entry.Name, this.options.TabSize ) ).ThenBy( entry => entry, ListingNameComparer.Instance ),
			_ => ordered.ThenBy( entry => entry, ListingNameComparer.Instance )
		};
		var result = ordered.ToList();
		if ( this.options.Reverse ) {
			if ( this.options.GroupDirectoriesFirst ) {
				var directories = result.Where( entry => entry.IsDirectory ).Reverse();
				var files = result.Where( entry => !entry.IsDirectory ).Reverse();
				result = directories.Concat( files ).ToList();
			} else {
				result.Reverse();
			}
		}
		return result;
	}

	private DateTimeOffset GetSelectedTime( ListingEntry entry ) {
		var value = this.options.TimeField switch {
			DirectoryListingTimeField.Access => entry.Metadata.AccessTime,
			DirectoryListingTimeField.Change => entry.Metadata.ChangeTime,
			DirectoryListingTimeField.Birth => entry.Metadata.BirthTime,
			_ => entry.Metadata.ModificationTime
		};
		return value.IsAvailable ? value.GetRequiredValue() : DateTimeOffset.MinValue;
	}

	private async Task WriteEntriesAsync( List<ListingEntry> entries ) {
		if ( 0 == entries.Count ) {
			return;
		}
		if ( DirectoryListingFormat.Long == this.options.Format ) {
			await this.WriteLongEntriesAsync( entries ).ConfigureAwait( false );
			return;
		}
		var inodeWidth = this.options.ShowInode ? entries.Max( entry => FormatOptional( entry.Metadata.InodeNumber ).Length ) : 0;
		var blockWidth = this.options.ShowBlocks ? entries.Max( entry => this.FormatAllocatedBlocks( entry ).Length ) : 0;
		var rendered = entries.Select( entry => {
			var builder = new StringBuilder();
			if ( this.options.ShowInode ) {
				builder.Append( FormatOptional( entry.Metadata.InodeNumber ).PadLeft( inodeWidth ) );
				builder.Append( ' ' );
			}
			if ( this.options.ShowBlocks ) {
				builder.Append( this.FormatAllocatedBlocks( entry ).PadLeft( blockWidth ) );
				builder.Append( ' ' );
			}
			builder.Append( this.PresentName( entry, entry.DisplayName, true ) );
			return builder.ToString();
		} ).ToList();
		switch ( this.options.Format ) {
			case DirectoryListingFormat.Columns:
				await this.WriteColumnsAsync( rendered, false ).ConfigureAwait( false );
				break;
			case DirectoryListingFormat.HorizontalColumns:
				await this.WriteColumnsAsync( rendered, true ).ConfigureAwait( false );
				break;
			case DirectoryListingFormat.Commas:
				await this.WriteCommasAsync( rendered ).ConfigureAwait( false );
				break;
			default:
				foreach ( var value in rendered ) {
					await this.output.WriteLineAsync( value ).ConfigureAwait( false );
				}
				break;
		}
	}

	private async Task WriteLongEntriesAsync( List<ListingEntry> entries ) {
		var rows = entries.Select( this.BuildLongRow ).ToList();
		var inodeWidth = this.options.ShowInode ? rows.Max( row => row.Inode.Length ) : 0;
		var blockWidth = this.options.ShowBlocks ? rows.Max( row => row.Blocks.Length ) : 0;
		var linksWidth = rows.Max( row => row.Links.Length );
		var ownerWidth = this.options.SuppressOwner ? 0 : rows.Max( row => row.Owner.Length );
		var groupWidth = this.options.SuppressGroup ? 0 : rows.Max( row => row.Group.Length );
		var authorWidth = this.options.ShowAuthor ? rows.Max( row => row.Author.Length ) : 0;
		var sizeWidth = rows.Max( row => row.Size.Length );
		foreach ( var row in rows ) {
			var builder = new StringBuilder();
			if ( this.options.ShowInode ) {
				builder.Append( row.Inode.PadLeft( inodeWidth ) );
				builder.Append( ' ' );
			}
			if ( this.options.ShowBlocks ) {
				builder.Append( row.Blocks.PadLeft( blockWidth ) );
				builder.Append( ' ' );
			}
			builder.Append( row.Mode );
			builder.Append( ' ' );
			builder.Append( row.Links.PadLeft( linksWidth ) );
			if ( !this.options.SuppressOwner ) {
				builder.Append( ' ' );
				builder.Append( row.Owner.PadRight( ownerWidth ) );
			}
			if ( !this.options.SuppressGroup ) {
				builder.Append( ' ' );
				builder.Append( row.Group.PadRight( groupWidth ) );
			}
			if ( this.options.ShowAuthor ) {
				builder.Append( ' ' );
				builder.Append( row.Author.PadRight( authorWidth ) );
			}
			builder.Append( ' ' );
			builder.Append( row.Size.PadLeft( sizeWidth ) );
			builder.Append( ' ' );
			builder.Append( row.Time );
			builder.Append( ' ' );
			builder.Append( row.Name );
			await this.output.WriteLineAsync( builder.ToString() ).ConfigureAwait( false );
		}
	}

	private LongListingRow BuildLongRow( ListingEntry entry ) {
		var owner = this.options.NumericIds
			? FormatOptional( entry.Metadata.UserId )
			: FormatOptional( entry.Metadata.OwnerName );
		var group = this.options.NumericIds
			? FormatOptional( entry.Metadata.GroupId )
			: FormatOptional( entry.Metadata.GroupName );
		var name = this.PresentName( entry, entry.DisplayName, true );
		if ( entry.IsPathIndirection && entry.Metadata.LinkTarget.IsAvailable ) {
			var target = entry.Metadata.LinkTarget.GetRequiredValue();
			name += " -> " + FileNamePresenter.Present( target, this.quotingPolicy );
		}
		return new LongListingRow(
			FormatOptional( entry.Metadata.InodeNumber ),
			this.FormatAllocatedBlocks( entry ),
			BuildMode( entry ),
			FormatOptional( entry.Metadata.LinkCount ),
			owner,
			group,
			owner,
			this.FormatLogicalSize( entry ),
			this.FormatTime( this.GetSelectedTime( entry ) ),
			name
		);
	}

	private async Task WriteColumnsAsync( IReadOnlyList<string> values, bool horizontal ) {
		var count = values.Count;
		var widths = values.Select( value => DisplayWidth.Measure( value, this.options.TabSize ) ).ToArray();
		var selectedColumns = 1;
		var selectedRows = count;
		int[] selectedWidths = { widths.Max() };
		for ( var columns = count; columns >= 1; columns-- ) {
			var rows = ( count + columns - 1 ) / columns;
			var columnWidths = new int[ columns ];
			for ( var index = 0; index < count; index++ ) {
				var column = horizontal ? index % columns : index / rows;
				if ( column < columns ) {
					columnWidths[ column ] = Math.Max( columnWidths[ column ], widths[ index ] );
				}
			}
			var required = columnWidths.Sum() + Math.Max( 0, columns - 1 ) * 2;
			if ( required <= this.outputWidth ) {
				selectedColumns = columns;
				selectedRows = rows;
				selectedWidths = columnWidths;
				break;
			}
		}
		for ( var row = 0; row < selectedRows; row++ ) {
			var builder = new StringBuilder();
			for ( var column = 0; column < selectedColumns; column++ ) {
				var index = horizontal ? row * selectedColumns + column : column * selectedRows + row;
				if ( index >= count ) {
					continue;
				}
				builder.Append( values[ index ] );
				var nextExists = Enumerable.Range( column + 1, selectedColumns - column - 1 )
					.Any( next => ( horizontal ? row * selectedColumns + next : next * selectedRows + row ) < count );
				if ( nextExists ) {
					builder.Append( ' ', Math.Max( 2, selectedWidths[ column ] - widths[ index ] + 2 ) );
				}
			}
			await this.output.WriteLineAsync( builder.ToString() ).ConfigureAwait( false );
		}
	}

	private async Task WriteCommasAsync( IReadOnlyList<string> values ) {
		var column = 0;
		for ( var index = 0; index < values.Count; index++ ) {
			var separator = index + 1 < values.Count ? ", " : string.Empty;
			var required = DisplayWidth.Measure( values[ index ], this.options.TabSize ) + separator.Length;
			if ( 0 != column && column + required > this.outputWidth ) {
				await this.output.WriteLineAsync().ConfigureAwait( false );
				column = 0;
			}
			await this.output.WriteAsync( string.Concat( values[ index ], separator ) ).ConfigureAwait( false );
			column += required;
		}
		await this.output.WriteLineAsync().ConfigureAwait( false );
	}

	private string PresentName( ListingEntry entry, string name, bool includeIndicator ) {
		var presented = FileNamePresenter.Present( name, this.quotingPolicy );
		if ( this.useColor ) {
			presented = this.colors.Apply( presented, this.colors.ResolveStyle( name, GetColorIndicator( entry ) ) );
		}
		return includeIndicator ? presented + this.GetClassificationIndicator( entry ) : presented;
	}

	private string GetClassificationIndicator( ListingEntry entry ) {
		if ( DirectoryListingIndicatorStyle.None == this.options.IndicatorStyle ) {
			return string.Empty;
		}
		if ( entry.IsDirectory ) {
			return "/";
		}
		if ( DirectoryListingIndicatorStyle.Slash == this.options.IndicatorStyle ) {
			return string.Empty;
		}
		if ( entry.IsPathIndirection ) {
			return "@";
		}
		var kind = entry.Metadata.Kind.ToString();
		if ( kind.Contains( "Fifo", StringComparison.OrdinalIgnoreCase ) || kind.Contains( "Pipe", StringComparison.OrdinalIgnoreCase ) ) {
			return "|";
		}
		if ( kind.Contains( "Socket", StringComparison.OrdinalIgnoreCase ) ) {
			return "=";
		}
		if ( kind.Contains( "Door", StringComparison.OrdinalIgnoreCase ) ) {
			return ">";
		}
		return DirectoryListingIndicatorStyle.Classify == this.options.IndicatorStyle && IsExecutable( entry ) ? "*" : string.Empty;
	}

	private static string GetColorIndicator( ListingEntry entry ) {
		if ( entry.IsPathIndirection ) {
			if ( entry.Metadata.LinkTarget.IsAvailable && !TargetExists( entry.Path, entry.Metadata.LinkTarget.GetRequiredValue() ) ) {
				return "or";
			}
			return "ln";
		}
		if ( entry.IsDirectory ) {
			var mode = GetMode( entry );
			var sticky = 0 != ( mode & 0x200U );
			var otherWritable = 0 != ( mode & 0x002U );
			if ( sticky && otherWritable ) return "tw";
			if ( otherWritable ) return "ow";
			if ( sticky ) return "st";
			return "di";
		}
		var kind = entry.Metadata.Kind.ToString();
		if ( kind.Contains( "Fifo", StringComparison.OrdinalIgnoreCase ) || kind.Contains( "Pipe", StringComparison.OrdinalIgnoreCase ) ) return "pi";
		if ( kind.Contains( "Socket", StringComparison.OrdinalIgnoreCase ) ) return "so";
		if ( kind.Contains( "Door", StringComparison.OrdinalIgnoreCase ) ) return "do";
		if ( kind.Contains( "Block", StringComparison.OrdinalIgnoreCase ) ) return "bd";
		if ( kind.Contains( "Character", StringComparison.OrdinalIgnoreCase ) ) return "cd";
		var nativeMode = GetMode( entry );
		if ( 0 != ( nativeMode & 0x800U ) ) return "su";
		if ( 0 != ( nativeMode & 0x400U ) ) return "sg";
		if ( entry.Metadata.LinkCount.IsAvailable && entry.Metadata.LinkCount.GetRequiredValue() > 1 ) return "mh";
		return IsExecutable( entry ) ? "ex" : "fi";
	}

	private static bool TargetExists( string path, string target ) {
		try {
			var resolved = System.IO.Path.IsPathRooted( target ) ? target : System.IO.Path.Combine( System.IO.Path.GetDirectoryName( path ) ?? string.Empty, target );
			return File.Exists( resolved ) || Directory.Exists( resolved );
		} catch ( Exception exception ) when ( exception is IOException or ArgumentException or NotSupportedException or System.Security.SecurityException ) {
			return false;
		}
	}

	private static bool IsExecutable( ListingEntry entry ) {
		var mode = GetMode( entry );
		if ( 0 != ( mode & 0x049U ) ) {
			return true;
		}
		if ( OperatingSystem.IsWindows() ) {
			var extension = System.IO.Path.GetExtension( entry.Name );
			return extension.Equals( ".exe", StringComparison.OrdinalIgnoreCase )
				|| extension.Equals( ".com", StringComparison.OrdinalIgnoreCase )
				|| extension.Equals( ".bat", StringComparison.OrdinalIgnoreCase )
				|| extension.Equals( ".cmd", StringComparison.OrdinalIgnoreCase );
		}
		return false;
	}

	private static uint GetMode( ListingEntry entry ) {
		return entry.Metadata.Mode.IsAvailable ? entry.Metadata.Mode.GetRequiredValue() : 0;
	}

	private static string BuildMode( ListingEntry entry ) {
		var kind = entry.Metadata.Kind.ToString();
		var type = entry.IsPathIndirection ? 'l'
			: entry.IsDirectory ? 'd'
			: kind.Contains( "Fifo", StringComparison.OrdinalIgnoreCase ) || kind.Contains( "Pipe", StringComparison.OrdinalIgnoreCase ) ? 'p'
			: kind.Contains( "Socket", StringComparison.OrdinalIgnoreCase ) ? 's'
			: kind.Contains( "Block", StringComparison.OrdinalIgnoreCase ) ? 'b'
			: kind.Contains( "Character", StringComparison.OrdinalIgnoreCase ) ? 'c'
			: '?';
		if ( '?' == type && kind.Contains( "File", StringComparison.OrdinalIgnoreCase ) ) {
			type = '-';
		}
		var mode = GetMode( entry );
		if ( !entry.Metadata.Mode.IsAvailable ) {
			return string.Concat( type, "?????????" );
		}
		Span<char> permissions = stackalloc char[ 9 ];
		var bits = new uint[] { 0x100, 0x080, 0x040, 0x020, 0x010, 0x008, 0x004, 0x002, 0x001 };
		var letters = "rwxrwxrwx";
		for ( var index = 0; index < permissions.Length; index++ ) {
			permissions[ index ] = 0 != ( mode & bits[ index ] ) ? letters[ index ] : '-';
		}
		permissions[ 2 ] = 0 != ( mode & 0x800 ) ? ( 'x' == permissions[ 2 ] ? 's' : 'S' ) : permissions[ 2 ];
		permissions[ 5 ] = 0 != ( mode & 0x400 ) ? ( 'x' == permissions[ 5 ] ? 's' : 'S' ) : permissions[ 5 ];
		permissions[ 8 ] = 0 != ( mode & 0x200 ) ? ( 'x' == permissions[ 8 ] ? 't' : 'T' ) : permissions[ 8 ];
		return type + new string( permissions );
	}

	private string FormatSize( ulong size ) {
		if ( !this.options.HumanReadable ) {
			return size.ToString( CultureInfo.InvariantCulture );
		}
		return FormatHuman( size, this.options.SiUnits ? 1000UL : 1024UL, this.options.SiUnits );
	}

	private string FormatBlockCount( ulong allocatedBytes ) {
		var blocks = allocatedBytes / this.options.BlockSize;
		if ( 0 != allocatedBytes % this.options.BlockSize ) {
			blocks++;
		}
		return this.options.HumanReadable
			? FormatHuman( allocatedBytes, this.options.SiUnits ? 1000UL : 1024UL, this.options.SiUnits )
			: blocks.ToString( CultureInfo.InvariantCulture );
	}

	private static string FormatHuman( ulong value, ulong radix, bool si ) {
		var suffixes = si
			? new[] { "", "k", "M", "G", "T", "P", "E" }
			: new[] { "", "K", "M", "G", "T", "P", "E" };
		var scaled = (decimal)value;
		var suffix = 0;
		while ( scaled >= radix && suffix + 1 < suffixes.Length ) {
			scaled /= radix;
			suffix++;
		}
		var text = suffix == 0 || scaled >= 10 ? decimal.Round( scaled, 0, MidpointRounding.AwayFromZero ).ToString( "0", CultureInfo.InvariantCulture ) : decimal.Round( scaled, 1, MidpointRounding.AwayFromZero ).ToString( "0.#", CultureInfo.InvariantCulture );
		return text + suffixes[ suffix ];
	}

	private string FormatTime( DateTimeOffset value ) {
		if ( DateTimeOffset.MinValue == value ) {
			return "-";
		}
		var local = value.ToLocalTime();
		var style = this.options.TimeStyle;
		if ( style.StartsWith( "posix-", StringComparison.OrdinalIgnoreCase ) ) {
			style = style[ 6.. ];
		}
		if ( style.StartsWith( "+", StringComparison.Ordinal ) ) {
			var customFormat = style[ 1.. ];
			var separator = customFormat.IndexOf( '\n' );
			var selected = separator < 0
				? customFormat
				: IsRecentTime( local )
					? customFormat[ ( separator + 1 ).. ]
					: customFormat[ ..separator ];
			return local.ToString( TranslateTimeFormat( selected ), CultureInfo.CurrentCulture );
		}
		return style.ToLowerInvariant() switch {
			"full-iso" => local.ToString( "yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture ),
			"long-iso" => local.ToString( "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture ),
			"iso" => IsRecentTime( local )
				? local.ToString( "MM-dd HH:mm", CultureInfo.InvariantCulture )
				: local.ToString( "yyyy-MM-dd", CultureInfo.InvariantCulture ),
			_ => FormatLocaleTime( local )
		};
	}

	private static string FormatLocaleTime( DateTimeOffset value ) {
		return IsRecentTime( value )
			? value.ToString( "MMM dd HH:mm", CultureInfo.CurrentCulture )
			: value.ToString( "MMM dd  yyyy", CultureInfo.CurrentCulture );
	}

	private static bool IsRecentTime( DateTimeOffset value ) {
		var now = DateTimeOffset.Now;
		return value >= now.AddMonths( -6 ) && value <= now.AddHours( 1 );
	}

	private static string TranslateTimeFormat( string format ) {
		return format
			.Replace( "%Y", "yyyy", StringComparison.Ordinal )
			.Replace( "%y", "yy", StringComparison.Ordinal )
			.Replace( "%m", "MM", StringComparison.Ordinal )
			.Replace( "%d", "dd", StringComparison.Ordinal )
			.Replace( "%H", "HH", StringComparison.Ordinal )
			.Replace( "%M", "mm", StringComparison.Ordinal )
			.Replace( "%S", "ss", StringComparison.Ordinal )
			.Replace( "%N", "fffffff", StringComparison.Ordinal )
			.Replace( "%z", "zzz", StringComparison.Ordinal )
			.Replace( "%b", "MMM", StringComparison.Ordinal )
			.Replace( "%a", "ddd", StringComparison.Ordinal )
			.Replace( "%%", "%", StringComparison.Ordinal );
	}

	private static ulong GetLogicalSize( ListingEntry entry ) {
		return entry.Metadata.Size.IsAvailable ? entry.Metadata.Size.GetRequiredValue() : 0;
	}

	private static ulong? SumAllocatedBytes( IEnumerable<ListingEntry> entries ) {
		var total = 0UL;
		foreach ( var entry in entries ) {
			if ( !entry.Metadata.AllocatedBytes.IsAvailable ) {
				return null;
			}
			var value = entry.Metadata.AllocatedBytes.GetRequiredValue();
			if ( ulong.MaxValue - total < value ) {
				return ulong.MaxValue;
			}
			total += value;
		}
		return total;
	}

	private string FormatAllocatedBlocks( ListingEntry entry ) {
		return entry.Metadata.AllocatedBytes.IsAvailable
			? this.FormatBlockCount( entry.Metadata.AllocatedBytes.GetRequiredValue() )
			: "?";
	}

	private string FormatLogicalSize( ListingEntry entry ) {
		return entry.Metadata.Size.IsAvailable
			? this.FormatSize( entry.Metadata.Size.GetRequiredValue() )
			: "?";
	}

	private static string FormatOptional<T>( FileSystemMetadataValue<T> value, string fallback = "?" ) {
		return value.IsAvailable
			? Convert.ToString( value.GetRequiredValue(), CultureInfo.InvariantCulture ) ?? fallback
			: fallback;
	}

	private static string GetIdentityKey( ListingEntry entry ) {
		var identity = entry.Metadata.EntryIdentity.ToString();
		return string.IsNullOrWhiteSpace( identity ) ? System.IO.Path.GetFullPath( entry.Path ) : identity;
	}

	private static string QuoteDiagnostic( string value ) {
		return "'" + value.Replace( "'", "'\\''", StringComparison.Ordinal ) + "'";
	}

	private sealed record ListingEntry(
		string Path,
		string Name,
		bool IsOperand,
		FileSystemMetadata Metadata,
		bool IsDirectory
	) {
		/// <summary>Gets the operand pathname or directory-entry name presented to the user.</summary>
		public string DisplayName => this.IsOperand ? this.Path : this.Name;
		/// <summary>Gets whether the rendered entry is the physical indirection object rather than its dereferenced target.</summary>
		public bool IsPathIndirection => this.Metadata.IsPathIndirection && !this.Metadata.WasDereferenced;
	}

	private sealed record LongListingRow(
		string Inode,
		string Blocks,
		string Mode,
		string Links,
		string Owner,
		string Group,
		string Author,
		string Size,
		string Time,
		string Name
	);

	private sealed class ListingNameComparer : IComparer<ListingEntry> {
		/// <summary>Gets the reusable locale-sensitive comparer.</summary>
		public static ListingNameComparer Instance { get; } = new();
		/// <summary>Compares two entries by locale-sensitive name order.</summary>
		/// <param name="x">The left entry.</param>
		/// <param name="y">The right entry.</param>
		/// <returns>A signed ordering result.</returns>
		public int Compare( ListingEntry? x, ListingEntry? y ) {
			if ( ReferenceEquals( x, y ) ) return 0;
			if ( x is null ) return -1;
			if ( y is null ) return 1;
			return CultureInfo.CurrentCulture.CompareInfo.Compare( x.Name, y.Name, CompareOptions.StringSort );
		}
	}

	private sealed class VersionListingComparer : IComparer<ListingEntry> {
		/// <summary>Gets the reusable natural-version comparer.</summary>
		public static VersionListingComparer Instance { get; } = new();
		/// <summary>Compares two entries using natural-version ordering.</summary>
		/// <param name="x">The left entry.</param>
		/// <param name="y">The right entry.</param>
		/// <returns>A signed ordering result.</returns>
		public int Compare( ListingEntry? x, ListingEntry? y ) {
			if ( ReferenceEquals( x, y ) ) return 0;
			if ( x is null ) return -1;
			if ( y is null ) return 1;
			return CompareVersion( x.Name, y.Name );
		}
		private static int CompareVersion( string left, string right ) {
			var leftIndex = 0;
			var rightIndex = 0;
			while ( leftIndex < left.Length && rightIndex < right.Length ) {
				if ( char.IsDigit( left[ leftIndex ] ) && char.IsDigit( right[ rightIndex ] ) ) {
					var leftStart = leftIndex;
					var rightStart = rightIndex;
					while ( leftIndex < left.Length && char.IsDigit( left[ leftIndex ] ) ) leftIndex++;
					while ( rightIndex < right.Length && char.IsDigit( right[ rightIndex ] ) ) rightIndex++;
					var leftDigits = left[ leftStart..leftIndex ].TrimStart( '0' );
					var rightDigits = right[ rightStart..rightIndex ].TrimStart( '0' );
					var lengthComparison = leftDigits.Length.CompareTo( rightDigits.Length );
					if ( 0 != lengthComparison ) return lengthComparison;
					var digitComparison = string.CompareOrdinal( leftDigits, rightDigits );
					if ( 0 != digitComparison ) return digitComparison;
					var zeroComparison = ( leftIndex - leftStart ).CompareTo( rightIndex - rightStart );
					if ( 0 != zeroComparison ) return zeroComparison;
					continue;
				}
				var comparison = CultureInfo.CurrentCulture.CompareInfo.Compare( left, leftIndex, 1, right, rightIndex, 1, CompareOptions.StringSort );
				if ( 0 != comparison ) return comparison;
				leftIndex++;
				rightIndex++;
			}
			return ( left.Length - leftIndex ).CompareTo( right.Length - rightIndex );
		}
	}
}

/// <summary>Measures terminal display cells while ignoring ANSI control sequences.</summary>
internal static class DisplayWidth {
	/// <summary>Measures one rendered value.</summary>
	/// <param name="value">The rendered value.</param>
	/// <param name="tabSize">The configured tab width.</param>
	/// <returns>The display-cell width.</returns>
	public static int Measure( string value, int tabSize ) {
		var width = 0;
		var escapeState = 0;
		foreach ( var rune in value.EnumerateRunes() ) {
			if ( 1 == escapeState ) {
				escapeState = '[' == rune.Value ? 2 : 0;
				continue;
			}
			if ( 2 == escapeState ) {
				if ( rune.Value is >= 0x40 and <= 0x7e ) {
					escapeState = 0;
				}
				continue;
			}
			if ( 0x1b == rune.Value ) {
				escapeState = 1;
				continue;
			}
			if ( '\t' == rune.Value ) {
				width += 0 == tabSize ? 0 : tabSize - ( width % tabSize );
				continue;
			}
			var category = Rune.GetUnicodeCategory( rune );
			if ( category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format or UnicodeCategory.Control ) {
				continue;
			}
			width += IsWide( rune.Value ) ? 2 : 1;
		}
		return width;
	}

	private static bool IsWide( int value ) {
		return value is >= 0x1100 and <= 0x115f
			or >= 0x2329 and <= 0x232a
			or >= 0x2e80 and <= 0xa4cf
			or >= 0xac00 and <= 0xd7a3
			or >= 0xf900 and <= 0xfaff
			or >= 0xfe10 and <= 0xfe19
			or >= 0xfe30 and <= 0xfe6f
			or >= 0xff00 and <= 0xff60
			or >= 0xffe0 and <= 0xffe6
			or >= 0x1f300 and <= 0x1faff
			or >= 0x20000 and <= 0x3fffd;
	}
}
