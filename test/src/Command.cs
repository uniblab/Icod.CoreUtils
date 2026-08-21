// Original behavior/reference: GNU Coreutils 9.11 test.c
// Ported to .NET by Timothy J. Bruce <uniblab@hotmail.com>

namespace Icod.CoreUtils.Test;

using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.FileSystem.Metadata;
using Icod.CoreUtils.Shared.FileSystem.Traversal;
using Icod.CoreUtils.Shared.Platform;
using ProcessIdentity = Icod.CommandFramework.Platform.ProcessIdentity;
using SystemIdentityProvider = Icod.CommandFramework.Platform.SystemIdentityProvider;

/// <summary>Identifies one access mode tested by a <c>test</c> file predicate.</summary>
public enum TestAccessMode {
	/// <summary>Read access.</summary>
	Read = 0,
	/// <summary>Write access.</summary>
	Write = 1,
	/// <summary>Execute or search access.</summary>
	Execute = 2
}

/// <summary>
/// Supplies injectable host observations used by the <c>test</c> expression evaluator.
/// </summary>
public interface ITestEvaluationHost {
	/// <summary>Gets metadata for one pathname, returning <see langword="null"/> when it cannot be observed.</summary>
	/// <param name="path">The pathname to observe.</param>
	/// <param name="followPathIndirection">Whether eligible terminal pathname indirection should be followed.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The metadata observation, or <see langword="null"/> when the entry is absent or inaccessible.</returns>
	ValueTask<FileSystemMetadata?> GetMetadataAsync(
		string path,
		bool followPathIndirection,
		CancellationToken cancellationToken = default
	);

	/// <summary>Gets the real and effective identity of the current process.</summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The process identity.</returns>
	ValueTask<ProcessIdentity> GetProcessIdentityAsync( CancellationToken cancellationToken = default );

	/// <summary>Determines whether one pathname is accessible in the requested mode.</summary>
	/// <param name="path">The pathname to test.</param>
	/// <param name="mode">The access mode.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> when access is available; otherwise, <see langword="false"/>.</returns>
	ValueTask<bool> CanAccessAsync(
		string path,
		TestAccessMode mode,
		CancellationToken cancellationToken = default
	);

	/// <summary>Determines whether one file descriptor is attached to a terminal.</summary>
	/// <param name="fileDescriptor">The file descriptor.</param>
	/// <param name="context">The command context.</param>
	/// <returns><see langword="true"/> when the descriptor is attached to a terminal; otherwise, <see langword="false"/>.</returns>
	bool IsTerminal( int fileDescriptor, CommandContext context );

	/// <summary>Compares two strings according to the active locale collation rules.</summary>
	/// <param name="left">The left string.</param>
	/// <param name="right">The right string.</param>
	/// <returns>A value less than, equal to, or greater than zero.</returns>
	int CompareStrings( string left, string right );
}

/// <summary>
/// Implements GNU/POSIX <c>test</c> expression evaluation without creating a separate <c>[</c> executable.
/// </summary>
public static class Command {
	private const uint SetUserIdBit = 0x0800;
	private const uint SetGroupIdBit = 0x0400;
	private const uint StickyBit = 0x0200;

	/// <summary>Runs <c>test</c> synchronously against optional caller-owned text streams.</summary>
	/// <param name="args">The expression operands and operators.</param>
	/// <param name="stdin">The standard-input reader.</param>
	/// <param name="stdout">The standard-output writer.</param>
	/// <param name="stderr">The standard-error writer.</param>
	/// <returns>Status 0 for true, 1 for false, 2 for a syntax error, or the shared cancellation status.</returns>
	public static int Run(
		string[] args,
		TextReader? stdin = null,
		TextWriter? stdout = null,
		TextWriter? stderr = null
	) {
		var context = new CommandContext(
			"test",
			stdin ?? Console.In,
			stdout ?? Console.Out,
			stderr ?? Console.Error
		);
		return RunAsync( args, context ).AsTask().GetAwaiter().GetResult();
	}

	/// <summary>Runs <c>test</c> asynchronously with the system evaluation host.</summary>
	/// <param name="args">The expression operands and operators.</param>
	/// <param name="context">The command context, or <see langword="null"/> to use console streams.</param>
	/// <returns>Status 0 for true, 1 for false, 2 for a syntax error, or the shared cancellation status.</returns>
	public static ValueTask<int> RunAsync( string[] args, CommandContext? context = null ) {
		return RunAsync(
			args,
			context ?? CommandContext.CreateConsole( "test" ),
			SystemTestEvaluationHost.Instance
		);
	}

	/// <summary>Runs <c>test</c> asynchronously with an injected evaluation host.</summary>
	/// <param name="args">The expression operands and operators.</param>
	/// <param name="context">The command context.</param>
	/// <param name="host">The evaluation host.</param>
	/// <returns>Status 0 for true, 1 for false, 2 for a syntax error, or the shared cancellation status.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="host"/> is <see langword="null"/>.</exception>
	public static async ValueTask<int> RunAsync(
		string[] args,
		CommandContext context,
		ITestEvaluationHost host
	) {
		ArgumentNullException.ThrowIfNull( context );
		ArgumentNullException.ThrowIfNull( host );
		args ??= Array.Empty<string>();
		try {
			context.CancellationToken.ThrowIfCancellationRequested();
			var evaluator = new ExpressionEvaluator( args, context, host );
			return await evaluator.EvaluateAsync().ConfigureAwait( false )
				? CommandExitCodes.Success
				: CommandExitCodes.Failure;
		} catch ( TestSyntaxException exception ) {
			await context.Diagnostics.ErrorAsync(
				exception.Message,
				context.CancellationToken
			).ConfigureAwait( false );
			return CommandExitCodes.UsageError;
		} catch ( OperationCanceledException ) when ( context.CancellationToken.IsCancellationRequested ) {
			return CommandExitCodes.Canceled;
		}
	}

	/// <summary>Writes the command usage and supported expression forms.</summary>
	/// <param name="output">The destination writer.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when the usage text has been written.</returns>
	public static async ValueTask WriteUsageAsync(
		TextWriter output,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		var lines = new[] {
			"Usage: test EXPRESSION",
			"  or:  test",
			"Exit with the status determined by EXPRESSION.",
			string.Empty,
			"File tests include -b -c -d -e -f -g -G -h -k -L -N -O -p -r -s -S -t -u -w -x.",
			"String tests include -n, -z, =, ==, !=, <, and >.",
			"Integer tests include -eq, -ne, -lt, -le, -gt, and -ge; -l STRING supplies a length.",
			"File comparisons include -ef, -nt, and -ot.  Connectives are !, -a, and -o.",
			"Parentheses may group expressions when passed as literal operands.",
			"The operands --help and --version are ordinary strings for the test executable."
		};
		foreach ( var line in lines ) {
			await output.WriteLineAsync( line.AsMemory(), cancellationToken ).ConfigureAwait( false );
		}
	}

	private sealed class ExpressionEvaluator {
		private readonly string[] myArguments;
		private readonly CommandContext myContext;
		private readonly ITestEvaluationHost myHost;
		private int myPosition;

		/// <summary>Initializes an evaluator for one operand vector.</summary>
		/// <param name="arguments">The expression operands and operators.</param>
		/// <param name="context">The command context.</param>
		/// <param name="host">The evaluation host.</param>
		public ExpressionEvaluator(
			string[] arguments,
			CommandContext context,
			ITestEvaluationHost host
		) {
			myArguments = arguments;
			myContext = context;
			myHost = host;
		}

		/// <summary>Evaluates the complete operand vector.</summary>
		/// <returns><see langword="true"/> when the expression is true; otherwise, <see langword="false"/>.</returns>
		public async ValueTask<bool> EvaluateAsync() {
			if ( 0 == myArguments.Length ) return false;
			myPosition = 0;
			var value = await EvaluatePosixAsync( myArguments.Length ).ConfigureAwait( false );
			if ( myPosition != myArguments.Length ) {
				throw new TestSyntaxException(
					string.Concat( "extra argument ", Quote( myArguments[myPosition] ) )
				);
			}
			return value;
		}

		private bool EvaluateOneArgument() {
			return IsNonEmpty( myArguments[myPosition++] );
		}

		private async ValueTask<bool> EvaluateTwoArgumentsAsync() {
			if ( IsCurrent( "!" ) ) {
				Advance( false );
				return !EvaluateOneArgument();
			}
			if ( IsUnaryCandidate( myArguments[myPosition] ) ) {
				return await EvaluateUnaryAtCurrentAsync().ConfigureAwait( false );
			}
			throw MissingArgument();
		}

		private async ValueTask<bool> EvaluateThreeArgumentsAsync() {
			var binaryOperator = GetBinaryOperator( myArguments[myPosition + 1] );
			if ( null != binaryOperator ) {
				return await EvaluateBinaryAtCurrentAsync( false, binaryOperator ).ConfigureAwait( false );
			}
			if ( IsCurrent( "!" ) ) {
				Advance( true );
				return !await EvaluateTwoArgumentsAsync().ConfigureAwait( false );
			}
			if (
				IsCurrent( "(" )
				&& string.Equals( myArguments[myPosition + 2], ")", StringComparison.Ordinal )
			) {
				Advance( false );
				var value = EvaluateOneArgument();
				Advance( false );
				return value;
			}
			var middle = myArguments[myPosition + 1];
			if ( middle is "-a" or "-o" or ">" or "<" ) {
				return await ParseExpressionAsync().ConfigureAwait( false );
			}
			throw new TestSyntaxException( string.Concat( Quote( middle ), ": binary operator expected" ) );
		}

		private async ValueTask<bool> EvaluatePosixAsync( int argumentCount ) {
			return argumentCount switch {
				1 => EvaluateOneArgument(),
				2 => await EvaluateTwoArgumentsAsync().ConfigureAwait( false ),
				3 => await EvaluateThreeArgumentsAsync().ConfigureAwait( false ),
				4 => await EvaluateFourArgumentsAsync().ConfigureAwait( false ),
				_ when 0 < argumentCount => await ParseExpressionAsync().ConfigureAwait( false ),
				_ => false
			};
		}

		private async ValueTask<bool> EvaluateFourArgumentsAsync() {
			if ( IsCurrent( "!" ) ) {
				Advance( true );
				return !await EvaluateThreeArgumentsAsync().ConfigureAwait( false );
			}
			if (
				IsCurrent( "(" )
				&& string.Equals( myArguments[myPosition + 3], ")", StringComparison.Ordinal )
			) {
				Advance( false );
				var value = await EvaluateTwoArgumentsAsync().ConfigureAwait( false );
				Advance( false );
				return value;
			}
			return await ParseExpressionAsync().ConfigureAwait( false );
		}

		private async ValueTask<bool> ParseExpressionAsync() {
			if ( myPosition >= myArguments.Length ) throw MissingArgument();
			return await ParseOrAsync().ConfigureAwait( false );
		}

		private async ValueTask<bool> ParseOrAsync() {
			var value = false;
			while ( true ) {
				value |= await ParseAndAsync().ConfigureAwait( false );
				if ( !IsCurrent( "-o" ) ) return value;
				Advance( false );
			}
		}

		private async ValueTask<bool> ParseAndAsync() {
			var value = true;
			while ( true ) {
				value &= await ParseTermAsync().ConfigureAwait( false );
				if ( !IsCurrent( "-a" ) ) return value;
				Advance( false );
			}
		}

		private async ValueTask<bool> ParseTermAsync() {
			var negated = false;
			while ( IsCurrent( "!" ) ) {
				Advance( true );
				negated = !negated;
			}
			if ( myPosition >= myArguments.Length ) throw MissingArgument();

			bool value;
			if ( IsCurrent( "(" ) ) {
				Advance( true );
				var argumentCount = CountParenthesizedArguments();
				value = await EvaluatePosixAsync( argumentCount ).ConfigureAwait( false );
				if ( myPosition >= myArguments.Length ) {
					throw new TestSyntaxException( string.Concat( Quote( ")" ), " expected" ) );
				}
				if ( !IsCurrent( ")" ) ) {
					throw new TestSyntaxException(
						string.Concat(
							Quote( ")" ),
							" expected, found ",
							Quote( myArguments[myPosition] )
						)
					);
				}
				Advance( false );
			} else if (
				4 <= myArguments.Length - myPosition
				&& IsCurrent( "-l" )
				&& null != GetBinaryOperator( myArguments[myPosition + 2] )
			) {
				var binaryOperator = GetBinaryOperator( myArguments[myPosition + 2] )!;
				value = await EvaluateBinaryAtCurrentAsync( true, binaryOperator ).ConfigureAwait( false );
			} else if (
				3 <= myArguments.Length - myPosition
				&& null != GetBinaryOperator( myArguments[myPosition + 1] )
			) {
				var binaryOperator = GetBinaryOperator( myArguments[myPosition + 1] )!;
				value = await EvaluateBinaryAtCurrentAsync( false, binaryOperator ).ConfigureAwait( false );
			} else if ( IsUnaryCandidate( myArguments[myPosition] ) ) {
				value = await EvaluateUnaryAtCurrentAsync().ConfigureAwait( false );
			} else {
				value = EvaluateOneArgument();
			}
			return negated ^ value;
		}

		private int CountParenthesizedArguments() {
			var argumentCount = 1;
			while (
				myPosition + argumentCount < myArguments.Length
				&& !string.Equals(
					myArguments[myPosition + argumentCount],
					")",
					StringComparison.Ordinal
				)
			) {
				if ( 4 == argumentCount ) {
					return myArguments.Length - myPosition;
				}
				argumentCount++;
			}
			return argumentCount;
		}

		private async ValueTask<bool> EvaluateUnaryAtCurrentAsync() {
			var unaryOperator = myArguments[myPosition];
			if ( !IsUnaryOperator( unaryOperator ) ) {
				throw new TestSyntaxException(
					string.Concat( Quote( unaryOperator ), ": unary operator expected" )
				);
			}
			Advance( true );
			var operand = myArguments[myPosition];
			Advance( false );
			return await EvaluateUnaryAsync( unaryOperator, operand ).ConfigureAwait( false );
		}

		private async ValueTask<bool> EvaluateUnaryAsync( string unaryOperator, string operand ) {
			myContext.CancellationToken.ThrowIfCancellationRequested();
			if ( unaryOperator == "-n" ) return IsNonEmpty( operand );
			if ( unaryOperator == "-z" ) return !IsNonEmpty( operand );
			if ( unaryOperator == "-t" ) {
				var descriptor = ParseInteger( operand );
				if ( descriptor < 0 || descriptor > int.MaxValue ) return false;
				return myHost.IsTerminal( (int)descriptor, myContext );
			}
			if ( unaryOperator is "-r" or "-w" or "-x" ) {
				var accessMode = unaryOperator switch {
					"-r" => TestAccessMode.Read,
					"-w" => TestAccessMode.Write,
					_ => TestAccessMode.Execute
				};
				return await myHost.CanAccessAsync(
					operand,
					accessMode,
					myContext.CancellationToken
				).ConfigureAwait( false );
			}

			var follow = unaryOperator is not "-h" and not "-L";
			var metadata = await myHost.GetMetadataAsync(
				operand,
				follow,
				myContext.CancellationToken
			).ConfigureAwait( false );
			if ( null == metadata ) return false;

			return unaryOperator switch {
				"-b" => metadata.Kind == FileSystemEntryKind.BlockDevice,
				"-c" => metadata.Kind == FileSystemEntryKind.CharacterDevice,
				"-d" => metadata.Kind == FileSystemEntryKind.Directory,
				"-e" => true,
				"-f" => metadata.Kind == FileSystemEntryKind.File,
				"-g" => HasModeBit( metadata, SetGroupIdBit ),
				"-G" => await IsOwnedByEffectiveGroupAsync( metadata ).ConfigureAwait( false ),
				"-h" or "-L" => metadata.IsSymbolicLink,
				"-k" => HasModeBit( metadata, StickyBit ),
				"-N" => IsModifiedSinceRead( metadata ),
				"-O" => await IsOwnedByEffectiveUserAsync( metadata ).ConfigureAwait( false ),
				"-p" => metadata.Kind == FileSystemEntryKind.Fifo,
				"-s" => metadata.Size.IsAvailable && 0UL < metadata.Size.GetRequiredValue(),
				"-S" => metadata.Kind == FileSystemEntryKind.Socket,
				"-u" => HasModeBit( metadata, SetUserIdBit ),
				_ => throw new TestSyntaxException(
					string.Concat( Quote( unaryOperator ), ": unary operator expected" )
				)
			};
		}

		private async ValueTask<bool> EvaluateBinaryAtCurrentAsync(
			bool leftIsLength,
			string binaryOperator
		) {
			if ( leftIsLength ) Advance( false );
			var operatorPosition = myPosition + 1;
			var rightIsLength = operatorPosition < myArguments.Length - 2
				&& string.Equals(
					myArguments[operatorPosition + 1],
					"-l",
					StringComparison.Ordinal
				);
			if ( rightIsLength ) Advance( false );
			myPosition += 3;

			var left = myArguments[operatorPosition - 1];
			var directRight = myArguments[operatorPosition + 1];
			if ( IsNumericOperator( binaryOperator ) ) {
				var numericRight = rightIsLength
					? myArguments[operatorPosition + 2]
					: directRight;
				return CompareIntegers(
					leftIsLength ? Utf8Length( left ) : ParseInteger( left ),
					binaryOperator,
					rightIsLength ? Utf8Length( numericRight ) : ParseInteger( numericRight )
				);
			}
			if ( binaryOperator is "-nt" or "-ot" ) {
				if ( leftIsLength || rightIsLength ) {
					throw new TestSyntaxException(
						string.Concat( binaryOperator, " does not accept -l" )
					);
				}
				return binaryOperator == "-nt"
					? await IsNewerThanAsync( left, directRight ).ConfigureAwait( false )
					: await IsOlderThanAsync( left, directRight ).ConfigureAwait( false );
			}
			if ( binaryOperator == "-ef" ) {
				if ( leftIsLength || rightIsLength ) {
					throw new TestSyntaxException( "-ef does not accept -l" );
				}
				return await AreSameFileAsync( left, directRight ).ConfigureAwait( false );
			}
			return binaryOperator switch {
				"=" or "==" => string.Equals( left, directRight, StringComparison.Ordinal ),
				"!=" => !string.Equals( left, directRight, StringComparison.Ordinal ),
				"<" => 0 > myHost.CompareStrings( left, directRight ),
				">" => 0 < myHost.CompareStrings( left, directRight ),
				_ => throw new TestSyntaxException(
					string.Concat( Quote( binaryOperator ), ": binary operator expected" )
				)
			};
		}

		private async ValueTask<bool> AreSameFileAsync( string left, string right ) {
			var leftMetadata = await GetFollowedMetadataAsync( left ).ConfigureAwait( false );
			var rightMetadata = await GetFollowedMetadataAsync( right ).ConfigureAwait( false );
			if ( null == leftMetadata || null == rightMetadata ) return false;
			if ( leftMetadata.EntryIdentity.IsAvailable && rightMetadata.EntryIdentity.IsAvailable ) {
				return leftMetadata.EntryIdentity == rightMetadata.EntryIdentity;
			}
			return leftMetadata.DeviceIdentifier.IsAvailable
				&& rightMetadata.DeviceIdentifier.IsAvailable
				&& leftMetadata.InodeNumber.IsAvailable
				&& rightMetadata.InodeNumber.IsAvailable
				&& string.Equals(
					leftMetadata.DeviceIdentifier.GetRequiredValue(),
					rightMetadata.DeviceIdentifier.GetRequiredValue(),
					StringComparison.Ordinal
				)
				&& leftMetadata.InodeNumber.GetRequiredValue() == rightMetadata.InodeNumber.GetRequiredValue();
		}

		private async ValueTask<bool> IsNewerThanAsync( string left, string right ) {
			var leftMetadata = await GetFollowedMetadataAsync( left ).ConfigureAwait( false );
			var rightMetadata = await GetFollowedMetadataAsync( right ).ConfigureAwait( false );
			if ( null == leftMetadata ) return false;
			if ( null == rightMetadata ) return true;
			return leftMetadata.ModificationTime.IsAvailable
				&& rightMetadata.ModificationTime.IsAvailable
				&& leftMetadata.ModificationTime.GetRequiredValue()
					> rightMetadata.ModificationTime.GetRequiredValue();
		}

		private async ValueTask<bool> IsOlderThanAsync( string left, string right ) {
			var leftMetadata = await GetFollowedMetadataAsync( left ).ConfigureAwait( false );
			var rightMetadata = await GetFollowedMetadataAsync( right ).ConfigureAwait( false );
			if ( null == rightMetadata ) return false;
			if ( null == leftMetadata ) return true;
			return leftMetadata.ModificationTime.IsAvailable
				&& rightMetadata.ModificationTime.IsAvailable
				&& leftMetadata.ModificationTime.GetRequiredValue()
					< rightMetadata.ModificationTime.GetRequiredValue();
		}

		private ValueTask<FileSystemMetadata?> GetFollowedMetadataAsync( string path ) {
			return myHost.GetMetadataAsync( path, true, myContext.CancellationToken );
		}

		private async ValueTask<bool> IsOwnedByEffectiveUserAsync( FileSystemMetadata metadata ) {
			var identity = await myHost.GetProcessIdentityAsync(
				myContext.CancellationToken
			).ConfigureAwait( false );
			if ( metadata.UserId.IsAvailable ) {
				return string.Equals(
					metadata.UserId.GetRequiredValue().ToString( CultureInfo.InvariantCulture ),
					identity.EffectiveUser.Id,
					StringComparison.Ordinal
				);
			}
			return metadata.OwnerName.IsAvailable && (
				string.Equals(
					metadata.OwnerName.GetRequiredValue(),
					identity.EffectiveUser.Id,
					StringComparison.Ordinal
				)
				|| string.Equals(
					metadata.OwnerName.GetRequiredValue(),
					identity.EffectiveUser.Name,
					StringComparison.Ordinal
				)
			);
		}

		private async ValueTask<bool> IsOwnedByEffectiveGroupAsync( FileSystemMetadata metadata ) {
			var identity = await myHost.GetProcessIdentityAsync(
				myContext.CancellationToken
			).ConfigureAwait( false );
			if ( metadata.GroupId.IsAvailable ) {
				return string.Equals(
					metadata.GroupId.GetRequiredValue().ToString( CultureInfo.InvariantCulture ),
					identity.EffectiveGroup.Id,
					StringComparison.Ordinal
				);
			}
			return metadata.GroupName.IsAvailable && (
				string.Equals(
					metadata.GroupName.GetRequiredValue(),
					identity.EffectiveGroup.Id,
					StringComparison.Ordinal
				)
				|| string.Equals(
					metadata.GroupName.GetRequiredValue(),
					identity.EffectiveGroup.Name,
					StringComparison.Ordinal
				)
			);
		}

		private void Advance( bool requireAnotherArgument ) {
			myPosition++;
			if ( requireAnotherArgument && myPosition >= myArguments.Length ) {
				throw MissingArgument();
			}
		}

		private TestSyntaxException MissingArgument() {
			var previous = 0 < myArguments.Length ? myArguments[^1] : string.Empty;
			return new TestSyntaxException(
				string.Concat( "missing argument after ", Quote( previous ) )
			);
		}

		private bool IsCurrent( string value ) {
			return myPosition < myArguments.Length
				&& string.Equals( myArguments[myPosition], value, StringComparison.Ordinal );
		}
	}
	private sealed class SystemTestEvaluationHost : ITestEvaluationHost {
		/// <summary>Gets the shared system evaluation host.</summary>
		public static SystemTestEvaluationHost Instance { get; } = new();
		private static readonly string[] DefaultExecutableExtensions = { ".COM", ".EXE", ".BAT", ".CMD" };

		/// <summary>Initializes a system evaluation host.</summary>
		public SystemTestEvaluationHost() { }

		/// <inheritdoc/>
		public async ValueTask<FileSystemMetadata?> GetMetadataAsync(
			string path,
			bool followPathIndirection,
			CancellationToken cancellationToken = default
		) {
			try {
				return await SystemFileSystemMetadataProvider.Instance.GetMetadataAsync(
					path,
					followPathIndirection,
					cancellationToken
				).ConfigureAwait( false );
			} catch ( FileNotFoundException ) {
				return null;
			} catch ( DirectoryNotFoundException ) {
				return null;
			} catch ( UnauthorizedAccessException ) {
				return null;
			} catch ( IOException ) {
				return null;
			} catch ( NotSupportedException ) {
				return null;
			} catch ( ArgumentException ) {
				return null;
			}
		}

		/// <inheritdoc/>
		public ValueTask<ProcessIdentity> GetProcessIdentityAsync(
			CancellationToken cancellationToken = default
		) => SystemIdentityProvider.Instance.GetCurrentAsync( cancellationToken );

		/// <inheritdoc/>
		public async ValueTask<bool> CanAccessAsync(
			string path,
			TestAccessMode mode,
			CancellationToken cancellationToken = default
		) {
			var metadata = await GetMetadataAsync( path, true, cancellationToken ).ConfigureAwait( false );
			if ( null == metadata ) return false;
			if ( metadata.Mode.IsAvailable && IsUnixLike ) {
				var identity = await GetProcessIdentityAsync( cancellationToken ).ConfigureAwait( false );
				return HasUnixAccess( metadata, identity, mode );
			}
			if ( OperatingSystem.IsWindows() ) return HasWindowsAccess( metadata, path, mode );
			return mode != TestAccessMode.Execute || metadata.Kind == FileSystemEntryKind.Directory;
		}

		/// <inheritdoc/>
		public bool IsTerminal( int fileDescriptor, CommandContext context ) {
			if ( 0 > fileDescriptor ) return false;
			try {
				return OperatingSystem.IsWindows()
					? 0 != NativeMethods.WindowsIsATty( fileDescriptor )
					: 1 == NativeMethods.UnixIsATty( fileDescriptor );
			} catch ( DllNotFoundException ) {
				return false;
			} catch ( EntryPointNotFoundException ) {
				return false;
			}
		}

		/// <inheritdoc/>
		public int CompareStrings( string left, string right ) {
			return CultureInfo.CurrentCulture.CompareInfo.Compare(
				left,
				right,
				CompareOptions.None
			);
		}

		private static bool HasUnixAccess(
			FileSystemMetadata metadata,
			ProcessIdentity identity,
			TestAccessMode accessMode
		) {
			var mode = metadata.Mode.GetRequiredValue();
			if ( identity.EffectiveUser.Id == "0" ) {
				return accessMode != TestAccessMode.Execute
					|| metadata.Kind == FileSystemEntryKind.Directory
					|| 0 != (mode & 0x49U);
			}

			var shift = 0;
			if (
				metadata.UserId.IsAvailable
				&& identity.EffectiveUser.Id == metadata.UserId.GetRequiredValue().ToString( CultureInfo.InvariantCulture )
			) {
				shift = 6;
			} else if ( metadata.GroupId.IsAvailable ) {
				var groupId = metadata.GroupId.GetRequiredValue().ToString( CultureInfo.InvariantCulture );
				if (
					identity.EffectiveGroup.Id == groupId
					|| identity.Groups.Any( group => group.Id == groupId )
				) shift = 3;
			}
			var bit = accessMode switch {
				TestAccessMode.Read => 4U,
				TestAccessMode.Write => 2U,
				_ => 1U
			};
			return 0 != (mode & (bit << shift));
		}

		private static bool HasWindowsAccess(
			FileSystemMetadata metadata,
			string path,
			TestAccessMode accessMode
		) {
			if ( accessMode == TestAccessMode.Read ) return true;
			if ( accessMode == TestAccessMode.Write ) {
				return !metadata.Attributes.IsAvailable
					|| 0 == (metadata.Attributes.GetRequiredValue() & FileAttributes.ReadOnly);
			}
			if ( metadata.Kind == FileSystemEntryKind.Directory ) return true;
			var extension = System.IO.Path.GetExtension( path );
			var configured = Environment.GetEnvironmentVariable( "PATHEXT" );
			var extensions = string.IsNullOrWhiteSpace( configured )
				? DefaultExecutableExtensions
				: configured.Split( ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
			return extensions.Contains( extension, StringComparer.OrdinalIgnoreCase );
		}

		private static bool IsUnixLike => OperatingSystem.IsLinux()
			|| OperatingSystem.IsMacOS()
			|| OperatingSystem.IsFreeBSD();

		private static class NativeMethods {
			/// <summary>Queries whether one Unix file descriptor is a terminal.</summary>
			/// <param name="fileDescriptor">The file descriptor.</param>
			/// <returns>One for a terminal; otherwise, zero.</returns>
			[DllImport( "libc", EntryPoint = "isatty", SetLastError = true )]
			public static extern int UnixIsATty( int fileDescriptor );

			/// <summary>Queries whether one Windows C-runtime file descriptor is a terminal.</summary>
			/// <param name="fileDescriptor">The file descriptor.</param>
			/// <returns>A nonzero value for a terminal; otherwise, zero.</returns>
			[DllImport( "msvcrt", EntryPoint = "_isatty", SetLastError = true )]
			public static extern int WindowsIsATty( int fileDescriptor );
		}
	}

	private sealed class TestSyntaxException : Exception {
		/// <summary>Initializes a syntax-error exception.</summary>
		/// <param name="message">The diagnostic message.</param>
		public TestSyntaxException( string message ) : base( message ) { }
	}

	private static bool HasModeBit( FileSystemMetadata metadata, uint bit ) {
		return metadata.Mode.IsAvailable && 0 != (metadata.Mode.GetRequiredValue() & bit);
	}

	private static bool IsModifiedSinceRead( FileSystemMetadata metadata ) {
		return metadata.ModificationTime.IsAvailable
			&& metadata.AccessTime.IsAvailable
			&& metadata.ModificationTime.GetRequiredValue() > metadata.AccessTime.GetRequiredValue();
	}

	private static bool IsNonEmpty( string value ) => !string.IsNullOrEmpty( value );

	private static bool IsUnaryCandidate( string value ) {
		return 2 == value.Length && '-' == value[0] && '\0' != value[1];
	}

	private static string? GetBinaryOperator( string value ) {
		return IsBinaryOperator( value ) ? value : null;
	}

	private static bool IsUnaryOperator( string value ) => value is
		"-b" or "-c" or "-d" or "-e" or "-f" or "-g" or "-G" or "-h" or
		"-k" or "-L" or "-N" or "-n" or "-O" or "-p" or "-r" or "-s" or
		"-S" or "-t" or "-u" or "-w" or "-x" or "-z";

	private static bool IsBinaryOperator( string value ) => value is
		"=" or "==" or "!=" or "<" or ">" or
		"-eq" or "-ne" or "-lt" or "-le" or "-gt" or "-ge" or
		"-ef" or "-nt" or "-ot";

	private static bool IsNumericOperator( string value ) => value is
		"-eq" or "-ne" or "-lt" or "-le" or "-gt" or "-ge";

	private static BigInteger ParseInteger( string value ) {
		var trimmed = value.Trim();
		var index = 0;
		if ( 0 < trimmed.Length && trimmed[0] is '+' or '-' ) index = 1;
		if ( index == trimmed.Length ) throw InvalidInteger( value );
		for ( ; index < trimmed.Length; index++ ) {
			if ( trimmed[index] is < '0' or > '9' ) throw InvalidInteger( value );
		}
		return BigInteger.Parse( trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture );
	}

	private static TestSyntaxException InvalidInteger( string value ) => new(
		string.Concat( "invalid integer ", Quote( value ) )
	);

	private static bool CompareIntegers(
		BigInteger left,
		string binaryOperator,
		BigInteger right
	) => binaryOperator switch {
		"-eq" => left == right,
		"-ne" => left != right,
		"-lt" => left < right,
		"-le" => left <= right,
		"-gt" => left > right,
		"-ge" => left >= right,
		_ => throw new TestSyntaxException(
			string.Concat( Quote( binaryOperator ), ": binary operator expected" )
		)
	};

	private static BigInteger Utf8Length( string value ) => Encoding.UTF8.GetByteCount( value );

	private static string Quote( string value ) => string.Concat( "'", value.Replace( "'", "'\\''", StringComparison.Ordinal ), "'" );
}
