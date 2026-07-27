namespace Icod.CoreUtils.Users;

public static class Program {
	public static Task<int> Main( string[] args ) => Command.RunAsync( args );
}
