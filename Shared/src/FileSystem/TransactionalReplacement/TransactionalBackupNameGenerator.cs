using System.Globalization;

namespace Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;

/// <summary>Generates GNU-compatible simple, numbered, and existing-style backup names.</summary>
public sealed class TransactionalBackupNameGenerator {
	/// <summary>Generates one backup pathname.</summary>
	/// <param name="destinationPath">The destination being protected.</param>
	/// <param name="policy">The validated backup policy.</param>
	/// <param name="pathExistsAsync">An injected no-follow existence predicate.</param>
	/// <param name="anyNumberedBackupExistsAsync">An injected bounded numbered-backup predicate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The selected backup pathname, or <see langword="null"/> when retention is disabled.</returns>
	public async ValueTask<string?> GenerateAsync(
		string destinationPath,
		TransactionalReplacementBackupPolicy policy,
		Func<string, CancellationToken, ValueTask<bool>> pathExistsAsync,
		Func<string, int, CancellationToken, ValueTask<bool>> anyNumberedBackupExistsAsync,
		CancellationToken cancellationToken = default
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( destinationPath );
		ArgumentNullException.ThrowIfNull( policy );
		ArgumentNullException.ThrowIfNull( pathExistsAsync );
		ArgumentNullException.ThrowIfNull( anyNumberedBackupExistsAsync );
		policy.Validate();
		if ( TransactionalReplacementBackupRetention.RetainAfterSuccess != policy.Retention ) {
			return null;
		}
		var mode = policy.Mode;
		if ( TransactionalReplacementBackupMode.Existing == mode ) {
			mode = await anyNumberedBackupExistsAsync(
				destinationPath,
				policy.MaximumNumberedBackup,
				cancellationToken
			).ConfigureAwait( false )
				? TransactionalReplacementBackupMode.Numbered
				: TransactionalReplacementBackupMode.Simple;
		}
		if ( TransactionalReplacementBackupMode.Simple == mode ) {
			return string.Concat( destinationPath, policy.SimpleSuffix );
		}
		if ( TransactionalReplacementBackupMode.Numbered == mode ) {
			for ( var number = 1; number <= policy.MaximumNumberedBackup; number++ ) {
				cancellationToken.ThrowIfCancellationRequested();
				var candidate = CreateNumberedName( destinationPath, number );
				if ( !await pathExistsAsync( candidate, cancellationToken ).ConfigureAwait( false ) ) {
					return candidate;
				}
			}
			throw new IOException( "No unused numbered backup name is available within the configured bound." );
		}
		return null;
	}

	private static string CreateNumberedName( string destinationPath, int number ) {
		return string.Concat(
			destinationPath,
			".~",
			number.ToString( CultureInfo.InvariantCulture ),
			"~"
		);
	}
}
