namespace Icod.CoreUtils.WhoAmI;

public static class Program {
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
