using Icod.CoreUtils.Shared.FileSystem.RecursiveMutation;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.RecursiveMutation;

/// <summary>Tests deterministic partial-operation cleanup.</summary>
public sealed class RecursiveCleanupJournalTests {
	/// <summary>Verifies reverse-order execution and continuation after one failure.</summary>
	[Fact]
	public async Task RollsBackInReverseOrderAndContinuesAfterFailure() {
		var observed = new List<int>();
		var journal = new RecursiveCleanupJournal();
		journal.Register( "first", _ => {
			observed.Add( 1 );
			return ValueTask.CompletedTask;
		} );
		journal.Register( "second", _ => {
			observed.Add( 2 );
			throw new IOException( "failure" );
		} );
		journal.Register( "third", _ => {
			observed.Add( 3 );
			return ValueTask.CompletedTask;
		} );
		var report = await journal.RollbackAsync();
		Assert.Equal( new[] { 3, 2, 1 }, observed );
		Assert.Equal( 3, report.Attempted );
		Assert.Single( report.Failures );
		Assert.Equal( "second", report.Failures[0].Description );
	}

	/// <summary>Verifies that commit discards pending compensating actions.</summary>
	[Fact]
	public async Task CommitDiscardsCleanupActions() {
		var called = false;
		var journal = new RecursiveCleanupJournal();
		journal.Register( "action", _ => {
			called = true;
			return ValueTask.CompletedTask;
		} );
		journal.Commit();
		var report = await journal.RollbackAsync();
		Assert.False( called );
		Assert.Equal( 0, report.Attempted );
		Assert.True( report.Succeeded );
	}

	/// <summary>Verifies that a rolled-back scope cannot be reused for later operations.</summary>
	[Fact]
	public async Task RollbackCompletesTheScope() {
		var journal = new RecursiveCleanupJournal();
		journal.Register( "action", _ => ValueTask.CompletedTask );
		_ = await journal.RollbackAsync();
		Assert.Throws<InvalidOperationException>( () => journal.Register( "later", _ => ValueTask.CompletedTask ) );
	}
}
