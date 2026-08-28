/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

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
