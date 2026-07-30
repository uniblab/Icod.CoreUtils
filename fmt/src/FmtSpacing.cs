namespace Icod.CoreUtils.Fmt;

/// <summary>Writes GNU <c>fmt</c> indentation using spaces and optional tab reintroduction.</summary>
internal static class FmtSpacing {
	private static readonly byte[] ourSpace = [ (byte)' ' ];
	private static readonly byte[] ourTab = [ (byte)'\t' ];

	/// <summary>Writes horizontal white space from one column to a target column.</summary>
	/// <param name="output">The destination stream.</param>
	/// <param name="useTabs">Whether equivalent tab characters may be generated.</param>
	/// <param name="column">The current output column.</param>
	/// <param name="target">The target output column.</param>
	/// <param name="cancellationToken">A token that can cancel asynchronous writes.</param>
	/// <returns>The resulting output column.</returns>
	internal static async ValueTask<int> WriteToColumnAsync(
		Stream output,
		bool useTabs,
		int column,
		int target,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( output );
		if ( target <= column ) {
			return column;
		}
		if ( useTabs ) {
			var tabTarget = (target / 8) * 8;
			if ( column + 1 < tabTarget ) {
				while ( column < tabTarget ) {
					await output.WriteAsync( ourTab, cancellationToken ).ConfigureAwait( false );
					column = checked((column / 8 + 1) * 8);
				}
			}
		}
		while ( column < target ) {
			await output.WriteAsync( ourSpace, cancellationToken ).ConfigureAwait( false );
			column++;
		}
		return column;
	}
}
