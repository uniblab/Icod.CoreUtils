using Icod.CoreUtils.Shared.FileSystem.TransactionalReplacement;
using Xunit;

namespace Icod.CoreUtils.Shared.Tests.FileSystem.TransactionalReplacement;

/// <summary>Tests GNU-compatible E6 backup-name selection.</summary>
public sealed class TransactionalBackupNameGeneratorTests {
	/// <summary>Verifies that existing mode uses the simple suffix when no numbered backup exists.</summary>
	[Fact]
	public async Task ExistingModeUsesSimpleNameWithoutNumberedSiblings() {
		var generator = new TransactionalBackupNameGenerator();
		var policy = CreateExistingPolicy();
		var result = await generator.GenerateAsync(
			"target",
			policy,
			static (_, _) => ValueTask.FromResult( false ),
			static (_, _, _) => ValueTask.FromResult( false )
		);
		Assert.Equal( "target~", result );
	}

	/// <summary>Verifies that existing mode uses the first free numbered name after numbered siblings appear.</summary>
	[Fact]
	public async Task ExistingModeUsesFirstUnusedNumberedName() {
		var generator = new TransactionalBackupNameGenerator();
		var policy = CreateExistingPolicy();
		var result = await generator.GenerateAsync(
			"target",
			policy,
			static (path, _) => ValueTask.FromResult( "target.~1~" == path ),
			static (_, _, _) => ValueTask.FromResult( true )
		);
		Assert.Equal( "target.~2~", result );
	}

	private static TransactionalReplacementBackupPolicy CreateExistingPolicy() {
		return new TransactionalReplacementBackupPolicy {
			Mode = TransactionalReplacementBackupMode.Existing,
			Retention = TransactionalReplacementBackupRetention.RetainAfterSuccess,
			MaximumNumberedBackup = 10
		};
	}
}
