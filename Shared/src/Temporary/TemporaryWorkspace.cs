namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Owns one secure temporary directory and the regular files created within it.</summary>
/// <remarks>
/// Cleanup never observes an operation cancellation token. Once a workspace has been created, disposal
/// attempts to remove every owned file and then the workspace directory on success, failure, or cancellation.
/// </remarks>
public sealed class TemporaryWorkspace : IDisposable, IAsyncDisposable {
	private const string DefaultDirectoryTemplate = "icod-work.XXXXXXXX";
	private const string DefaultFileTemplate = "run-XXXXXXXX.tmp";
	private readonly object syncRoot = new();
	private readonly SecureTemporaryObjectCreator creator;
	private readonly List<string> ownedFiles = new();
	private bool isDisposed;

	private TemporaryWorkspace(
		string rootPath,
		SecureTemporaryObjectCreator creator
	) {
		this.RootPath = rootPath;
		this.creator = creator;
	}

	/// <summary>Gets the absolute pathname of the owned temporary directory.</summary>
	public string RootPath { get; }

	/// <summary>Gets a snapshot of the regular files currently owned by the workspace.</summary>
	public IReadOnlyList<string> OwnedFiles {
		get {
			lock ( this.syncRoot ) {
				return this.ownedFiles.ToArray();
			}
		}
	}

	/// <summary>Creates a secure temporary workspace beneath a parent directory.</summary>
	/// <param name="parentDirectory">The existing parent directory, or <see langword="null"/> for the host temporary directory.</param>
	/// <param name="directoryTemplate">A leaf-name GNU temporary template containing at least three consecutive <c>X</c> characters.</param>
	/// <param name="creator">The secure temporary-object creator, or <see langword="null"/> for the host provider.</param>
	/// <param name="cancellationToken">A token used only while creating the workspace directory.</param>
	/// <returns>The newly owned workspace.</returns>
	/// <exception cref="ArgumentException">The parent or template is invalid.</exception>
	/// <exception cref="IOException">The directory could not be created securely.</exception>
	public static TemporaryWorkspace Create(
		string? parentDirectory = null,
		string directoryTemplate = DefaultDirectoryTemplate,
		SecureTemporaryObjectCreator? creator = null,
		CancellationToken cancellationToken = default
	) {
		var selectedCreator = creator ?? SecureTemporaryObjectCreator.System;
		var parent = Path.GetFullPath( parentDirectory ?? Path.GetTempPath() );
		if ( !Directory.Exists( parent ) ) {
			throw new ArgumentException(
				"The temporary-workspace parent directory does not exist.",
				nameof( parentDirectory )
			);
		}
		var template = ParseLeafTemplate( directoryTemplate, nameof( directoryTemplate ) )
			.WithDirectory( parent );
		var result = selectedCreator.Create(
			template,
			TemporaryObjectKind.Directory,
			cancellationToken
		);
		if ( !result.IsSuccess || string.IsNullOrEmpty( result.Path ) ) {
			throw new IOException(
				result.ErrorMessage ?? "Secure temporary-workspace creation failed."
			);
		}
		return new TemporaryWorkspace( result.Path, selectedCreator );
	}

	/// <summary>Creates and assumes ownership of an empty regular file inside the workspace.</summary>
	/// <param name="fileTemplate">A leaf-name GNU temporary template containing at least three consecutive <c>X</c> characters.</param>
	/// <param name="cancellationToken">A token used only while creating the file.</param>
	/// <returns>The absolute pathname of the newly created file.</returns>
	/// <exception cref="ObjectDisposedException">The workspace has already been disposed.</exception>
	/// <exception cref="ArgumentException">The template is invalid.</exception>
	/// <exception cref="IOException">The file could not be created securely.</exception>
	public string CreateFile(
		string fileTemplate = DefaultFileTemplate,
		CancellationToken cancellationToken = default
	) {
		lock ( this.syncRoot ) {
			ObjectDisposedException.ThrowIf( this.isDisposed, this );
			var template = ParseLeafTemplate( fileTemplate, nameof( fileTemplate ) )
				.WithDirectory( this.RootPath );
			var result = this.creator.Create(
				template,
				TemporaryObjectKind.File,
				cancellationToken
			);
			if ( !result.IsSuccess || string.IsNullOrEmpty( result.Path ) ) {
				throw new IOException(
					result.ErrorMessage ?? "Secure temporary-file creation failed."
				);
			}
			this.ownedFiles.Add( result.Path );
			return result.Path;
		}
	}

	/// <summary>Deletes one owned regular file and releases it from the workspace.</summary>
	/// <param name="path">The exact pathname returned by <see cref="CreateFile(string, CancellationToken)"/>.</param>
	/// <exception cref="ObjectDisposedException">The workspace has already been disposed.</exception>
	/// <exception cref="ArgumentException">The pathname is not owned by this workspace.</exception>
	/// <exception cref="IOException">The file could not be removed.</exception>
	public void DeleteFile( string path ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		lock ( this.syncRoot ) {
			ObjectDisposedException.ThrowIf( this.isDisposed, this );
			var index = this.ownedFiles.FindIndex(
				value => string.Equals( value, path, StringComparison.Ordinal )
			);
			if ( 0 > index ) {
				throw new ArgumentException(
					"The pathname is not owned by this temporary workspace.",
					nameof( path )
				);
			}
			if ( !this.TryDeleteOwnedObject(
				path,
				TemporaryObjectKind.File,
				out var errorMessage
			) ) {
				throw new IOException(
					errorMessage ?? "Temporary-file cleanup failed."
				);
			}
			this.ownedFiles.RemoveAt( index );
		}
	}

	/// <summary>Removes all owned files and then the workspace directory.</summary>
	/// <exception cref="IOException">One or more owned objects could not be removed.</exception>
	public void Dispose() {
		List<string> files;
		lock ( this.syncRoot ) {
			if ( this.isDisposed ) {
				return;
			}
			this.isDisposed = true;
			files = new List<string>( this.ownedFiles );
			this.ownedFiles.Clear();
		}
		var errors = new List<string>();
		for ( var index = files.Count - 1; 0 <= index; index-- ) {
			if ( !this.TryDeleteOwnedObject(
				files[ index ],
				TemporaryObjectKind.File,
				out var errorMessage
			) ) {
				errors.Add( string.Concat(
					files[ index ],
					": ",
					errorMessage ?? "temporary-file cleanup failed"
				) );
			}
		}
		if ( !this.TryDeleteOwnedObject(
			this.RootPath,
			TemporaryObjectKind.Directory,
			out var directoryError
		) ) {
			errors.Add( string.Concat(
				this.RootPath,
				": ",
				directoryError ?? "temporary-directory cleanup failed"
			) );
		}
		if ( 0 < errors.Count ) {
			throw new IOException( string.Join( Environment.NewLine, errors ) );
		}
	}

	/// <summary>Asynchronously completes deterministic workspace cleanup.</summary>
	/// <returns>A completed value task after synchronous filesystem cleanup.</returns>
	public ValueTask DisposeAsync() {
		this.Dispose();
		return ValueTask.CompletedTask;
	}

	private bool TryDeleteOwnedObject(
		string path,
		TemporaryObjectKind kind,
		out string? errorMessage
	) {
		try {
			return this.creator.TryDelete( path, kind, out errorMessage );
		} catch ( Exception exception ) {
			errorMessage = exception.Message;
			return false;
		}
	}

	private static TemporaryNameTemplate ParseLeafTemplate(
		string template,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( template );
		if (
			Path.IsPathRooted( template )
			|| template.Contains( '/' )
			|| template.Contains( '\\' )
			|| !string.Equals( Path.GetFileName( template ), template, StringComparison.Ordinal )
		) {
			throw new ArgumentException(
				"A temporary-workspace template must be a leaf name.",
				parameterName
			);
		}
		if ( !TemporaryNameTemplate.TryParse(
			template,
			null,
			out var result,
			out var errorMessage
		) ) {
			throw new ArgumentException( errorMessage, parameterName );
		}
		return result!;
	}
}
