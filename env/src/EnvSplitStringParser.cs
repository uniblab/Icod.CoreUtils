namespace Icod.CoreUtils.Env;

using System.Text;
using Icod.CoreUtils.Shared.Processes;

/// <summary>
/// Parses the GNU <c>env -S</c> split-string language without invoking a shell.
/// </summary>
internal static class EnvSplitStringParser {
	private const string WhiteSpace = " \t\n\v\f\r";

	/// <summary>Splits one GNU <c>-S</c> string using the original environment for variable expansion.</summary>
	internal static IReadOnlyList<string> Parse(
		string value,
		ProcessEnvironment originalEnvironment
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( originalEnvironment );
		var result = new List<string>();
		var current = new StringBuilder();
		var started = false;
		var singleQuoted = false;
		var doubleQuoted = false;
		for ( var index = 0; index < value.Length; index++ ) {
			var character = value[ index ];
			if ( !singleQuoted && !doubleQuoted && WhiteSpace.Contains( character ) ) {
				Flush();
				continue;
			}
			if ( !singleQuoted && !doubleQuoted && '#' == character && !started ) {
				break;
			}
			if ( !doubleQuoted && '\'' == character ) {
				singleQuoted = !singleQuoted;
				started = true;
				continue;
			}
			if ( !singleQuoted && '"' == character ) {
				doubleQuoted = !doubleQuoted;
				started = true;
				continue;
			}
			if ( '$' == character && !singleQuoted ) {
				index = AppendVariable( index );
				continue;
			}
			if ( '\\' == character ) {
				if ( index + 1 >= value.Length ) {
					throw new EnvUsageException( "invalid backslash at end of string in -S" );
				}
				var escaped = value[ ++index ];
				if ( singleQuoted ) {
					if ( '\\' == escaped || '\'' == escaped ) current.Append( escaped );
					else { current.Append( '\\' ); current.Append( escaped ); }
					started = true;
					continue;
				}
				switch ( escaped ) {
					case '"': case '#': case '$': case '\'': case '\\': current.Append( escaped ); break;
					case '_':
						if ( doubleQuoted ) {
							current.Append( ' ' );
							started = true;
						} else {
							Flush();
						}
						continue;
					case 'c':
						if ( doubleQuoted ) throw new EnvUsageException( "\\c must not appear in double quotes in -S" );
						Flush();
						return result;
					case 'f': current.Append( '\f' ); break;
					case 'n': current.Append( '\n' ); break;
					case 'r': current.Append( '\r' ); break;
					case 't': current.Append( '\t' ); break;
					case 'v': current.Append( '\v' ); break;
					default: throw new EnvUsageException( $"invalid sequence '\\{escaped}' in -S" );
				}
				started = true;
				continue;
			}
			current.Append( character );
			started = true;
		}
		if ( singleQuoted || doubleQuoted ) {
			throw new EnvUsageException( "no terminating quote in -S string" );
		}
		Flush();
		return result;

		void Flush() {
			if ( !started ) return;
			result.Add( current.ToString() );
			current.Clear();
			started = false;
		}

		int AppendVariable( int index ) {
			if ( index + 3 >= value.Length || '{' != value[ index + 1 ] ) {
				throw new EnvUsageException( "only ${VARNAME} expansion is supported in -S" );
			}
			var start = index + 2;
			if ( start >= value.Length || !( char.IsAsciiLetter( value[ start ] ) || '_' == value[ start ] ) ) {
				throw new EnvUsageException( "invalid variable name in -S" );
			}
			var end = start + 1;
			while ( end < value.Length && ( char.IsAsciiLetterOrDigit( value[ end ] ) || '_' == value[ end ] ) ) end++;
			if ( end >= value.Length || '}' != value[ end ] ) throw new EnvUsageException( "invalid variable name in -S" );
			var name = value[ start..end ];
			if ( originalEnvironment.Variables.TryGetValue( name, out var expanded ) ) {
				current.Append( expanded );
				started = true;
			}
			return end;
		}
	}
}
