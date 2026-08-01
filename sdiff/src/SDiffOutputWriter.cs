namespace Icod.DiffUtils.SDiff;

using Icod.DiffUtils.Shared.Layout;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Writes deterministic side-by-side rows independently of terminal capabilities.</summary>
internal sealed class SDiffOutputWriter {
	private readonly TextWriter output;
	private readonly SDiffOptions options;
	private readonly CancellationToken cancellationToken;

	/// <summary>Initializes an output writer.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="options">The validated command options.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SDiffOutputWriter( TextWriter output, SDiffOptions options, CancellationToken cancellationToken ) {
		this.output = output ?? throw new ArgumentNullException( nameof( output ) );
		this.options = options ?? throw new ArgumentNullException( nameof( options ) );
		this.cancellationToken = cancellationToken;
	}

	/// <summary>Writes every comparison group.</summary>
	/// <param name="comparison">The aligned comparison to write.</param>
	/// <returns>A task representing the output operation.</returns>
	public async Task WriteComparisonAsync( SDiffComparison comparison ) {
		ArgumentNullException.ThrowIfNull( comparison );
		foreach ( var group in comparison.Groups ) {
			await this.WriteGroupAsync( group, showCommon: true ).ConfigureAwait( false );
		}
	}

	/// <summary>Writes one common or differing group.</summary>
	/// <param name="group">The group to write.</param>
	/// <param name="showCommon">Whether common rows are currently verbose.</param>
	/// <returns>A task representing the output operation.</returns>
	public async Task WriteGroupAsync( SDiffGroup group, bool showCommon ) {
		ArgumentNullException.ThrowIfNull( group );
		foreach ( var row in group.Rows ) {
			this.cancellationToken.ThrowIfCancellationRequested();
			if ( row.IsCommon && ( !showCommon || this.options.SuppressCommonLines ) ) {
				continue;
			}
			await this.WriteRowAsync( row ).ConfigureAwait( false );
		}
	}

	private async ValueTask WriteRowAsync( SDiffRow row ) {
		if ( row.IsCommon && this.options.LeftColumn && null == row.Left ) {
			return;
		}
		var left = GetOutputText( row.Left );
		var right = GetOutputText( row.Right );
		var marker = row.Marker;
		if ( row.IsCommon && this.options.LeftColumn ) {
			marker = '(';
			right = null;
		}
		var line = SideBySideLayout.FormatRow(
			left,
			marker,
			right,
			this.options.Width,
			this.options.TabSize,
			this.options.ExpandTabs
		);
		var terminated = true == row.Left?.HasLineTerminator || true == row.Right?.HasLineTerminator;
		await this.output.WriteAsync( line.AsMemory(), this.cancellationToken ).ConfigureAwait( false );
		if ( terminated ) {
			await this.output.WriteLineAsync( ReadOnlyMemory<char>.Empty, this.cancellationToken ).ConfigureAwait( false );
		}
	}

	private string? GetOutputText( ComparisonLine? line ) {
		if ( null == line ) {
			return null;
		}
		var value = line.Content;
		if ( this.options.ComparisonPolicy.StripTrailingCarriageReturn && value.EndsWith( '\r' ) ) {
			value = value[..^1];
		}
		return value;
	}
}
