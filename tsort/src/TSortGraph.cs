namespace Icod.CoreUtils.TSort;

using Icod.CommandFramework.IO;

/// <summary>
/// Stores token relations and emits the GNU-compatible deterministic
/// topological order, reporting and breaking loops when necessary.
/// </summary>
internal sealed class TSortGraph {
	private static readonly byte[] LineTerminator = System.Text.Encoding.UTF8.GetBytes( Environment.NewLine );
	private readonly SortedDictionary<byte[], Node> myNodes = new( ByteSequenceComparer.Instance );

	/// <summary>Adds a relation in which <paramref name="predecessor"/> precedes <paramref name="successor"/>.</summary>
	/// <param name="predecessor">The predecessor token.</param>
	/// <param name="successor">The successor token.</param>
	/// <remarks>An equal-token pair declares a node but does not create a self-edge.</remarks>
	internal void AddRelation( byte[] predecessor, byte[] successor ) {
		ArgumentNullException.ThrowIfNull( predecessor );
		ArgumentNullException.ThrowIfNull( successor );
		var first = this.Intern( predecessor );
		var second = this.Intern( successor );
		if ( ReferenceEquals( first, second ) ) {
			return;
		}
		checked {
			second.IncomingCount++;
		}
		first.Top = new Edge( second, first.Top );
	}

	/// <summary>Writes the deterministic order and reports every loop needed to complete it.</summary>
	/// <param name="standardOutput">The byte-oriented standard-output destination.</param>
	/// <param name="standardError">The byte-oriented standard-error destination.</param>
	/// <param name="programName">The diagnostic program name.</param>
	/// <param name="sourceName">The diagnostic input operand.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> when at least one input loop was encountered; otherwise <see langword="false"/>.</returns>
	internal async Task<bool> WriteAsync(
		ByteOutputStream standardOutput,
		ByteOutputStream standardError,
		string programName,
		string sourceName,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( standardOutput );
		ArgumentNullException.ThrowIfNull( standardError );
		ArgumentException.ThrowIfNullOrEmpty( programName );
		ArgumentNullException.ThrowIfNull( sourceName );

		var remaining = this.myNodes.Count;
		var encounteredLoop = false;
		var zeros = new Queue<Node>();
		while ( 0 < remaining ) {
			cancellationToken.ThrowIfCancellationRequested();
			foreach ( var node in this.myNodes.Values ) {
				if ( !node.Printed && 0 == node.IncomingCount && !node.Queued ) {
					node.Queued = true;
					zeros.Enqueue( node );
				}
			}

			while ( 0 < zeros.Count ) {
				cancellationToken.ThrowIfCancellationRequested();
				var node = zeros.Dequeue();
				node.Queued = false;
				if ( node.Printed ) {
					continue;
				}
				await standardOutput.WriteAsync( node.Value.AsMemory(), cancellationToken ).ConfigureAwait( false );
				await standardOutput.WriteAsync( LineTerminator.AsMemory(), cancellationToken ).ConfigureAwait( false );
				node.Printed = true;
				remaining--;

				for ( var relation = node.Top; null != relation; relation = relation.Next ) {
					cancellationToken.ThrowIfCancellationRequested();
					relation.Target.IncomingCount--;
					if ( 0 == relation.Target.IncomingCount && !relation.Target.Printed && !relation.Target.Queued ) {
						relation.Target.Queued = true;
						zeros.Enqueue( relation.Target );
					}
				}
			}

			if ( 0 < remaining ) {
				encounteredLoop = true;
				await standardError.WriteTextAsync(
					string.Concat( programName, ": ", sourceName, ": input contains a loop:", Environment.NewLine ),
					cancellationToken
				).ConfigureAwait( false );
				await this.DetectAndBreakLoopAsync(
					standardError,
					programName,
					cancellationToken
				).ConfigureAwait( false );
			}
		}
		return encounteredLoop;
	}

	private async Task DetectAndBreakLoopAsync(
		ByteOutputStream standardError,
		string programName,
		CancellationToken cancellationToken
	) {
		Node? loop = null;
		while ( true ) {
			foreach ( var node in this.myNodes.Values ) {
				cancellationToken.ThrowIfCancellationRequested();
				if ( node.IncomingCount <= 0 ) {
					continue;
				}
				if ( null == loop ) {
					loop = node;
					continue;
				}

				Edge? previous = null;
				for ( var relation = node.Top; null != relation; relation = relation.Next ) {
					if ( ReferenceEquals( relation.Target, loop ) ) {
						if ( null != node.LoopLink ) {
							while ( null != loop ) {
								var next = loop.LoopLink;
								await WriteLoopMemberAsync(
									standardError,
									programName,
									loop.Value,
									cancellationToken
								).ConfigureAwait( false );
								if ( ReferenceEquals( loop, node ) ) {
									relation.Target.IncomingCount--;
									if ( null == previous ) {
										node.Top = relation.Next;
									} else {
										previous.Next = relation.Next;
									}
									break;
								}
								loop.LoopLink = null;
								loop = next;
							}
							while ( null != loop ) {
								var next = loop.LoopLink;
								loop.LoopLink = null;
								loop = next;
							}
							return;
						}
						node.LoopLink = loop;
						loop = node;
						break;
					}
					previous = relation;
				}
			}
		}
	}

	private static async ValueTask WriteLoopMemberAsync(
		ByteOutputStream standardError,
		string programName,
		byte[] token,
		CancellationToken cancellationToken
	) {
		await standardError.WriteTextAsync(
			string.Concat( programName, ": " ),
			cancellationToken
		).ConfigureAwait( false );
		await standardError.WriteAsync( token.AsMemory(), cancellationToken ).ConfigureAwait( false );
		await standardError.WriteAsync( LineTerminator.AsMemory(), cancellationToken ).ConfigureAwait( false );
	}

	private Node Intern( byte[] token ) {
		var normalized = NormalizeToken( token );
		if ( this.myNodes.TryGetValue( normalized, out var existing ) ) {
			return existing;
		}
		var node = new Node( normalized );
		this.myNodes.Add( normalized, node );
		return node;
	}

	private static byte[] NormalizeToken( byte[] token ) {
		var nul = Array.IndexOf( token, (byte)0 );
		if ( nul < 0 ) {
			return token;
		}
		return token.AsSpan( 0, nul ).ToArray();
	}

	private sealed class ByteSequenceComparer : IComparer<byte[]> {
		/// <summary>Gets the singleton byte-sequence comparer.</summary>
		internal static readonly ByteSequenceComparer Instance = new();

		/// <inheritdoc/>
		public int Compare( byte[]? left, byte[]? right ) {
			if ( ReferenceEquals( left, right ) ) {
				return 0;
			}
			if ( null == left ) {
				return -1;
			}
			if ( null == right ) {
				return 1;
			}
			var length = Math.Min( left.Length, right.Length );
			for ( var index = 0; index < length; index++ ) {
				var comparison = left[ index ].CompareTo( right[ index ] );
				if ( 0 != comparison ) {
					return comparison;
				}
			}
			return left.Length.CompareTo( right.Length );
		}
	}

	private sealed class Edge {
		/// <summary>Initializes a successor relation.</summary>
		/// <param name="target">The relation target.</param>
		/// <param name="next">The next relation in reverse input order.</param>
		internal Edge( Node target, Edge? next ) {
			this.Target = target;
			this.Next = next;
		}
		/// <summary>Gets or sets the next successor relation.</summary>
		internal Edge? Next { get; set; }

		/// <summary>Gets the relation target.</summary>
		internal Node Target { get; }
	}

	private sealed class Node {
		/// <summary>Initializes a graph node.</summary>
		/// <param name="value">The canonical byte token.</param>
		internal Node( byte[] value ) {
			this.Value = value;
		}
		/// <summary>Gets or sets the number of active incoming relations.</summary>
		internal int IncomingCount { get; set; }

		/// <summary>Gets or sets the predecessor link used while discovering a loop.</summary>
		internal Node? LoopLink { get; set; }

		/// <summary>Gets or sets whether the node has been written.</summary>
		internal bool Printed { get; set; }

		/// <summary>Gets or sets whether the node is currently in the zero queue.</summary>
		internal bool Queued { get; set; }

		/// <summary>Gets or sets the head of the successor relation list.</summary>
		internal Edge? Top { get; set; }

		/// <summary>Gets the canonical byte token.</summary>
		internal byte[] Value { get; }
	}
}
