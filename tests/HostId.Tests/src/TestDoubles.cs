namespace Icod.CoreUtils.HostId.Tests;

using Icod.CoreUtils.Shared.Host;

/// <summary>Supplies deterministic host-identifier observations to tests.</summary>
public sealed class TestHostIdentifierProvider : IHostIdentifierProvider {
	private readonly Func<CancellationToken, ValueTask<HostResourceValue<HostIdentifier>>> factory;

	/// <summary>Initializes a provider from one observation factory.</summary>
	/// <param name="factory">The observation factory.</param>
	public TestHostIdentifierProvider(
		Func<CancellationToken, ValueTask<HostResourceValue<HostIdentifier>>> factory
	) {
		this.factory = factory ?? throw new ArgumentNullException( nameof( factory ) );
	}

	/// <summary>Gets the number of provider calls.</summary>
	public int CallCount { get; private set; }

	/// <inheritdoc />
	public ValueTask<HostResourceValue<HostIdentifier>> GetHostIdentifierAsync(
		CancellationToken cancellationToken = default
	) {
		CallCount++;
		return factory( cancellationToken );
	}
}
