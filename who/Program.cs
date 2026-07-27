namespace Icod.CoreUtils.Who;

public static class Program {
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
