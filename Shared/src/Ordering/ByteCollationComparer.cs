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

/// <summary>Compares byte sequences according to a resolved collation profile.</summary>
/// <remarks>
/// C and POSIX profiles compare bytes directly. Linguistic profiles decode valid UTF-8
/// and use the profile's managed <see cref="CompareInfo"/>; invalid UTF-8 falls back to deterministic raw-byte comparison. The optional case-insensitive mode is intended
/// for record-oriented commands such as <c>join --ignore-case</c>.
/// </remarks>
public sealed class ByteCollationComparer : IComparer<ReadOnlyMemory<byte>> {
	private static readonly UTF8Encoding Utf8 = new( false, true );
	private readonly ICollationProvider myCollation;
	private readonly bool myIgnoreCase;

	/// <summary>Initializes a byte-sequence collation comparer.</summary>
	/// <param name="collation">The resolved collation provider.</param>
	/// <param name="ignoreCase">Whether case differences are ignored.</param>
	public ByteCollationComparer(
		ICollationProvider collation,
		bool ignoreCase = false
	) {
		this.myCollation = collation ?? throw new ArgumentNullException( nameof( collation ) );
		this.myIgnoreCase = ignoreCase;
	}

	/// <summary>Gets the collation provider used by this comparer.</summary>
	public ICollationProvider Collation => this.myCollation;

	/// <summary>Gets whether case differences are ignored.</summary>
	public bool IgnoreCase => this.myIgnoreCase;

	/// <inheritdoc/>
	public int Compare( ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y ) {
		if ( this.myCollation.Profile.IsBytewise ) {
			return this.myIgnoreCase
				? CompareAsciiFolded( x.Span, y.Span )
				: x.Span.SequenceCompareTo( y.Span );
		}
		string left;
		string right;
		try {
			left = Utf8.GetString( x.Span );
			right = Utf8.GetString( y.Span );
		} catch ( DecoderFallbackException ) {
			return this.myIgnoreCase
				? CompareAsciiFolded( x.Span, y.Span )
				: x.Span.SequenceCompareTo( y.Span );
		}
		if ( !this.myIgnoreCase ) {
			return this.myCollation.Compare( left, right );
		}
		return this.myCollation.Profile.Culture!.CompareInfo.Compare(
			left,
			right,
			this.myCollation.Profile.CompareOptions | CompareOptions.IgnoreCase
		);
	}

	private static int CompareAsciiFolded(
		ReadOnlySpan<byte> left,
		ReadOnlySpan<byte> right
	) {
		var count = Math.Min( left.Length, right.Length );
		for ( var index = 0; index < count; index++ ) {
			var leftByte = FoldAscii( left[index] );
			var rightByte = FoldAscii( right[index] );
			if ( leftByte != rightByte ) {
				return leftByte < rightByte ? -1 : 1;
			}
		}
		return left.Length.CompareTo( right.Length );
	}

	private static byte FoldAscii( byte value ) {
		return value is >= (byte)'A' and <= (byte)'Z'
			? (byte)( value + ( (byte)'a' - (byte)'A' ) )
			: value;
	}
}
