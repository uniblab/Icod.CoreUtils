namespace Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;

/// <summary>Describes one rollback or cleanup failure.</summary>
/// <param name="Description">The caller-supplied description of the compensating action.</param>
/// <param name="Message">The controlled failure message.</param>
/// <param name="Exception">The underlying exception.</param>
public sealed record RecursiveCleanupFailure(
	string Description,
	string Message,
	Exception? Exception
);

/// <summary>Describes deterministic reverse-order cleanup.</summary>
public sealed class RecursiveCleanupReport {
	/// <summary>Initializes a cleanup report.</summary>
	/// <param name="attempted">The number of compensating actions attempted.</param>
	/// <param name="failures">The cleanup failures in execution order.</param>
	public RecursiveCleanupReport( int attempted, IReadOnlyList<RecursiveCleanupFailure> failures ) {
		if ( 0 > attempted ) {
			throw new ArgumentOutOfRangeException( nameof( attempted ) );
		}
		ArgumentNullException.ThrowIfNull( failures );
		Attempted = attempted;
		Failures = Array.AsReadOnly( failures.ToArray() );
	}

	/// <summary>Gets the number of cleanup actions attempted.</summary>
	public int Attempted { get; }
	/// <summary>Gets cleanup failures in execution order.</summary>
	public IReadOnlyList<RecursiveCleanupFailure> Failures { get; }
	/// <summary>Gets whether every registered cleanup action succeeded.</summary>
	public bool Succeeded => Failures.Count == 0;
}

/// <summary>Defines the rollback seam that later E6 replacement scopes may implement or wrap.</summary>
public interface IRecursiveRollbackScope {
	/// <summary>Gets the number of pending compensating actions.</summary>
	int Count { get; }

	/// <summary>Records one compensating action.</summary>
	/// <param name="description">A stable description used if cleanup fails.</param>
	/// <param name="action">The asynchronous compensating action.</param>
	void Register( string description, Func<CancellationToken, ValueTask> action );

	/// <summary>Commits the operation and discards pending compensating actions.</summary>
	void Commit();

	/// <summary>Executes pending compensating actions in reverse order.</summary>
	/// <param name="cancellationToken">The token supplied to each compensating action.</param>
	/// <returns>A report containing every attempted action and controlled cleanup failure.</returns>
	ValueTask<RecursiveCleanupReport> RollbackAsync( CancellationToken cancellationToken = default );
}

/// <summary>
/// Records compensating actions for partial recursive operations and exposes the rollback seam consumed by E6.
/// </summary>
public sealed class RecursiveCleanupJournal : IRecursiveRollbackScope {
	private readonly Stack<CleanupAction> _actions = new();
	private bool _completed;

	/// <summary>Gets the number of pending compensating actions.</summary>
	public int Count => _actions.Count;

	/// <summary>Records one compensating action.</summary>
	/// <param name="description">A stable description used if cleanup fails.</param>
	/// <param name="action">The asynchronous compensating action.</param>
	public void Register(
		string description,
		Func<CancellationToken, ValueTask> action
	) {
		ArgumentException.ThrowIfNullOrEmpty( description );
		ArgumentNullException.ThrowIfNull( action );
		if ( _completed ) {
			throw new InvalidOperationException( "A completed cleanup journal cannot accept new actions." );
		}
		_actions.Push( new CleanupAction( description, action ) );
	}

	/// <summary>Commits the operation and discards all compensating actions.</summary>
	public void Commit() {
		_completed = true;
		_actions.Clear();
	}

	/// <summary>Executes pending actions in reverse registration order and continues after failures.</summary>
	/// <param name="cancellationToken">The token supplied to each compensating action.</param>
	/// <returns>A report containing every attempted action and controlled cleanup failure.</returns>
	/// <remarks>
	/// Cancellation thrown by one cleanup action is recorded as a cleanup failure so later compensating actions
	/// still receive an opportunity to restore invariants.
	/// </remarks>
	public async ValueTask<RecursiveCleanupReport> RollbackAsync(
		CancellationToken cancellationToken = default
	) {
		if ( _completed ) {
			return new RecursiveCleanupReport( 0, Array.Empty<RecursiveCleanupFailure>() );
		}
		_completed = true;
		var attempted = 0;
		var failures = new List<RecursiveCleanupFailure>();
		while ( _actions.TryPop( out var item ) ) {
			attempted++;
			try {
				await item.Action( cancellationToken ).ConfigureAwait( false );
			} catch ( Exception exception ) {
				failures.Add( new RecursiveCleanupFailure( item.Description, exception.Message, exception ) );
			}
		}
		return new RecursiveCleanupReport( attempted, failures );
	}

	private sealed record CleanupAction(
		string Description,
		Func<CancellationToken, ValueTask> Action
	);
}
