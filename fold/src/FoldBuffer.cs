namespace Icod.CoreUtils.Fold;

using Icod.CoreUtils.Shared.Text;

/// <summary>Retains exact text units for the current movable fold segment.</summary>
internal sealed class FoldBuffer {
	private readonly List<TextUnit> myUnits = new();

	/// <summary>Gets the retained source-byte count.</summary>
	internal int ByteCount { get; private set; }

	/// <summary>Gets the number of retained text units.</summary>
	internal int Count => this.myUnits.Count;

	/// <summary>Gets a retained text unit.</summary>
	/// <param name="index">The zero-based unit index.</param>
	/// <returns>The retained unit.</returns>
	internal TextUnit GetUnit( int index ) => this.myUnits[index];

	/// <summary>Adds an exact text unit.</summary>
	/// <param name="unit">The unit to retain.</param>
	internal void Add( TextUnit unit ) {
		this.myUnits.Add( unit );
		this.ByteCount = checked(this.ByteCount + unit.ByteCount);
	}

	/// <summary>Finds the final locale blank in the buffer.</summary>
	/// <param name="localeProvider">The locale classifier.</param>
	/// <returns>The zero-based index, or minus one when absent.</returns>
	internal int FindLastBlank( ITextLocaleProvider localeProvider ) {
		ArgumentNullException.ThrowIfNull( localeProvider );
		for ( var index = this.myUnits.Count - 1; 0 <= index; index-- ) {
			if ( localeProvider.IsBlank( this.myUnits[index] ) ) {
				return index;
			}
		}
		return -1;
	}

	/// <summary>Writes the requested prefix as exact source bytes.</summary>
	/// <param name="output">The byte destination.</param>
	/// <param name="count">The number of units to write.</param>
	/// <param name="scratch">A reusable four-byte unit buffer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	internal async Task WritePrefixAsync(
		Stream output,
		int count,
		byte[] scratch,
		CancellationToken cancellationToken
	) {
		for ( var index = 0; index < count; index++ ) {
			var byteCount = this.myUnits[index].CopyBytesTo( scratch );
			await output.WriteAsync( scratch.AsMemory( 0, byteCount ), cancellationToken ).ConfigureAwait( false );
		}
	}

	/// <summary>Removes a prefix after it has been written.</summary>
	/// <param name="count">The number of units to remove.</param>
	internal void RemovePrefix( int count ) {
		for ( var index = 0; index < count; index++ ) {
			this.ByteCount -= this.myUnits[index].ByteCount;
		}
		this.myUnits.RemoveRange( 0, count );
	}

	/// <summary>Clears every retained unit.</summary>
	internal void Clear() {
		this.myUnits.Clear();
		this.ByteCount = 0;
	}
}
