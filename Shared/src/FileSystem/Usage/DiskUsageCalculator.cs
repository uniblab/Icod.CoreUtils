using Path = global::System.IO.Path;
using System.IO.Enumeration;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;

namespace Icod.CoreUtils.Shared.FileSystem.Usage;

/// <summary>Identifies the timestamp accumulated for <c>du --time</c>.</summary>
public enum DiskUsageTimeField {
	/// <summary>Use the last-data-modification time.</summary>
	Modification = 0,
	/// <summary>Use the last-access time.</summary>
	Access = 1,
	/// <summary>Use the last metadata-change time.</summary>
	Change = 2,
	/// <summary>Use the birth or creation time.</summary>
	Birth = 3
}

/// <summary>Controls recursive disk-usage accounting.</summary>
public sealed class DiskUsageCalculationOptions {
	/// <summary>Gets or initializes whether logical size replaces allocated size.</summary>
	public bool ApparentSize { get; init; }
	/// <summary>Gets or initializes whether every hard-link name is counted.</summary>
	public bool CountLinks { get; init; }
	/// <summary>Gets or initializes whether only inode counts are accumulated.</summary>
	public bool Inodes { get; init; }
	/// <summary>Gets or initializes whether directory totals exclude descendant directory totals.</summary>
	public bool SeparateDirectories { get; init; }
	/// <summary>Gets or initializes whether traversal remains on each root filesystem.</summary>
	public bool OneFileSystem { get; init; }
	/// <summary>Gets or initializes the directory-link traversal policy.</summary>
	public SymbolicLinkTraversalMode SymbolicLinkMode { get; init; } = SymbolicLinkTraversalMode.Never;
	/// <summary>Gets or initializes the timestamp accumulated for each result.</summary>
	public DiskUsageTimeField TimeField { get; init; } = DiskUsageTimeField.Modification;
	/// <summary>Gets exclusion glob patterns.</summary>
	public IReadOnlyList<string> ExcludePatterns { get; init; } = Array.Empty<string>();
}

/// <summary>Represents one postorder disk-usage result.</summary>
public sealed record DiskUsageResult( string Path, int Depth, ulong Value, DateTimeOffset? LatestTime, bool IsDirectory );

/// <summary>Represents a controlled traversal diagnostic.</summary>
public sealed record DiskUsageDiagnostic( string Path, string Message );

/// <summary>Represents the complete result for one root.</summary>
public sealed class DiskUsageCalculation {
	/// <summary>Gets postorder entries.</summary>
	public List<DiskUsageResult> Entries { get; } = new();
	/// <summary>Gets controlled diagnostics.</summary>
	public List<DiskUsageDiagnostic> Diagnostics { get; } = new();
}

/// <summary>Computes allocated or apparent usage over the shared traversal and metadata providers.</summary>
public sealed class DiskUsageCalculator {
	private readonly IFileSystemMetadataProvider metadataProvider;
	private readonly IReadOnlyFileSystemProvider readOnlyProvider;
	private readonly HashSet<FileSystemEntryIdentity> countedLinks = new();

	/// <summary>Initializes a calculator over injectable providers.</summary>
	public DiskUsageCalculator(
		IFileSystemMetadataProvider metadataProvider,
		IReadOnlyFileSystemProvider readOnlyProvider
	) {
		this.metadataProvider = metadataProvider ?? throw new ArgumentNullException( nameof( metadataProvider ) );
		this.readOnlyProvider = readOnlyProvider ?? throw new ArgumentNullException( nameof( readOnlyProvider ) );
	}

	/// <summary>Clears cross-operand hard-link deduplication state.</summary>
	public void ResetHardLinkState() => countedLinks.Clear();

	/// <summary>Computes one root in deterministic postorder.</summary>
	public async Task<DiskUsageCalculation> CalculateAsync(
		string path,
		DiskUsageCalculationOptions options,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		ArgumentNullException.ThrowIfNull( options );
		if ( !Enum.IsDefined( typeof( DiskUsageTimeField ), options.TimeField ) ) {
			throw new ArgumentOutOfRangeException( nameof( options ) );
		}
		var result = new DiskUsageCalculation();
		var roots = new[] {
			new PathTraversalRoot( path, 0, 0, path, path, PathTraversalRootKind.Literal )
		};
		var traversal = new ReadOnlyPathTraversalEngine( readOnlyProvider );
		var traversalOptions = new PathTraversalOptions {
			SymbolicLinkMode = options.SymbolicLinkMode,
			FileSystemBoundaryMode = options.OneFileSystem
				? FileSystemBoundaryMode.StayOnRootFileSystem
				: FileSystemBoundaryMode.CrossFileSystems,
			ChildOrder = PathTraversalChildOrder.Ordinal,
			Selector = new ExclusionSelector( options.ExcludePatterns ),
			ErrorMode = PathTraversalErrorMode.Continue
		};
		var frames = new Dictionary<string, Frame>( PathComparer );
		await foreach ( var item in traversal.TraverseAsync( roots, traversalOptions, cancellationToken ).ConfigureAwait( false ) ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( item.Kind == PathTraversalEventKind.Error ) {
				var error = item.Error!;
				result.Diagnostics.Add( new DiskUsageDiagnostic( error.Path, error.Message ) );
				continue;
			}
			if ( item.Kind == PathTraversalEventKind.Cycle ) {
				result.Diagnostics.Add( new DiskUsageDiagnostic( item.Entry!.DisplayPath, "symbolic link cycle detected" ) );
				continue;
			}
			if ( item.Kind is PathTraversalEventKind.Root or PathTraversalEventKind.FileSystemBoundary ) {
				continue;
			}
			var entry = item.Entry;
			if ( entry is null ) {
				continue;
			}
			switch ( item.Kind ) {
				case PathTraversalEventKind.EnterDirectory:
					frames[ NormalizePath( entry.AccessPath ) ] = new Frame();
					break;
				case PathTraversalEventKind.Entry:
					var leaf = await ObserveValueWithDiagnosticsAsync( entry, options, result, cancellationToken ).ConfigureAwait( false );
					AddToParent( frames, entry, leaf.Value, leaf.LatestTime, false, options.SeparateDirectories );
					result.Entries.Add( new DiskUsageResult( entry.DisplayPath, entry.Depth, leaf.Value, leaf.LatestTime, false ) );
					break;
				case PathTraversalEventKind.LeaveDirectory:
					if ( !frames.Remove( NormalizePath( entry.AccessPath ), out var frame ) ) {
						break;
					}
					var own = await ObserveValueWithDiagnosticsAsync( entry, options, result, cancellationToken ).ConfigureAwait( false );
					frame.Value = checked( frame.Value + own.Value );
					frame.LatestTime = Latest( frame.LatestTime, own.LatestTime );
					result.Entries.Add( new DiskUsageResult( entry.DisplayPath, entry.Depth, frame.Value, frame.LatestTime, true ) );
					AddToParent( frames, entry, frame.Value, frame.LatestTime, true, options.SeparateDirectories );
					break;
			}
		}
		return result;
	}

	private async Task<ValueAndTime> ObserveValueWithDiagnosticsAsync(
		PathTraversalEntry entry,
		DiskUsageCalculationOptions options,
		DiskUsageCalculation result,
		CancellationToken cancellationToken
	) {
		try {
			return await ObserveValueAsync( entry, options, cancellationToken ).ConfigureAwait( false );
		} catch ( Exception exception ) when (
			exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
		) {
			result.Diagnostics.Add( new DiskUsageDiagnostic( entry.DisplayPath, exception.Message ) );
			return default;
		}
	}

	private async Task<ValueAndTime> ObserveValueAsync(
		PathTraversalEntry entry,
		DiskUsageCalculationOptions options,
		CancellationToken cancellationToken
	) {
		var metadata = await metadataProvider.GetMetadataAsync(
			entry.AccessPath,
			entry.WasDereferenced,
			cancellationToken
		).ConfigureAwait( false );
		var time = GetTime( metadata, options.TimeField );
		if (
			entry.Kind != FileSystemEntryKind.Directory
			&& !options.CountLinks
			&& metadata.LinkCount.IsAvailable
			&& metadata.LinkCount.GetRequiredValue() > 1
			&& metadata.EntryIdentity.IsAvailable
		) {
			if ( !countedLinks.Add( metadata.EntryIdentity ) ) {
				return new ValueAndTime( 0, time );
			}
		}
		var value = options.Inodes
			? 1UL
			: options.ApparentSize
				? entry.Kind == FileSystemEntryKind.File || (entry.IsPathIndirection && !entry.WasDereferenced)
					? GetAvailable( metadata.Size )
					: 0
				: metadata.AllocatedBytes.IsAvailable
					? metadata.AllocatedBytes.GetRequiredValue()
					: GetAvailable( metadata.Size );
		return new ValueAndTime( value, time );
	}

	private static ulong GetAvailable( FileSystemMetadataValue<ulong> value ) => value.IsAvailable ? value.GetRequiredValue() : 0;
	private static DateTimeOffset? GetTime( FileSystemMetadata metadata, DiskUsageTimeField field ) {
		var value = field switch {
			DiskUsageTimeField.Access => metadata.AccessTime,
			DiskUsageTimeField.Change => metadata.ChangeTime,
			DiskUsageTimeField.Birth => metadata.BirthTime,
			_ => metadata.ModificationTime
		};
		return value.IsAvailable ? value.GetRequiredValue() : null;
	}
	private static DateTimeOffset? Latest( DateTimeOffset? left, DateTimeOffset? right ) => left is null
		? right
		: right is null || left >= right ? left : right;

	private static void AddToParent(
		Dictionary<string, Frame> frames,
		PathTraversalEntry entry,
		ulong value,
		DateTimeOffset? latestTime,
		bool childIsDirectory,
		bool separateDirectories
	) {
		if ( entry.IsRoot || (childIsDirectory && separateDirectories) ) {
			return;
		}
		var parentPath = Path.GetDirectoryName( Path.TrimEndingDirectorySeparator( entry.AccessPath ) );
		if ( parentPath is not null && frames.TryGetValue( NormalizePath( parentPath ), out var parent ) ) {
			parent.Value = checked( parent.Value + value );
			parent.LatestTime = Latest( parent.LatestTime, latestTime );
		}
	}

	private sealed class ExclusionSelector : IPathTraversalSelector {
		private readonly IReadOnlyList<string> patterns;

		/// <summary>Initializes a selector for GNU-style exclusion globs.</summary>
		/// <param name="patterns">The patterns matched against names and relative paths.</param>
		public ExclusionSelector( IReadOnlyList<string> patterns ) {
			this.patterns = patterns ?? throw new ArgumentNullException( nameof( patterns ) );
		}

		/// <inheritdoc/>
		public PathTraversalSelection Select( PathTraversalEntry entry ) {
			ArgumentNullException.ThrowIfNull( entry );
			var ignoreCase = OperatingSystem.IsWindows();
			foreach ( var pattern in patterns ) {
				if (
					FileSystemName.MatchesSimpleExpression( pattern, entry.Name, ignoreCase )
					|| (
						entry.RelativePath.Length > 0
						&& FileSystemName.MatchesSimpleExpression( pattern, entry.RelativePath, ignoreCase )
					)
				) {
					return PathTraversalSelection.ExcludeAll;
				}
			}
			return PathTraversalSelection.IncludeAll;
		}
	}

	private static string NormalizePath( string path ) => Path.TrimEndingDirectorySeparator( Path.GetFullPath( path ) );
	private static StringComparer PathComparer => OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	private sealed class Frame {
		/// <summary>Gets or sets the accumulated value.</summary>
		public ulong Value { get; set; }
		/// <summary>Gets or sets the latest accumulated timestamp.</summary>
		public DateTimeOffset? LatestTime { get; set; }
	}
	private readonly record struct ValueAndTime( ulong Value, DateTimeOffset? LatestTime );
}
