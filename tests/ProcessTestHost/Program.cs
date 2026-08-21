namespace Icod.CoreUtils.ProcessTestHost;

/// <summary>
/// Provides the small repository-local child-process behaviors required by
/// Coreutils command integration tests.
/// </summary>
public static class Program {

	/// <summary>Runs the requested Coreutils test-host behavior.</summary>
	public static async Task<int> Main(
		string[] args
	) {
		ArgumentNullException.ThrowIfNull( args );

		if ( 0 == args.Length ) {
			return 2;
		}

		switch ( args[ 0 ] ) {
			case "exit":
				return 1 < args.Length
					&& int.TryParse(
						args[ 1 ],
						out var exitCode
					)
						? exitCode
						: 0
				;

			case "sleep":
				await Task.Delay(
					1 < args.Length
						&& int.TryParse(
							args[ 1 ],
							out var milliseconds
						)
							? milliseconds
							: 30000
				).ConfigureAwait( false );
				return 0;

			default:
				return 2;
		}
	}

}
