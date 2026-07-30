namespace Icod.CoreUtils.Shared.Ordering;

using System.Globalization;
using System.Text;

/// <summary>Provides C/POSIX bytewise or managed culture collation from a resolved profile.</summary>
public sealed class SystemCollationProvider : ICollationProvider {
	private readonly string profileIdentity;

	/// <summary>Initializes a collation provider.</summary>
	/// <param name="profile">The resolved profile.</param>
	public SystemCollationProvider( CollationProfile profile ) {
		ArgumentNullException.ThrowIfNull( profile );
		this.Profile = profile;
		this.profileIdentity = string.Concat(
			profile.Name,
			"|",
			(int)profile.CompareOptions,
			"|",
			profile.IsBytewise ? "byte" : "culture"
		);
	}

	/// <inheritdoc/>
	public CollationProfile Profile { get; }

	/// <inheritdoc/>
	public int Compare( string? x, string? y ) {
		if ( ReferenceEquals( x, y ) ) {
			return 0;
		}
		if ( null == x ) {
			return -1;
		}
		if ( null == y ) {
			return 1;
		}
		if ( this.Profile.IsBytewise ) {
			return ByteSequenceComparer.Instance.Compare(
				Encoding.UTF8.GetBytes( x ),
				Encoding.UTF8.GetBytes( y )
			);
		}
		return this.Profile.Culture!.CompareInfo.Compare(
			x,
			y,
			this.Profile.CompareOptions
		);
	}

	/// <inheritdoc/>
	public CollationKey CreateKey( string value ) {
		ArgumentNullException.ThrowIfNull( value );
		var data = this.Profile.IsBytewise
			? Encoding.UTF8.GetBytes( value )
			: this.Profile.Culture!.CompareInfo.GetSortKey(
				value,
				this.Profile.CompareOptions
			).KeyData;
		return new CollationKey( this.profileIdentity, data );
	}
}
