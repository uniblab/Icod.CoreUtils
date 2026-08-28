// Original behavior/reference: GNU coreutils
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.RunCon;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Icod.CommandFramework.Platform;

/// <summary>Runs a command under a selected or computed SELinux process context.</summary>
public static class Command {
	private const int InternalFailure = 125;

	private sealed class Options {
		public bool Compute { get; set; }
		public string? User { get; set; }
		public string? Role { get; set; }
		public string? Type { get; set; }
		public string? Range { get; set; }
		public bool UserSpecified { get; set; }
		public bool RoleSpecified { get; set; }
		public bool TypeSpecified { get; set; }
		public bool RangeSpecified { get; set; }
		public List<string> Operands { get; } = new();
		public bool HasComponents => UserSpecified || RoleSpecified || TypeSpecified || RangeSpecified;
	}

	/// <summary>Runs <c>runcon</c> synchronously.</summary>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		ISelinuxPlatform? platform = null,
		CancellationToken cancellationToken = default
	) {
		return RunAsync(
			args,
			stdin,
			stdout,
			stderr,
			platform,
			cancellationToken
		).GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>runcon</c> through an injectable SELinux/process provider.</summary>
	public static ValueTask<int> RunAsync(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null,
		ISelinuxPlatform? platform = null,
		CancellationToken cancellationToken = default
	) {
		_ = stdin;
		stdout ??= Console.Out;
		stderr ??= Console.Error;

		if ( TryHandleInformationalOption( args, stdout, out var informationalStatus ) ) {
			return ValueTask.FromResult( informationalStatus );
		}
		if ( !TryParse( args, stderr, out var options ) ) {
			return ValueTask.FromResult( InternalFailure );
		}
		if ( cancellationToken.IsCancellationRequested ) {
			stderr.WriteLine( "runcon: operation canceled" );
			return ValueTask.FromResult( InternalFailure );
		}

		// GNU runcon diagnoses a bare CONTEXT operand as a missing command before
		// consulting SELinux.  Preserve that CLI error ordering on unsupported hosts.
		if ( options.Operands.Count == 1 && !options.Compute && !options.HasComponents ) {
			return ValueTask.FromResult(
				ReportError( stderr, "missing command operand after context" )
			);
		}

		platform ??= new NativeSelinuxPlatform();
		if ( !EnsureSelinux( platform, stderr ) ) {
			return ValueTask.FromResult( InternalFailure );
		}

		if ( options.Operands.Count == 0 ) {
			if ( !platform.TryGetCurrentContext( out var current, out var currentError ) ) {
				stderr.WriteLine( $"runcon: failed to get current context: {platform.DescribeError( currentError )}" );
				return ValueTask.FromResult( InternalFailure );
			}
			stdout.WriteLine( current );
			return ValueTask.FromResult( 0 );
		}

		return ValueTask.FromResult( Execute( options, platform, stderr, cancellationToken ) );
	}

	private static int Execute(
		Options options,
		ISelinuxPlatform platform,
		TextWriter stderr,
		CancellationToken cancellationToken
	) {
		string context;
		IReadOnlyList<string> command;

		if ( !options.Compute && !options.HasComponents ) {
			if ( options.Operands.Count < 2 ) {
				return ReportError( stderr, "missing command operand after context" );
			}
			context = options.Operands[ 0 ];
			command = options.Operands.GetRange( 1, options.Operands.Count - 1 );
		} else {
			if ( options.Operands.Count < 1 ) {
				return ReportError( stderr, "missing command operand" );
			}
			command = options.Operands;
			if ( !platform.TryGetCurrentContext( out context, out var currentError ) ) {
				stderr.WriteLine( $"runcon: failed to get current context: {platform.DescribeError( currentError )}" );
				return InternalFailure;
			}

			if ( options.Compute ) {
				if ( !platform.TryGetFileContext( command[ 0 ], true, out var executableContext, out var fileError ) ) {
					stderr.WriteLine( $"runcon: failed to get security context of '{command[ 0 ]}': {platform.DescribeError( fileError )}" );
					return InternalFailure;
				}
				if ( !platform.TryComputeProcessContext( context, executableContext, out var computed, out var computeError ) ) {
					stderr.WriteLine( $"runcon: failed to compute a new context: {platform.DescribeError( computeError )}" );
					return InternalFailure;
				}
				context = computed;
			}

			if ( options.HasComponents ) {
				if ( !SelinuxContext.TryParse( context, out var parsed ) || parsed is null ) {
					stderr.WriteLine( "runcon: failed to parse base security context" );
					return InternalFailure;
				}
				context = new SelinuxContext(
					( options.UserSpecified )
						? options.User!
						: parsed.User,
					( options.RoleSpecified )
						? options.Role!
						: parsed.Role,
					( options.TypeSpecified )
						? options.Type!
						: parsed.Type,
					( options.RangeSpecified )
						? options.Range
						: parsed.Range
				).ToString();
			}
		}

		if ( !platform.TryValidateContext( context, out var validationError ) ) {
			stderr.WriteLine( $"runcon: invalid context '{context}': {platform.DescribeError( validationError )}" );
			return InternalFailure;
		}

		if ( cancellationToken.IsCancellationRequested ) {
			stderr.WriteLine( "runcon: operation canceled" );
			return InternalFailure;
		}

		var execution = platform.ExecuteWithContext(
			context,
			command,
			searchPath: !options.Compute
		);
		if ( execution.Diagnostic is not null ) {
			stderr.WriteLine( $"runcon: {execution.Diagnostic}" );
		}
		return execution.ExitCode;
	}

	private static bool TryParse(
		string[] args,
		TextWriter stderr,
		out Options options
	) {
		options = new Options();
		var endOptions = false;
		for ( var i = 0; i < args.Length; i++ ) {
			var arg = args[ i ];
			if ( endOptions || arg.Length == 0 || arg[ 0 ] != '-' || arg == "-" ) {
				options.Operands.Add( arg );
				endOptions = true;
				continue;
			}
			if ( arg == "--" ) {
				endOptions = true;
				continue;
			}

			if ( arg.StartsWith( "--", StringComparison.Ordinal ) ) {
				if ( !ParseLongOption( args, ref i, arg, options, stderr ) ) {
					return false;
				}
				continue;
			}
			if ( !ParseShortOptions( args, ref i, arg, options, stderr ) ) {
				return false;
			}
		}

		return true;
	}

	private static bool ParseLongOption(
		string[] args,
		ref int index,
		string arg,
		Options options,
		TextWriter stderr
	) {
		var equals = arg.IndexOf( '=' );
		var name = ( equals < 0 )
			? arg
			: arg[ ..equals ]
		;
		var inlineValue = ( equals < 0 )
			? null
			: arg[ ( equals + 1 ).. ]
		;
		switch ( name ) {
			case "--compute":
				if ( inlineValue is not null ) {
					return Error(
						stderr,
						"option '--compute' doesn't allow an argument"
					);
				}
				options.Compute = true;
				return true;
			case "--user":
				return TakeComponent(
					args,
					ref index,
					inlineValue,
					name,
					options.UserSpecified,
					stderr,
					value => {
						options.User = value;
						options.UserSpecified = true;
					}
				);
			case "--role":
				return TakeComponent(
					args,
					ref index,
					inlineValue,
					name,
					options.RoleSpecified,
					stderr,
					value => {
						options.Role = value;
						options.RoleSpecified = true;
					}
				);
			case "--type":
				return TakeComponent(
					args,
					ref index,
					inlineValue,
					name,
					options.TypeSpecified,
					stderr,
					value => {
						options.Type = value;
						options.TypeSpecified = true;
					}
				);
			case "--range":
				return TakeComponent(
					args,
					ref index,
					inlineValue,
					name,
					options.RangeSpecified,
					stderr,
					value => {
						options.Range = value;
						options.RangeSpecified = true;
					}
				);
			default:
				return Error( stderr, $"unrecognized option '{arg}'" );
		}
	}

	private static bool ParseShortOptions(
		string[] args,
		ref int index,
		string arg,
		Options options,
		TextWriter stderr
	) {
		for ( var p = 1; p < arg.Length; p++ ) {
			var option = arg[ p ];
			if ( option == 'c' ) {
				options.Compute = true;
				continue;
			}
			if ( option is not ( 'u' or 'r' or 't' or 'l' ) ) {
				return Error( stderr, $"invalid option -- '{option}'" );
			}

			string value;
			if ( p + 1 < arg.Length ) {
				value = arg[ ( p + 1 ).. ];
				p = arg.Length;
			} else if ( index + 1 < args.Length ) {
				value = args[ ++index ];
			} else {
				return Error( stderr, $"option requires an argument -- '{option}'" );
			}

			if ( option == 'u' ) {
				if ( options.UserSpecified ) {
					return Error( stderr, "multiple users specified" );
				}
				options.User = value;
				options.UserSpecified = true;
			} else if ( option == 'r' ) {
				if ( options.RoleSpecified ) {
					return Error( stderr, "multiple roles specified" );
				}
				options.Role = value;
				options.RoleSpecified = true;
			} else if ( option == 't' ) {
				if ( options.TypeSpecified ) {
					return Error( stderr, "multiple types specified" );
				}
				options.Type = value;
				options.TypeSpecified = true;
			} else {
				if ( options.RangeSpecified ) {
					return Error( stderr, "multiple ranges specified" );
				}
				options.Range = value;
				options.RangeSpecified = true;
			}
		}
		return true;
	}

	private static bool TakeComponent(
		string[] args,
		ref int index,
		string? inlineValue,
		string name,
		bool alreadySpecified,
		TextWriter stderr,
		Action<string> setter
	) {
		if ( alreadySpecified ) {
			return Error( stderr, $"option '{name}' specified more than once" );
		}
		var value = inlineValue;
		if ( value is null ) {
			if ( index + 1 >= args.Length ) {
				return Error( stderr, $"option '{name}' requires an argument" );
			}
			value = args[ ++index ];
		}
		if ( value.Length == 0 ) {
			return Error(
				stderr,
				$"option '{name}' requires a non-empty argument"
			);
		}
		setter( value );
		return true;
	}

	private static bool EnsureSelinux( ISelinuxPlatform platform, TextWriter stderr ) {
		if ( !platform.IsSupported ) {
			stderr.WriteLine( $"runcon: {platform.UnsupportedReason}" );
			return false;
		}
		if ( platform.IsEnabled( out var error ) ) {
			return true;
		}
		stderr.WriteLine( $"runcon: SELinux is disabled or unavailable: {platform.DescribeError( error )}" );
		return false;
	}

	private static bool TryHandleInformationalOption(
		string[] args,
		TextWriter stdout,
		out int status
	) {
		status = 0;
		for ( var i = 0; i < args.Length; i++ ) {
			var arg = args[ i ];
			if ( arg == "--" ) {
				break;
			}
			if ( arg.Length == 0 || arg[ 0 ] != '-' || arg == "-" ) {
				// GNU runcon uses '+' getopt semantics: the first operand ends option parsing.
				break;
			}
			if ( arg == "--help" || arg == "-?" ) {
				PrintHelp( stdout );
				return true;
			}
			if ( arg == "--version" ) {
				stdout.WriteLine( "runcon (Icod.CoreUtils) 9.11-compatible" );
				return true;
			}

			if ( arg is "--user" or "--role" or "--type" or "--range" ) {
				if ( i + 1 < args.Length ) {
					i++;
				}
				continue;
			}
			if (
				arg.StartsWith( "--user=", StringComparison.Ordinal )
					|| arg.StartsWith( "--role=", StringComparison.Ordinal )
					|| arg.StartsWith( "--type=", StringComparison.Ordinal )
					|| arg.StartsWith( "--range=", StringComparison.Ordinal )
					|| arg == "--compute"
			) {
				continue;
			}
			if ( arg.StartsWith( "--", StringComparison.Ordinal ) ) {
				return false;
			}

			if ( arg.Length > 1 && arg[ 0 ] == '-' ) {
				for ( var p = 1; p < arg.Length; p++ ) {
					var option = arg[ p ];
					if ( option == 'c' ) {
						continue;
					}
					if ( option is not ( 'u' or 'r' or 't' or 'l' ) ) {
						return false;
					}
					if ( p + 1 == arg.Length && i + 1 < args.Length ) {
						i++;
					}
					break;
				}
			}
		}
		return false;
	}

	private static bool Error( TextWriter stderr, string message ) {
		stderr.WriteLine( $"runcon: {message}" );
		stderr.WriteLine( "Try 'runcon --help' for more information." );
		return false;
	}

	private static int ReportError( TextWriter stderr, string message ) {
		_ = Error( stderr, message );
		return InternalFailure;
	}

	private static void PrintHelp( TextWriter stdout ) {
		stdout.WriteLine( "Usage: runcon CONTEXT COMMAND [args]" );
		stdout.WriteLine( "  or:  runcon [ -c ] [-u USER] [-r ROLE] [-t TYPE] [-l RANGE] COMMAND [args]" );
		stdout.WriteLine( "Run a program in a different SELinux security context." );
		stdout.WriteLine( "With neither CONTEXT nor COMMAND, print the current security context." );
		stdout.WriteLine();
		stdout.WriteLine( "  -c, --compute          compute process transition context before modifying" );
		stdout.WriteLine( "  -u, --user=USER        set user USER in the target security context" );
		stdout.WriteLine( "  -r, --role=ROLE        set role ROLE in the target security context" );
		stdout.WriteLine( "  -t, --type=TYPE        set type TYPE in the target security context" );
		stdout.WriteLine( "  -l, --range=RANGE      set range RANGE in the target security context" );
		stdout.WriteLine( "      --help             display this help and exit" );
		stdout.WriteLine( "      --version          output version information and exit" );
	}
}
