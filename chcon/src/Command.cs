// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.ChCon;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;

/// <summary>GNU-compatible SELinux file-context manipulation front end.</summary>
public static class Command {
	private const int Failure = 1;
	private const int ENoData = 61;

	private enum TraversalPolicy {
		Physical,
		CommandLine,
		Logical
	}

	private sealed class Options {
		public bool Recursive { get; set; }
		public bool Verbose { get; set; }
		public bool PreserveRoot { get; set; }
		public bool? Dereference { get; set; }
		public TraversalPolicy Traversal { get; set; } = TraversalPolicy.Physical;
		public string? Reference { get; set; }
		public string? User { get; set; }
		public string? Role { get; set; }
		public string? Type { get; set; }
		public string? Range { get; set; }
		public List<string> Operands { get; } = new();
		public bool HasComponents => User is not null || Role is not null || Type is not null || Range is not null;
	}

	/// <summary>Runs <c>chcon</c> synchronously.</summary>
	public static int Run( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null, ISelinuxPlatform? platform = null, CancellationToken cancellationToken = default ) {
		return RunAsync( args, stdin, stdout, stderr, platform, cancellationToken ).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>chcon</c> through an injectable SELinux provider.</summary>
	public static ValueTask<int> RunAsync( string[] args, TextReader? stdin = null, TextWriter? stdout = null, TextWriter? stderr = null, ISelinuxPlatform? platform = null, CancellationToken cancellationToken = default ) {
		_ = stdin;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( TryHandleInformationalOption( args, stdout, out var informationalStatus ) )
			return ValueTask.FromResult( informationalStatus );

		if ( !TryParse( args, stderr, out var options ) )
			return ValueTask.FromResult( Failure );

		if ( cancellationToken.IsCancellationRequested ) {
			stderr.WriteLine( "chcon: operation canceled" );
			return ValueTask.FromResult( Failure );
		}

		platform ??= new NativeSelinuxPlatform();
		if ( !EnsureSelinux( platform, stderr ) )
			return ValueTask.FromResult( Failure );

		return ExecuteAsync( options, stdout, stderr, platform, cancellationToken );
	}

	private static async ValueTask<int> ExecuteAsync( Options options, TextWriter stdout, TextWriter stderr, ISelinuxPlatform platform, CancellationToken cancellationToken ) {
		string? fixedContext = null;
		var validateFixedContext = false;
		var operands = options.Operands;

		if ( options.Reference is not null ) {
			if ( !platform.TryGetFileContext( options.Reference, true, out fixedContext, out var referenceError ) ) {
				stderr.WriteLine( $"chcon: failed to get security context of '{options.Reference}': {platform.DescribeError( referenceError )}" );
				return Failure;
			}
		} else if ( !options.HasComponents ) {
			fixedContext = operands[0];
			validateFixedContext = true;
			operands = operands.GetRange( 1, operands.Count - 1 );
		}

		if ( validateFixedContext && fixedContext is not null && !platform.TryValidateContext( fixedContext, out var validationError ) ) {
			stderr.WriteLine( $"chcon: invalid context '{fixedContext}': {platform.DescribeError( validationError )}" );
			return Failure;
		}

		var dereference = options.Dereference ?? ( options.Recursive ? options.Traversal != TraversalPolicy.Physical : true );
		if ( !options.Recursive ) {
			var nonrecursiveFailure = false;
			foreach ( var operand in operands ) {
				if ( cancellationToken.IsCancellationRequested ) {
					stderr.WriteLine( "chcon: operation canceled" );
					return Failure;
				}
				if ( !ApplyContext( operand, dereference, options, fixedContext, platform, stdout, stderr ) )
					nonrecursiveFailure = true;
			}
			return nonrecursiveFailure ? Failure : 0;
		}

		var failed = false;
		var roots = new List<PathTraversalRoot>();
		var fileSystem = SystemReadOnlyFileSystemProvider.Instance;
		try {
			for ( var i = 0; i < operands.Count; i++ ) {
				var operand = operands[i];
				if ( cancellationToken.IsCancellationRequested ) {
					stderr.WriteLine( "chcon: operation canceled" );
					return Failure;
				}
				if ( string.IsNullOrEmpty( operand ) ) {
					stderr.WriteLine( "chcon: cannot access '': invalid empty pathname" );
					failed = true;
					continue;
				}
				if ( options.PreserveRoot && await IsRootOperandAsync( operand, options.Traversal, fileSystem, cancellationToken ).ConfigureAwait( false ) ) {
					stderr.WriteLine( $"chcon: it is dangerous to operate recursively on '{operand}'" );
					stderr.WriteLine( "chcon: use --no-preserve-root to override this failsafe" );
					failed = true;
					continue;
				}
				roots.Add( new PathTraversalRoot( operand, i, roots.Count, operand, operand, PathTraversalRootKind.Literal ) );
			}
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			stderr.WriteLine( "chcon: operation canceled" );
			return Failure;
		}

		var traversalOptions = new PathTraversalOptions {
			SymbolicLinkMode = options.Traversal switch {
				TraversalPolicy.CommandLine => SymbolicLinkTraversalMode.RootsOnly,
				TraversalPolicy.Logical => SymbolicLinkTraversalMode.Always,
				_ => SymbolicLinkTraversalMode.Never
			},
			FileSystemBoundaryMode = FileSystemBoundaryMode.CrossFileSystems,
			ChildOrder = PathTraversalChildOrder.Provider,
			ErrorMode = PathTraversalErrorMode.Continue
		};
		var traversal = new ReadOnlyPathTraversalEngine( fileSystem );
		try {
			await foreach ( var item in traversal.TraverseAsync( roots, traversalOptions, cancellationToken ).ConfigureAwait( false ) ) {
				switch ( item.Kind ) {
					case PathTraversalEventKind.Entry:
					case PathTraversalEventKind.LeaveDirectory:
						if ( item.Entry is not null && !ApplyContext( item.Entry.AccessPath, dereference, options, fixedContext, platform, stdout, stderr ) )
							failed = true;
						break;
					case PathTraversalEventKind.Error:
						if ( item.Error is not null ) {
							var detail = item.Error.Exception?.Message ?? item.Error.Message;
							stderr.WriteLine( $"chcon: cannot access '{item.Error.Path}': {detail}" );
						}
						failed = true;
						break;
					case PathTraversalEventKind.Cycle:
						stderr.WriteLine( $"chcon: detected recursive directory cycle at '{item.Entry?.AccessPath ?? item.Root.AccessPath}'" );
						failed = true;
						break;
				}
			}
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			stderr.WriteLine( "chcon: operation canceled" );
			return Failure;
		}
		return failed ? Failure : 0;
	}

	private static bool ApplyContext( string path, bool dereference, Options options, string? fixedContext, ISelinuxPlatform platform, TextWriter stdout, TextWriter stderr ) {
		var context = fixedContext;
		if ( context is null ) {
			if ( !platform.TryGetFileContext( path, dereference, out var current, out var getError ) ) {
				if ( getError == ENoData )
					stderr.WriteLine( $"chcon: can't apply partial context to unlabeled file '{path}'" );
				else
					stderr.WriteLine( $"chcon: failed to get security context of '{path}': {platform.DescribeError( getError )}" );
				return false;
			}
			if ( !SelinuxContext.TryParse( current, out var parsed ) || parsed is null ) {
				stderr.WriteLine( $"chcon: failed to parse security context of '{path}'" );
				return false;
			}

			context = new SelinuxContext(
				options.User ?? parsed.User,
				options.Role ?? parsed.Role,
				options.Type ?? parsed.Type,
				options.Range ?? parsed.Range ).ToString();
			if ( !platform.TryValidateContext( context, out var validationError ) ) {
				stderr.WriteLine( $"chcon: invalid context '{context}': {platform.DescribeError( validationError )}" );
				return false;
			}
		}

		if ( options.Verbose )
			stdout.WriteLine( $"changing security context of '{path}'" );
		if ( platform.TrySetFileContext( path, context, dereference, out var setError ) )
			return true;

		stderr.WriteLine( $"chcon: failed to change context of '{path}' to '{context}': {platform.DescribeError( setError )}" );
		return false;
	}

	private static bool TryParse( string[] args, TextWriter stderr, out Options options ) {
		options = new Options();
		var endOptions = false;
		for ( var i = 0; i < args.Length; i++ ) {
			var arg = args[i];
			if ( endOptions || arg.Length == 0 || arg[0] != '-' || arg == "-" ) {
				options.Operands.Add( arg );
				continue;
			}
			if ( arg == "--" ) {
				endOptions = true;
				continue;
			}

			if ( arg.StartsWith( "--", StringComparison.Ordinal ) ) {
				if ( !ParseLongOption( args, ref i, arg, options, stderr ) )
					return false;
				continue;
			}

			if ( !ParseShortOptions( args, ref i, arg, options, stderr ) )
				return false;
		}

		if ( options.Reference is not null && options.HasComponents )
			return Error( stderr, "conflicting security context specifiers given" );
		if ( options.Reference is not null && options.Operands.Count == 0 )
			return Error( stderr, "missing file operand" );
		if ( options.Reference is null && options.HasComponents && options.Operands.Count == 0 )
			return Error( stderr, "missing file operand" );
		if ( options.Reference is null && !options.HasComponents && options.Operands.Count < 2 )
			return Error( stderr, "missing operand after security context" );

		if ( options.Recursive && options.Dereference == true && options.Traversal == TraversalPolicy.Physical )
			return Error( stderr, "-R --dereference requires either -H or -L" );
		if ( options.Recursive && options.Dereference == false && options.Traversal != TraversalPolicy.Physical )
			return Error( stderr, "-R -h requires -P" );
		return true;
	}

	private static bool ParseLongOption( string[] args, ref int index, string arg, Options options, TextWriter stderr ) {
		var equals = arg.IndexOf( '=' );
		var name = equals < 0 ? arg : arg[..equals];
		var inlineValue = equals < 0 ? null : arg[( equals + 1 )..];
		switch ( name ) {
			case "--dereference": options.Dereference = true; return RequireNoInlineValue( inlineValue, name, stderr );
			case "--no-dereference": options.Dereference = false; return RequireNoInlineValue( inlineValue, name, stderr );
			case "--recursive": options.Recursive = true; return RequireNoInlineValue( inlineValue, name, stderr );
			case "--verbose": options.Verbose = true; return RequireNoInlineValue( inlineValue, name, stderr );
			case "--preserve-root": options.PreserveRoot = true; return RequireNoInlineValue( inlineValue, name, stderr );
			case "--no-preserve-root": options.PreserveRoot = false; return RequireNoInlineValue( inlineValue, name, stderr );
			case "--reference": return TryTakeValue( args, ref index, inlineValue, name, stderr, value => options.Reference = value );
			case "--user": return TryTakeValue( args, ref index, inlineValue, name, stderr, value => options.User = value );
			case "--role": return TryTakeValue( args, ref index, inlineValue, name, stderr, value => options.Role = value );
			case "--type": return TryTakeValue( args, ref index, inlineValue, name, stderr, value => options.Type = value );
			case "--range": return TryTakeValue( args, ref index, inlineValue, name, stderr, value => options.Range = value );
			default: return Error( stderr, $"unrecognized option '{arg}'" );
		}
	}

	private static bool ParseShortOptions( string[] args, ref int index, string arg, Options options, TextWriter stderr ) {
		for ( var p = 1; p < arg.Length; p++ ) {
			var option = arg[p];
			switch ( option ) {
				case 'R': options.Recursive = true; break;
				case 'v': options.Verbose = true; break;
				case 'f': break; // GNU compatibility no-op retained by upstream source.
				case 'h': options.Dereference = false; break;
				case 'H': options.Traversal = TraversalPolicy.CommandLine; break;
				case 'L': options.Traversal = TraversalPolicy.Logical; break;
				case 'P': options.Traversal = TraversalPolicy.Physical; break;
				case 'u': case 'r': case 't': case 'l': {
					string value;
					if ( p + 1 < arg.Length ) {
						value = arg[( p + 1 )..];
						p = arg.Length;
					} else if ( index + 1 < args.Length ) {
						value = args[++index];
					} else {
						return Error( stderr, $"option requires an argument -- '{option}'" );
					}
					if ( option == 'u' ) options.User = value;
					else if ( option == 'r' ) options.Role = value;
					else if ( option == 't' ) options.Type = value;
					else options.Range = value;
					break;
				}
				default: return Error( stderr, $"invalid option -- '{option}'" );
			}
		}
		return true;
	}

	private static bool TryTakeValue( string[] args, ref int index, string? inlineValue, string name, TextWriter stderr, Action<string> setter ) {
		var value = inlineValue;
		if ( value is null ) {
			if ( index + 1 >= args.Length )
				return Error( stderr, $"option '{name}' requires an argument" );
			value = args[++index];
		}
		if ( value.Length == 0 )
			return Error( stderr, $"option '{name}' requires a non-empty argument" );
		setter( value );
		return true;
	}

	private static bool RequireNoInlineValue( string? value, string name, TextWriter stderr ) {
		return value is null || Error( stderr, $"option '{name}' doesn't allow an argument" );
	}

	private static bool EnsureSelinux( ISelinuxPlatform platform, TextWriter stderr ) {
		if ( !platform.IsSupported ) {
			stderr.WriteLine( $"chcon: {platform.UnsupportedReason}" );
			return false;
		}
		if ( platform.IsEnabled( out var error ) )
			return true;
		stderr.WriteLine( $"chcon: SELinux is disabled or unavailable: {platform.DescribeError( error )}" );
		return false;
	}

	private static async ValueTask<bool> IsRootOperandAsync(
		string path,
		TraversalPolicy traversal,
		IReadOnlyFileSystemProvider fileSystem,
		CancellationToken cancellationToken
	) {
		try {
			var full = System.IO.Path.TrimEndingDirectorySeparator( System.IO.Path.GetFullPath( path ) );
			var lexicalRoot = System.IO.Path.TrimEndingDirectorySeparator( System.IO.Path.GetPathRoot( full ) ?? string.Empty );
			if ( string.Equals( full, lexicalRoot, StringComparison.Ordinal ) )
				return true;

			var root = await fileSystem.ObserveAsync( "/", true, cancellationToken ).ConfigureAwait( false );
			var candidate = await fileSystem.ObserveAsync( path, traversal != TraversalPolicy.Physical, cancellationToken ).ConfigureAwait( false );
			return root.EntryIdentity.IsAvailable
				&& candidate.EntryIdentity.IsAvailable
				&& root.EntryIdentity == candidate.EntryIdentity;
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			throw;
		} catch {
			// Normal operand diagnostics will report inaccessible paths later.
			return false;
		}
	}

	private static bool TryHandleInformationalOption( string[] args, TextWriter stdout, out int status ) {
		status = 0;
		for ( var i = 0; i < args.Length; i++ ) {
			var arg = args[i];
			if ( arg == "--" )
				break;
			if ( arg == "--help" || arg == "-?" ) {
				PrintHelp( stdout );
				return true;
			}
			if ( arg == "--version" ) {
				stdout.WriteLine( "chcon (Icod.CoreUtils) 9.11-compatible" );
				return true;
			}

			if ( arg is "--reference" or "--user" or "--role" or "--type" or "--range" ) {
				if ( i + 1 < args.Length )
					i++;
				continue;
			}
			if ( arg.StartsWith( "--reference=", StringComparison.Ordinal )
				|| arg.StartsWith( "--user=", StringComparison.Ordinal )
				|| arg.StartsWith( "--role=", StringComparison.Ordinal )
				|| arg.StartsWith( "--type=", StringComparison.Ordinal )
				|| arg.StartsWith( "--range=", StringComparison.Ordinal )
				|| arg is "--dereference" or "--no-dereference" or "--recursive" or "--verbose" or "--preserve-root" or "--no-preserve-root" )
				continue;
			if ( arg.StartsWith( "--", StringComparison.Ordinal ) )
				return false;

			if ( arg.Length > 1 && arg[0] == '-' ) {
				for ( var p = 1; p < arg.Length; p++ ) {
					var option = arg[p];
					if ( option is 'R' or 'v' or 'f' or 'h' or 'H' or 'L' or 'P' )
						continue;
					if ( option is not ( 'u' or 'r' or 't' or 'l' ) )
						return false;
					if ( p + 1 == arg.Length && i + 1 < args.Length )
						i++;
					break;
				}
			}
		}
		return false;
	}

	private static bool Error( TextWriter stderr, string message ) {
		stderr.WriteLine( $"chcon: {message}" );
		stderr.WriteLine( "Try 'chcon --help' for more information." );
		return false;
	}

	private static void PrintHelp( TextWriter stdout ) {
		stdout.WriteLine( "Usage: chcon [OPTION]... CONTEXT FILE..." );
		stdout.WriteLine( "  or:  chcon [OPTION]... [-u USER] [-r ROLE] [-l RANGE] [-t TYPE] FILE..." );
		stdout.WriteLine( "  or:  chcon [OPTION]... --reference=RFILE FILE..." );
		stdout.WriteLine( "Change the SELinux security context of each FILE." );
		stdout.WriteLine();
		stdout.WriteLine( "      --dereference          affect the referent of each symbolic link" );
		stdout.WriteLine( "  -h, --no-dereference       affect symbolic links instead of referenced files" );
		stdout.WriteLine( "      --reference=RFILE      use RFILE's security context" );
		stdout.WriteLine( "  -u, --user=USER            set user USER in the target security context" );
		stdout.WriteLine( "  -r, --role=ROLE            set role ROLE in the target security context" );
		stdout.WriteLine( "  -t, --type=TYPE            set type TYPE in the target security context" );
		stdout.WriteLine( "  -l, --range=RANGE          set range RANGE in the target security context" );
		stdout.WriteLine( "  -R, --recursive            operate on files and directories recursively" );
		stdout.WriteLine( "  -H                         follow command-line symbolic links to directories" );
		stdout.WriteLine( "  -L                         follow every symbolic link to a directory" );
		stdout.WriteLine( "  -P                         do not traverse symbolic links (default)" );
		stdout.WriteLine( "      --preserve-root        fail to operate recursively on '/'" );
		stdout.WriteLine( "      --no-preserve-root     do not treat '/' specially (default)" );
		stdout.WriteLine( "  -v, --verbose              output a diagnostic for every file processed" );
		stdout.WriteLine( "      --help                 display this help and exit" );
		stdout.WriteLine( "      --version              output version information and exit" );
	}
}
