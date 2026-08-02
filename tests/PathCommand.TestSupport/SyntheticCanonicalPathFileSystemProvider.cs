namespace Icod.CoreUtils.PathCommandTests;

using Icod.Path;

/// <summary>Provides deterministic no-follow filesystem observations for path-command tests.</summary>
internal sealed class SyntheticCanonicalPathFileSystemProvider : ICanonicalPathFileSystemProvider {
	private readonly Dictionary<string, PathComponentObservation> entries;

	/// <summary>Initializes a provider with an existing root.</summary>
	/// <param name="semantics">The pathname grammar.</param>
	/// <param name="currentDirectory">The absolute current directory.</param>
	public SyntheticCanonicalPathFileSystemProvider(
		PathPlatformSemantics semantics,
		string currentDirectory
	) {
		this.Semantics = semantics;
		this.CurrentDirectory = currentDirectory;
		this.entries = new Dictionary<string, PathComponentObservation>( semantics.PathComparer );
		var root = PathLexicalNormalizer.Normalize(
			currentDirectory,
			currentDirectory,
			semantics
		).Root!;
		this.AddDirectory( root.RootPath );
	}

	/// <inheritdoc/>
	public PathPlatformSemantics Semantics { get; }

	/// <inheritdoc/>
	public string CurrentDirectory { get; }

	/// <summary>Adds an existing directory.</summary>
	/// <param name="path">The absolute lexical pathname.</param>
	/// <returns>This provider.</returns>
	public SyntheticCanonicalPathFileSystemProvider AddDirectory( string path ) =>
		this.Add( PathComponentObservation.Existing( path, CanonicalPathEntryKind.Directory ) )
	;

	/// <summary>Adds an existing file.</summary>
	/// <param name="path">The absolute lexical pathname.</param>
	/// <returns>This provider.</returns>
	public SyntheticCanonicalPathFileSystemProvider AddFile( string path ) =>
		this.Add( PathComponentObservation.Existing( path, CanonicalPathEntryKind.File ) )
	;

	/// <summary>Adds a supported symbolic link.</summary>
	/// <param name="path">The absolute lexical pathname.</param>
	/// <param name="target">The raw target text.</param>
	/// <param name="isReparsePoint">Whether the link is represented by a Windows reparse point.</param>
	/// <returns>This provider.</returns>
	public SyntheticCanonicalPathFileSystemProvider AddLink(
		string path,
		string target,
		bool isReparsePoint = false
	) => this.Add(
		PathComponentObservation.Existing(
			path,
			CanonicalPathEntryKind.Unknown,
			isSymbolicLink: true,
			linkTarget: target,
			isReparsePoint: isReparsePoint
		)
	);

	/// <summary>Adds an unsupported reparse-point object.</summary>
	/// <param name="path">The absolute lexical pathname.</param>
	/// <returns>This provider.</returns>
	public SyntheticCanonicalPathFileSystemProvider AddUnsupportedReparsePoint(
		string path
	) => this.Add(
		PathComponentObservation.Existing(
			path,
			CanonicalPathEntryKind.Unknown,
			isReparsePoint: true
		)
	);

	/// <inheritdoc/>
	public ValueTask<PathComponentObservation> ObserveAsync(
		string path,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult(
			this.entries.TryGetValue( path, out var observation )
				? observation
				: PathComponentObservation.Missing( path )
		);
	}

	private SyntheticCanonicalPathFileSystemProvider Add( PathComponentObservation observation ) {
		this.entries[observation.Path] = observation;
		return this;
	}
}
