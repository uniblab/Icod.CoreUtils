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

namespace Icod.CoreUtils.Shared.FileSystem.Traversal;

using Icod.CommandFramework.FileSystem.Traversal;

/// <summary>
/// Expands command operands through the canonical CommandFramework pathname
/// traversal contract.
/// </summary>
public static class PathnameOperandExpander {

	/// <summary>
	/// Expands pathname operands while preserving operand order, unmatched
	/// literals, repetitions, and expansion provenance.
	/// </summary>
	/// <param name="operands">The operands which the command has classified as pathnames.</param>
	/// <param name="options">Optional pathname expansion behavior.</param>
	/// <param name="cancellationToken">A token used to cancel traversal.</param>
	/// <returns>The canonical pathname expansion result.</returns>
	public static async Task<PathnameOperandExpansionResult> ExpandAsync(
		IEnumerable<string> operands,
		PathnameExpansionOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			operands
		);

		var operandList = operands as IReadOnlyList<string>
			?? operands.ToArray()
		;
		var expander = new PathnameExpander(
			SystemReadOnlyFileSystemProvider.Instance
		);
		var operandOptions = new PathnameOperandExpansionOptions {
			ExpansionOptions = options ?? new PathnameExpansionOptions {
				SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly
			}
		};
		return await expander.ExpandOperandsAsync(
			operandList,
			operandOptions,
			cancellationToken
		).ConfigureAwait( false );
	}

}
