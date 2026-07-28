namespace Icod.CoreUtils.Shared.Temporary;

/// <summary>Generates secure base-62 names and creates temporary objects with exclusive filesystem operations.</summary>
public sealed class SecureTemporaryObjectCreator {
	private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	/// <summary>Gets the GNU-compatible minimum candidate-attempt bound of 62 cubed.</summary>
	public const int DefaultMaximumAttempts = 238_328;

	private readonly ITemporaryObjectFileSystem fileSystem;
	private readonly ISecureRandomSource randomSource;
	private readonly int maximumAttempts;

	/// <summary>Initializes a temporary-object creator.</summary>
	/// <param name="fileSystem">The exclusive filesystem provider.</param>
	/// <param name="randomSource">The cryptographic random source.</param>
	/// <param name="maximumAttempts">The maximum number of candidate names to attempt.</param>
	public SecureTemporaryObjectCreator(
		ITemporaryObjectFileSystem fileSystem,
		ISecureRandomSource randomSource,
		int maximumAttempts = DefaultMaximumAttempts
	) {
		ArgumentNullException.ThrowIfNull( fileSystem );
		ArgumentNullException.ThrowIfNull( randomSource );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( maximumAttempts );
		this.fileSystem = fileSystem;
		this.randomSource = randomSource;
		this.maximumAttempts = maximumAttempts;
	}

	/// <summary>Gets a creator backed by the host cryptographic generator and filesystem.</summary>
	public static SecureTemporaryObjectCreator System { get; } = new(
		SystemTemporaryObjectFileSystem.Instance,
		CryptographicRandomSource.Instance
	);

	/// <summary>Generates and optionally creates a temporary object.</summary>
	/// <param name="template">The parsed temporary-name template.</param>
	/// <param name="kind">The requested operation.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The complete creation result.</returns>
	public TemporaryObjectCreationResult Create(
		TemporaryNameTemplate template,
		TemporaryObjectKind kind,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( template );
		var replacement = new char[ template.ReplacementLength ];
		for ( var attempt = 1; maximumAttempts >= attempt; attempt++ ) {
			cancellationToken.ThrowIfCancellationRequested();
			for ( var index = 0; replacement.Length > index; index++ ) {
				replacement[ index ] = Alphabet[
					randomSource.GetInt32( Alphabet.Length )
				];
			}
			var path = template.Render( replacement );
			cancellationToken.ThrowIfCancellationRequested();
			var result = fileSystem.TryCreate( path, kind );
			switch ( result.Status ) {
				case TemporaryObjectAttemptStatus.Success:
					return TemporaryObjectCreationResult.Succeeded(
						path,
						attempt,
						kind
					);
				case TemporaryObjectAttemptStatus.Collision:
					continue;
				case TemporaryObjectAttemptStatus.Failure:
					return TemporaryObjectCreationResult.Failed(
						result.ErrorMessage ?? "temporary-object creation failed",
						attempt,
						kind
					);
				default:
					throw new InvalidOperationException( "Unknown temporary-object attempt status." );
			}
		}
		return TemporaryObjectCreationResult.Failed(
			string.Concat(
				"failed to create a unique temporary name after ",
				maximumAttempts,
				" attempts"
			),
			maximumAttempts,
			kind
		);
	}

	/// <summary>Attempts to remove a previously created temporary object.</summary>
	/// <param name="path">The pathname to remove.</param>
	/// <param name="kind">The object kind.</param>
	/// <param name="errorMessage">Receives a controlled error message when cleanup fails.</param>
	/// <returns><see langword="true"/> when cleanup succeeded.</returns>
	public bool TryDelete(
		string path,
		TemporaryObjectKind kind,
		out string? errorMessage
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( path );
		return fileSystem.TryDelete( path, kind, out errorMessage );
	}
}
