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

	/// <summary>
	/// Expands only operands which contain pathname metacharacters while preserving
	/// every literal operand exactly as supplied by the caller.
	/// </summary>
	/// <param name="operands">The operands which the command has classified as pathnames.</param>
	/// <param name="options">Optional pathname expansion behavior.</param>
	/// <param name="cancellationToken">A token used to cancel traversal.</param>
	/// <returns>The ordered pathname operands after wildcard expansion.</returns>
	public static async Task<IReadOnlyList<string>> ExpandPatternsPreservingLiteralsAsync(
		IEnumerable<string> operands,
		PathnameExpansionOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			operands
		);

		var expansionOptions = options ?? new PathnameExpansionOptions {
			SymbolicLinkMode = SymbolicLinkTraversalMode.RootsOnly
		};
		var expandedOperands = new List<string>();
		foreach ( var operand in operands ) {
			ArgumentNullException.ThrowIfNull(
				operand
			);
			cancellationToken.ThrowIfCancellationRequested();

			var pattern = PathnamePattern.Parse(
				operand,
				expansionOptions.PatternOptions
			);
			if ( !pattern.HasMetacharacters ) {
				expandedOperands.Add(
					operand
				);
				continue;
			}

			var expansion = await ExpandAsync(
				new[] { operand },
				expansionOptions,
				cancellationToken
			).ConfigureAwait( false );
			expandedOperands.AddRange(
				expansion.Operands
			);
		}
		return expandedOperands.AsReadOnly();
	}

	/// <summary>
	/// Expands pathname operands through an injected read-only filesystem provider.
	/// </summary>
	/// <param name="operands">The operands which the command has classified as pathnames.</param>
	/// <param name="provider">The read-only filesystem provider used for expansion.</param>
	/// <param name="options">Optional pathname expansion behavior.</param>
	/// <param name="cancellationToken">A token used to cancel traversal.</param>
	/// <returns>The canonical pathname expansion result.</returns>
	public static async Task<PathnameOperandExpansionResult> ExpandAsync(
		IEnumerable<string> operands,
		IReadOnlyFileSystemProvider provider,
		PathnameExpansionOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			operands
		);
		ArgumentNullException.ThrowIfNull(
			provider
		);

		var operandList = operands as IReadOnlyList<string>
			?? operands.ToArray()
		;
		var expander = new PathnameExpander(
			provider
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
