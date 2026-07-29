namespace Icod.CoreUtils.Unexpand;

using Icod.CoreUtils.Shared.Text;

/// <summary>Retains exact blank units until GNU <c>unexpand</c> can decide their replacement.</summary>
internal sealed class PendingBlankBuffer {
	private static readonly byte[] ourTab = new[] { (byte)'\t' };
	private readonly List<TextUnit> myUnits = new();
	private bool myReplaceFirstWithTab;

	/// <summary>Gets the number of retained blank units.</summary>
	internal int Count => this.myUnits.Count;

	/// <summary>Gets whether no pending blank units are retained.</summary>
	internal bool IsEmpty => 0 == this.myUnits.Count;

	/// <summary>Adds one exact blank unit.</summary>
	/// <param name="unit">The source unit.</param>
	internal void Add( TextUnit unit ) {
		this.myUnits.Add( unit );
	}

	/// <summary>Marks the first retained blank for output as a horizontal tab.</summary>
	internal void ReplaceFirstWithTab() {
		if ( !this.IsEmpty ) {
			this.myReplaceFirstWithTab = true;
		}
	}

	/// <summary>Keeps only the first retained unit, or clears the buffer.</summary>
	/// <param name="keepFirst">Whether the first unit must remain pending.</param>
	internal void KeepFirstOrClear( bool keepFirst ) {
		if ( !keepFirst ) {
			this.Clear();
			return;
		}
		if ( 1 < this.myUnits.Count ) {
			this.myUnits.RemoveRange( 1, this.myUnits.Count - 1 );
		}
	}

	/// <summary>Writes all pending units, optionally converting the first to a tab, and clears the buffer.</summary>
	/// <param name="output">The byte destination.</param>
	/// <param name="scratch">A reusable four-byte text-unit buffer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	internal async Task WriteAsync(
		Stream output,
		byte[] scratch,
		CancellationToken cancellationToken
	) {
		for ( var index = 0; index < this.myUnits.Count; index++ ) {
			if ( 0 == index && this.myReplaceFirstWithTab ) {
				await output.WriteAsync( ourTab.AsMemory(), cancellationToken ).ConfigureAwait( false );
				continue;
			}
			var count = this.myUnits[index].CopyBytesTo( scratch );
			await output.WriteAsync( scratch.AsMemory( 0, count ), cancellationToken ).ConfigureAwait( false );
		}
		this.Clear();
	}

	/// <summary>Discards all retained state.</summary>
	internal void Clear() {
		this.myUnits.Clear();
		this.myReplaceFirstWithTab = false;
	}
}
