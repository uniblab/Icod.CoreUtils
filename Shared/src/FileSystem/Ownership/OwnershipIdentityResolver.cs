namespace Icod.CoreUtils.Shared.FileSystem.Ownership;

using System.Globalization;
using Icod.CoreUtils.Shared.Platform;

/// <summary>Describes a resolved owner and/or group selection.</summary>
/// <param name="UserId">The selected numeric user ID, or <see langword="null"/> when unchanged.</param>
/// <param name="GroupId">The selected numeric group ID, or <see langword="null"/> when unchanged.</param>
/// <param name="UserDisplay">The resolved user display text.</param>
/// <param name="GroupDisplay">The resolved group display text.</param>
public sealed record OwnershipSelection(
	uint? UserId,
	uint? GroupId,
	string? UserDisplay,
	string? GroupDisplay
) {
	/// <summary>Gets whether the selection contains at least one identity.</summary>
	public bool HasValue => UserId.HasValue || GroupId.HasValue;
}

/// <summary>Describes a successful or failed ownership-identity resolution.</summary>
public sealed class OwnershipResolutionResult {
	private OwnershipResolutionResult(
		OwnershipSelection? selection,
		string? message,
		string? warning
	) {
		Selection = selection;
		Message = message;
		Warning = warning;
	}

	/// <summary>Gets whether resolution succeeded.</summary>
	public bool Succeeded => Selection is not null;
	/// <summary>Gets the resolved selection.</summary>
	public OwnershipSelection? Selection { get; }
	/// <summary>Gets the controlled failure message.</summary>
	public string? Message { get; }
	/// <summary>Gets a non-fatal compatibility warning.</summary>
	public string? Warning { get; }

	/// <summary>Creates a successful resolution.</summary>
	/// <param name="selection">The resolved ownership selection.</param>
	/// <param name="warning">An optional non-fatal compatibility warning.</param>
	/// <returns>The successful result.</returns>
	public static OwnershipResolutionResult Success(
		OwnershipSelection selection,
		string? warning = null
	) {
		ArgumentNullException.ThrowIfNull( selection );
		return new OwnershipResolutionResult( selection, null, warning );
	}

	/// <summary>Creates a failed resolution.</summary>
	public static OwnershipResolutionResult Failure( string message ) {
		ArgumentException.ThrowIfNullOrWhiteSpace( message );
		return new OwnershipResolutionResult( null, message, null );
	}
}

/// <summary>Resolves GNU owner and group operands through an injectable identity provider.</summary>
public static class OwnershipIdentityResolver {
	private const string LegacyDotWarning = "warning: legacy owner.group syntax is deprecated; use owner:group";
	/// <summary>Resolves a <c>chown</c>-style owner specification.</summary>
	/// <param name="text">The owner specification.</param>
	/// <param name="identityProvider">The identity provider.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The controlled resolution result.</returns>
	public static async ValueTask<OwnershipResolutionResult> ResolveOwnerSpecAsync(
		string text,
		IIdentityProvider identityProvider,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( identityProvider );
		ArgumentNullException.ThrowIfNull( text );
		if ( text.Length == 0 ) {
			return OwnershipResolutionResult.Success(
				new OwnershipSelection( null, null, null, null )
			);
		}

		var separator = text.IndexOf( ':' );
		if ( separator != text.LastIndexOf( ':' ) ) {
			return OwnershipResolutionResult.Failure( "invalid owner specification" );
		}
		var legacyDot = false;
		if ( separator < 0 ) {
			var wholeUserResult = await ResolveUserAsync(
				text,
				identityProvider,
				cancellationToken
			).ConfigureAwait( false );
			if ( wholeUserResult.Succeeded ) {
				return OwnershipResolutionResult.Success( new OwnershipSelection(
					wholeUserResult.Id,
					null,
					wholeUserResult.Display,
					null
				) );
			}
			separator = text.IndexOf( '.' );
			if ( separator < 0 ) return wholeUserResult.Failure!;
			legacyDot = true;
		}

		var userText = text[..separator];
		var groupText = text[(separator + 1)..];
		if ( userText.Length == 0 && groupText.Length == 0 ) {
			return OwnershipResolutionResult.Success(
				new OwnershipSelection( null, null, null, null ),
				legacyDot ? LegacyDotWarning : null
			);
		}

		UserIdentity? user = null;
		uint? userId = null;
		string? userDisplay = null;
		if ( userText.Length != 0 ) {
			var userResult = await ResolveUserAsync(
				userText,
				identityProvider,
				cancellationToken
			).ConfigureAwait( false );
			if ( !userResult.Succeeded ) return userResult.Failure!;
			user = userResult.Identity;
			userId = userResult.Id;
			userDisplay = userResult.Display;
		}

		uint? groupId = null;
		string? groupDisplay = null;
		if ( groupText.Length == 0 ) {
			if ( user is null && userId.HasValue ) {
				user = await identityProvider.FindUserByIdAsync(
					userId.Value.ToString( CultureInfo.InvariantCulture ),
					cancellationToken
				).ConfigureAwait( false );
			}
			if ( user is null ) {
				return OwnershipResolutionResult.Failure( "invalid owner specification" );
			}
			if ( !TryParseIdentifier( user.PrimaryGroup.Id, out var primaryGroupId ) ) {
				return OwnershipResolutionResult.Failure( "invalid primary group identifier" );
			}
			groupId = primaryGroupId;
			groupDisplay = user.PrimaryGroup.Name;
		} else {
			var groupResult = await ResolveGroupCoreAsync(
				groupText,
				identityProvider,
				cancellationToken
			).ConfigureAwait( false );
			if ( !groupResult.Succeeded ) return groupResult.Failure!;
			groupId = groupResult.Id;
			groupDisplay = groupResult.Display;
		}
		return OwnershipResolutionResult.Success(
			new OwnershipSelection( userId, groupId, userDisplay, groupDisplay ),
			legacyDot ? LegacyDotWarning : null
		);
	}

	/// <summary>Resolves a <c>chgrp</c>-style group operand.</summary>
	/// <param name="text">The group name or numeric group ID.</param>
	/// <param name="identityProvider">The identity provider.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The controlled resolution result.</returns>
	public static async ValueTask<OwnershipResolutionResult> ResolveGroupAsync(
		string text,
		IIdentityProvider identityProvider,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( identityProvider );
		if ( string.IsNullOrEmpty( text ) ) {
			return OwnershipResolutionResult.Failure( "invalid group" );
		}
		var result = await ResolveGroupCoreAsync(
			text,
			identityProvider,
			cancellationToken
		).ConfigureAwait( false );
		return result.Succeeded
			? OwnershipResolutionResult.Success(
				new OwnershipSelection( null, result.Id, null, result.Display )
			)
			: result.Failure!;
	}

	private static async ValueTask<IdentityResult<UserIdentity>> ResolveUserAsync(
		string text,
		IIdentityProvider identityProvider,
		CancellationToken cancellationToken
	) {
		if ( TryParseForcedIdentifier( text, out var forcedId ) ) {
			return IdentityResult<UserIdentity>.Success( forcedId, text[1..], null );
		}
		var named = await identityProvider.FindUserAsync( text, cancellationToken ).ConfigureAwait( false );
		if ( named is not null && TryParseIdentifier( named.Id, out var namedId ) ) {
			return IdentityResult<UserIdentity>.Success( namedId, named.Name, named );
		}
		if ( TryParseIdentifier( text, out var numericId ) ) {
			var numeric = await identityProvider.FindUserByIdAsync( text, cancellationToken ).ConfigureAwait( false );
			return IdentityResult<UserIdentity>.Success(
				numericId,
				numeric?.Name ?? numericId.ToString( CultureInfo.InvariantCulture ),
				numeric
			);
		}
		return IdentityResult<UserIdentity>.Fail(
			OwnershipResolutionResult.Failure( string.Concat( "invalid user: '", text, "'" ) )
		);
	}

	private static async ValueTask<IdentityResult<GroupIdentity>> ResolveGroupCoreAsync(
		string text,
		IIdentityProvider identityProvider,
		CancellationToken cancellationToken
	) {
		if ( TryParseForcedIdentifier( text, out var forcedId ) ) {
			return IdentityResult<GroupIdentity>.Success( forcedId, text[1..], null );
		}
		var named = await identityProvider.FindGroupAsync( text, cancellationToken ).ConfigureAwait( false );
		if ( named is not null && TryParseIdentifier( named.Id, out var namedId ) ) {
			return IdentityResult<GroupIdentity>.Success( namedId, named.Name, named );
		}
		if ( TryParseIdentifier( text, out var numericId ) ) {
			var numeric = await identityProvider.FindGroupByIdAsync( text, cancellationToken ).ConfigureAwait( false );
			return IdentityResult<GroupIdentity>.Success(
				numericId,
				numeric?.Name ?? numericId.ToString( CultureInfo.InvariantCulture ),
				numeric
			);
		}
		return IdentityResult<GroupIdentity>.Fail(
			OwnershipResolutionResult.Failure( string.Concat( "invalid group: '", text, "'" ) )
		);
	}

	private static bool TryParseForcedIdentifier( string text, out uint value ) {
		if ( text.Length > 1 && text[0] == '+' ) {
			return TryParseIdentifier( text[1..], out value );
		}
		value = 0;
		return false;
	}

	private static bool TryParseIdentifier( string text, out uint value ) {
		return uint.TryParse( text, NumberStyles.None, CultureInfo.InvariantCulture, out value )
			&& value != uint.MaxValue;
	}

	private readonly record struct IdentityResult<T>(
		bool Succeeded,
		uint Id,
		string? Display,
		T? Identity,
		OwnershipResolutionResult? Failure
	) where T : class {
		/// <summary>Creates a successful identity result.</summary>
		public static IdentityResult<T> Success( uint id, string display, T? identity ) {
			return new IdentityResult<T>( true, id, display, identity, null );
		}

		/// <summary>Creates a failed identity result.</summary>
		public static IdentityResult<T> Fail( OwnershipResolutionResult failure ) {
			return new IdentityResult<T>( false, 0, null, null, failure );
		}
	}
}
