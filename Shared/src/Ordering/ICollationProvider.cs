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

/// <summary>Provides injectable locale-aware managed-string collation and reusable collation keys.</summary>
public interface ICollationProvider : IComparer<string> {
	/// <summary>Gets the active collation profile.</summary>
	CollationProfile Profile { get; }

	/// <summary>Creates a reusable key whose byte ordering matches this provider.</summary>
	/// <param name="value">The managed string to key.</param>
	/// <returns>The immutable collation key.</returns>
	CollationKey CreateKey( string value );
}

/// <summary>Represents immutable collation bytes produced by one profile.</summary>
public sealed class CollationKey {
	private readonly byte[] data;

	/// <summary>Initializes a collation key by copying its bytes.</summary>
	/// <param name="profileIdentity">The identity of the profile that produced the bytes.</param>
	/// <param name="data">The collation bytes.</param>
	public CollationKey(
		string profileIdentity,
		ReadOnlySpan<byte> data
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( profileIdentity );
		this.ProfileIdentity = profileIdentity;
		this.data = data.ToArray();
	}

	/// <summary>Gets the producing profile identity.</summary>
	public string ProfileIdentity { get; }

	/// <summary>Gets the immutable collation bytes.</summary>
	public ReadOnlyMemory<byte> Data => this.data;
}

/// <summary>Compares collation keys produced by the same profile.</summary>
public sealed class CollationKeyComparer : IComparer<CollationKey> {
	/// <summary>Gets the shared collation-key comparer.</summary>
	public static CollationKeyComparer Instance { get; } = new();

	private CollationKeyComparer() {
	}

	/// <inheritdoc/>
	public int Compare( CollationKey? x, CollationKey? y ) {
		if ( ReferenceEquals( x, y ) ) {
			return 0;
		}
		if ( null == x ) {
			return -1;
		}
		if ( null == y ) {
			return 1;
		}
		if ( !string.Equals(
			x.ProfileIdentity,
			y.ProfileIdentity,
			StringComparison.Ordinal
		) ) {
			throw new ArgumentException(
				"Collation keys from different profiles cannot be compared."
			);
		}
		return ByteSequenceComparer.Instance.Compare( x.Data, y.Data );
	}
}

/// <summary>Compares byte sequences lexicographically without allocating.</summary>
public sealed class ByteSequenceComparer : IComparer<ReadOnlyMemory<byte>> {
	/// <summary>Gets the shared lexicographic byte comparer.</summary>
	public static ByteSequenceComparer Instance { get; } = new();

	private ByteSequenceComparer() {
	}

	/// <inheritdoc/>
	public int Compare( ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y ) {
		return x.Span.SequenceCompareTo( y.Span );
	}
}
