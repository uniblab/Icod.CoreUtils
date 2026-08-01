namespace Icod.DiffUtils.SDiff;

using Icod.DiffUtils.Shared.Lines;

/// <summary>Edits a temporary merge fragment for an interactive <c>sdiff</c> command.</summary>
public interface ISDiffEditor {
	/// <summary>Edits the supplied initial fragment and returns the resulting logical lines.</summary>
	/// <param name="initialContent">The initial temporary-file contents.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task whose result is the edited logical-line sequence.</returns>
	Task<IReadOnlyList<ComparisonLine>> EditAsync(
		IReadOnlyList<ComparisonLine> initialContent,
		CancellationToken cancellationToken = default
	);
}
