namespace Icod.CoreUtils.Sed;

using System.Threading.Tasks;

public static class Program {

	public static async Task<int> Main(
		string[] args
	) {
		return await Command.RunAsync(
			args
		).ConfigureAwait( false );
	}

}