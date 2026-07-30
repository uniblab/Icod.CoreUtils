namespace Icod.CoreUtils.Shared.Escapes;

using System.Buffers;
using System.Text;

/// <summary>Parses the low-level byte escapes used inside GNU <c>tr</c> set expressions.</summary>
/// <remarks>Character classes, equivalence classes, repetitions, and set ranges remain command-specific Batch 23 grammar.</remarks>
public static class TrByteEscapeParser {

	/// <summary>Parses ordinary characters and one-to-three-digit octal or named byte escapes.</summary>
	/// <param name="value">The managed set-expression fragment.</param>
	/// <param name="encoding">The stateless encoding used for ordinary Unicode scalars; deterministic UTF-8 is used by default.</param>
	/// <returns>The escaped-byte stream plus deterministic warnings or errors.</returns>
	public static TrByteEscapeParseResult Parse(
		string value,
		Encoding? encoding = null
	) {
		ArgumentNullException.ThrowIfNull( value );
		encoding ??= new UTF8Encoding(
			encoderShouldEmitUTF8Identifier: false,
			throwOnInvalidBytes: true
		);
		var bytes = new List<EscapedByte>();
		var diagnostics = new List<EscapeDiagnostic>();
		for ( var index = 0; index < value.Length; index++ ) {
			if ( '\\' != value[index] ) {
				var sourceOffset = index;
				if ( !AppendRune( value, ref index, encoding, false, sourceOffset, bytes ) ) {
					diagnostics.Add( InvalidScalar( index ) );
					break;
				}
				continue;
			}
			var backslash = index;
			EscapeSequenceScanner.TryRead(
				value,
				ref index,
				out _
			);
			if ( value.Length <= index ) {
				bytes.Add( new EscapedByte( (byte)'\\', false, backslash ) );
				diagnostics.Add(
					new EscapeDiagnostic(
						EscapeDiagnosticCode.TrailingBackslash,
						EscapeDiagnosticSeverity.Warning,
						backslash,
						1,
						"A trailing backslash is treated as an ordinary unescaped backslash."
					)
				);
				break;
			}
			var current = value[index];
			if ( IsOctal( current ) ) {
				AppendOctal(
					value,
					ref index,
					backslash,
					bytes,
					diagnostics
				);
				continue;
			}
			var named = current switch {
				'\\' => (byte)'\\',
				'a' => (byte)'\a',
				'b' => (byte)'\b',
				'f' => (byte)'\f',
				'n' => (byte)'\n',
				'r' => (byte)'\r',
				't' => (byte)'\t',
				'v' => (byte)'\v',
				_ => (byte?)null
			};
			if ( named.HasValue ) {
				bytes.Add( new EscapedByte( named.Value, true, backslash ) );
				continue;
			}
			if ( !AppendRune( value, ref index, encoding, true, backslash, bytes ) ) {
				diagnostics.Add( InvalidScalar( index ) );
				break;
			}
		}
		return new TrByteEscapeParseResult( bytes, diagnostics );
	}

	private static void AppendOctal(
		string value,
		ref int index,
		int backslash,
		List<EscapedByte> bytes,
		List<EscapeDiagnostic> diagnostics
	) {
		var parsed = value[index] - '0';
		var consumed = 1;
		while (
			consumed < 3
			&& index + 1 < value.Length
			&& IsOctal( value[index + 1] )
		) {
			var next = checked( parsed * 8 + ( value[index + 1] - '0' ) );
			if ( 2 == consumed && 255 < next ) {
				diagnostics.Add(
					new EscapeDiagnostic(
						EscapeDiagnosticCode.AmbiguousOctalEscape,
						EscapeDiagnosticSeverity.Warning,
						backslash,
						index + 2 - backslash,
						"A three-digit octal escape exceeded one byte; only the first two digits were consumed."
					)
				);
				break;
			}
			index++;
			parsed = next;
			consumed++;
		}
		bytes.Add( new EscapedByte( (byte)parsed, true, backslash ) );
	}

	private static bool AppendRune(
		string value,
		ref int index,
		Encoding encoding,
		bool wasEscaped,
		int sourceOffset,
		List<EscapedByte> destination
	) {
		var status = Rune.DecodeFromUtf16(
			value.AsSpan( index ),
			out var rune,
			out var consumed
		);
		if ( OperationStatus.Done != status ) {
			return false;
		}
		foreach ( var item in encoding.GetBytes( rune.ToString() ) ) {
			destination.Add(
				new EscapedByte(
					item,
					wasEscaped,
					sourceOffset
				)
			);
		}
		index += consumed - 1;
		return true;
	}

	private static EscapeDiagnostic InvalidScalar( int index ) => new(
		EscapeDiagnosticCode.InvalidUnicodeScalar,
		EscapeDiagnosticSeverity.Error,
		index,
		1,
		"The set expression contains an invalid UTF-16 scalar sequence."
	);

	private static bool IsOctal( char value ) => '0' <= value && value <= '7';

}
