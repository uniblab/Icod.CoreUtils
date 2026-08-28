/*
	Icod.CoreUtils.Shared
	Shared support library for the Icod.CoreUtils command suite.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

namespace Icod.CoreUtils.Shared.Codecs;

using System.Buffers;
using System.Numerics;
using System.Text;

/// <summary>
/// Provides asynchronous encoders and decoders for the encodings exposed by
/// <c>base32</c>, <c>base64</c>, and <c>basenc</c>. All fixed-ratio encodings
/// stream through bounded buffers. Base58 buffers one complete value because
/// its arbitrary-precision conversion depends on the whole integer.
/// </summary>
public static class BaseEncodingProcessor {

	private const string Base16Alphabet = "0123456789ABCDEF";
	private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
	private const string Base32HexAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUV";
	private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
	private const string Base64Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
	private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
	private const string Z85Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";
	private static readonly byte[] LineFeed = new byte[] {
		(byte)'\n'
	};

	/// <summary>
	/// Encodes the input stream and writes ASCII output.
	/// </summary>
	public static async Task EncodeAsync(
		Stream input,
		Stream output,
		BaseEncodingKind encoding,
		long wrapColumns = 76,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			input
		);
		ArgumentNullException.ThrowIfNull(
			output
		);
		if ( !input.CanRead ) {
			throw new ArgumentException(
				"The input stream must be readable.",
				nameof( input )
			);
		}
		if ( !output.CanWrite ) {
			throw new ArgumentException(
				"The output stream must be writable.",
				nameof( output )
			);
		}
		if ( wrapColumns < 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( wrapColumns )
			);
		}

		var writer = new WrappedAsciiWriter(
			output,
			wrapColumns
		);
		switch ( encoding ) {
			case BaseEncodingKind.Base16:
				await EncodeBase16Async(
					input,
					writer,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base32:
				await EncodePowerOfTwoAsync(
					input,
					writer,
					Base32Alphabet,
					5,
					8,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base32Hex:
				await EncodePowerOfTwoAsync(
					input,
					writer,
					Base32HexAlphabet,
					5,
					8,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base58:
				await EncodeBase58Async(
					input,
					writer,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base64:
				await EncodePowerOfTwoAsync(
					input,
					writer,
					Base64Alphabet,
					6,
					4,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base64Url:
				await EncodePowerOfTwoAsync(
					input,
					writer,
					Base64UrlAlphabet,
					6,
					4,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base2Lsbf:
				await EncodeBase2Async(
					input,
					writer,
					leastSignificantBitFirst: true,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base2Msbf:
				await EncodeBase2Async(
					input,
					writer,
					leastSignificantBitFirst: false,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Z85:
				await EncodeZ85Async(
					input,
					writer,
					cancellationToken
				).ConfigureAwait( false );
				break;
			default:
				throw new ArgumentOutOfRangeException(
					nameof( encoding )
				);
		}

		await writer.CompleteAsync(
			cancellationToken
		).ConfigureAwait( false );
	}

	/// <summary>
	/// Decodes ASCII input and writes the recovered bytes.
	/// </summary>
	public static async Task DecodeAsync(
		Stream input,
		Stream output,
		BaseEncodingKind encoding,
		bool ignoreGarbage = false,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull(
			input
		);
		ArgumentNullException.ThrowIfNull(
			output
		);
		if ( !input.CanRead ) {
			throw new ArgumentException(
				"The input stream must be readable.",
				nameof( input )
			);
		}
		if ( !output.CanWrite ) {
			throw new ArgumentException(
				"The output stream must be writable.",
				nameof( output )
			);
		}

		switch ( encoding ) {
			case BaseEncodingKind.Base16:
				await DecodeBase16Async(
					input,
					output,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base32:
				await DecodePowerOfTwoAsync(
					input,
					output,
					Base32Alphabet,
					5,
					8,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base32Hex:
				await DecodePowerOfTwoAsync(
					input,
					output,
					Base32HexAlphabet,
					5,
					8,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base58:
				await DecodeBase58Async(
					input,
					output,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base64:
				await DecodePowerOfTwoAsync(
					input,
					output,
					Base64Alphabet,
					6,
					4,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base64Url:
				await DecodePowerOfTwoAsync(
					input,
					output,
					Base64UrlAlphabet,
					6,
					4,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base2Lsbf:
				await DecodeBase2Async(
					input,
					output,
					leastSignificantBitFirst: true,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Base2Msbf:
				await DecodeBase2Async(
					input,
					output,
					leastSignificantBitFirst: false,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			case BaseEncodingKind.Z85:
				await DecodeZ85Async(
					input,
					output,
					ignoreGarbage,
					cancellationToken
				).ConfigureAwait( false );
				break;
			default:
				throw new ArgumentOutOfRangeException(
					nameof( encoding )
				);
		}

		await output.FlushAsync(
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task EncodePowerOfTwoAsync(
		Stream input,
		WrappedAsciiWriter writer,
		string alphabet,
		int bitsPerSymbol,
		int symbolsPerQuantum,
		CancellationToken cancellationToken
	) {
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			128 * 1024
		);
		uint accumulator = 0;
		var bitCount = 0;
		long symbolCount = 0;

		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}

				var outputCount = 0;
				for (
					var index = 0;
					index < read;
					index++
				) {
					accumulator = (
						accumulator << 8
					) | inputBuffer[ index ];
					bitCount += 8;
					while ( bitsPerSymbol <= bitCount ) {
						bitCount -= bitsPerSymbol;
						outputBuffer[ outputCount++ ] = checked(
							(byte)alphabet[
								(int)(
									accumulator >> bitCount
								) & (
									( 1 << bitsPerSymbol ) - 1
								)
							]
						);
						symbolCount++;
					}
					accumulator = 0 == bitCount
						? 0
						: accumulator & (
							( 1u << bitCount ) - 1u
						)
					;
				}

				if ( 0 < outputCount ) {
					await writer.WriteAsync(
						outputBuffer.AsMemory(
							0,
							outputCount
						),
						cancellationToken
					).ConfigureAwait( false );
				}
			}

			var finalBuffer = new byte[
				symbolsPerQuantum + 1
			];
			var finalCount = 0;
			if ( 0 < bitCount ) {
				finalBuffer[ finalCount++ ] = checked(
					(byte)alphabet[
						(int)(
							accumulator << (
								bitsPerSymbol - bitCount
							)
						) & (
							( 1 << bitsPerSymbol ) - 1
						)
					]
				);
				symbolCount++;
			}
			while (
				0 != symbolCount % symbolsPerQuantum
			) {
				finalBuffer[ finalCount++ ] = (byte)'=';
				symbolCount++;
			}
			if ( 0 < finalCount ) {
				await writer.WriteAsync(
					finalBuffer.AsMemory(
						0,
						finalCount
					),
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static async Task DecodePowerOfTwoAsync(
		Stream input,
		Stream output,
		string alphabet,
		int bitsPerSymbol,
		int symbolsPerQuantum,
		bool ignoreGarbage,
		CancellationToken cancellationToken
	) {
		var lookup = CreateLookup(
			alphabet,
			caseInsensitive: false
		);
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		uint accumulator = 0;
		var bitCount = 0;
		var symbolsInQuantum = 0;
		var paddingCount = 0;

		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}

				var outputCount = 0;
				for (
					var index = 0;
					index < read;
					index++
				) {
					var value = inputBuffer[ index ];
					if ( (byte)'\n' == value ) {
						continue;
					}
					if ( (byte)'=' == value ) {
						paddingCount++;
						continue;
					}

					var decoded = lookup[ value ];
					if ( decoded < 0 ) {
						if ( ignoreGarbage ) {
							continue;
						}
						throw InvalidInput();
					}
					if ( 0 < paddingCount ) {
						ValidatePaddedQuantum(
							bitsPerSymbol,
							symbolsInQuantum,
							paddingCount,
							accumulator
						);
						accumulator = 0;
						bitCount = 0;
						symbolsInQuantum = 0;
						paddingCount = 0;
					}

					symbolsInQuantum++;
					accumulator = (
						accumulator << bitsPerSymbol
					) | checked( (uint)decoded );
					bitCount += bitsPerSymbol;
					while ( 8 <= bitCount ) {
						bitCount -= 8;
						outputBuffer[ outputCount++ ] = checked(
							(byte)(
								accumulator >> bitCount
							)
						);
						if ( outputCount == outputBuffer.Length ) {
							await output.WriteAsync(
								outputBuffer.AsMemory(
									0,
									outputCount
								),
								cancellationToken
							).ConfigureAwait( false );
							outputCount = 0;
						}
					}
					accumulator = 0 == bitCount
						? 0
						: accumulator & (
							( 1u << bitCount ) - 1u
						)
					;
					if ( symbolsInQuantum == symbolsPerQuantum ) {
						if (
							0 != bitCount
							|| 0 != accumulator
						) {
							throw InvalidInput();
						}
						symbolsInQuantum = 0;
					}
				}

				if ( 0 < outputCount ) {
					await output.WriteAsync(
						outputBuffer.AsMemory(
							0,
							outputCount
						),
						cancellationToken
					).ConfigureAwait( false );
				}
			}

			if ( 0 < paddingCount ) {
				ValidatePaddedQuantum(
					bitsPerSymbol,
					symbolsInQuantum,
					paddingCount,
					accumulator
				);
			} else {
				ValidateUnpaddedQuantum(
					bitsPerSymbol,
					symbolsInQuantum,
					accumulator
				);
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static void ValidatePaddedQuantum(
		int bitsPerSymbol,
		int symbolsInQuantum,
		int paddingCount,
		uint accumulator
	) {
		var requiredPadding = GetMaximumPadding(
			bitsPerSymbol,
			symbolsInQuantum
		);
		if (
			requiredPadding <= 0
			|| paddingCount != requiredPadding
			|| 0 != accumulator
		) {
			throw InvalidInput();
		}
	}

	private static void ValidateUnpaddedQuantum(
		int bitsPerSymbol,
		int symbolsInQuantum,
		uint accumulator
	) {
		if (
			GetMaximumPadding(
				bitsPerSymbol,
				symbolsInQuantum
			) < 0
			|| 0 != accumulator
		) {
			throw InvalidInput();
		}
	}

	private static int GetMaximumPadding(
		int bitsPerSymbol,
		int remainder
	) {
		if ( 6 == bitsPerSymbol ) {
			return remainder switch {
				0 => 0,
				2 => 2,
				3 => 1,
				_ => -1
			};
		}
		return remainder switch {
			0 => 0,
			2 => 6,
			4 => 4,
			5 => 3,
			7 => 1,
			_ => -1
		};
	}

	private static async Task EncodeBase16Async(
		Stream input,
		WrappedAsciiWriter writer,
		CancellationToken cancellationToken
	) {
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			128 * 1024
		);
		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				for (
					var index = 0;
					index < read;
					index++
				) {
					var value = inputBuffer[ index ];
					outputBuffer[ index * 2 ] = checked(
						(byte)Base16Alphabet[
							value >> 4
						]
					);
					outputBuffer[ index * 2 + 1 ] = checked(
						(byte)Base16Alphabet[
							value & 0x0F
						]
					);
				}
				await writer.WriteAsync(
					outputBuffer.AsMemory(
						0,
						read * 2
					),
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static async Task DecodeBase16Async(
		Stream input,
		Stream output,
		bool ignoreGarbage,
		CancellationToken cancellationToken
	) {
		var lookup = CreateLookup(
			Base16Alphabet,
			caseInsensitive: true
		);
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var highNibble = -1;

		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}

				var outputCount = 0;
				for (
					var index = 0;
					index < read;
					index++
				) {
					var value = inputBuffer[ index ];
					if ( (byte)'\n' == value ) {
						continue;
					}
					var decoded = lookup[ value ];
					if ( decoded < 0 ) {
						if ( ignoreGarbage ) {
							continue;
						}
						throw InvalidInput();
					}
					if ( highNibble < 0 ) {
						highNibble = decoded;
					} else {
						outputBuffer[ outputCount++ ] = checked(
							(byte)(
								highNibble << 4 | decoded
							)
						);
						highNibble = -1;
					}
				}
				if ( 0 < outputCount ) {
					await output.WriteAsync(
						outputBuffer.AsMemory(
							0,
							outputCount
						),
						cancellationToken
					).ConfigureAwait( false );
				}
			}

			if ( 0 <= highNibble ) {
				throw InvalidInput();
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static async Task EncodeBase2Async(
		Stream input,
		WrappedAsciiWriter writer,
		bool leastSignificantBitFirst,
		CancellationToken cancellationToken
	) {
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			8192
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			8192 * 8
		);
		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				var outputIndex = 0;
				for (
					var index = 0;
					index < read;
					index++
				) {
					var value = inputBuffer[ index ];
					for (
						var bit = 0;
						bit < 8;
						bit++
					) {
						var shift = leastSignificantBitFirst
							? bit
							: 7 - bit
						;
						outputBuffer[ outputIndex++ ] = 0 != (
							value & (
								1 << shift
							)
						)
							? (byte)'1'
							: (byte)'0'
						;
					}
				}
				await writer.WriteAsync(
					outputBuffer.AsMemory(
						0,
						outputIndex
					),
					cancellationToken
				).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static async Task DecodeBase2Async(
		Stream input,
		Stream output,
		bool leastSignificantBitFirst,
		bool ignoreGarbage,
		CancellationToken cancellationToken
	) {
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			8192
		);
		var bitCount = 0;
		byte decodedByte = 0;

		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				var outputCount = 0;
				for (
					var index = 0;
					index < read;
					index++
				) {
					var value = inputBuffer[ index ];
					if ( (byte)'\n' == value ) {
						continue;
					}
					if (
						(byte)'0' != value
						&& (byte)'1' != value
					) {
						if ( ignoreGarbage ) {
							continue;
						}
						throw InvalidInput();
					}
					if ( (byte)'1' == value ) {
						var shift = leastSignificantBitFirst
							? bitCount
							: 7 - bitCount
						;
						decodedByte |= checked(
							(byte)(
								1 << shift
							)
						);
					}
					bitCount++;
					if ( 8 == bitCount ) {
						outputBuffer[ outputCount++ ] = decodedByte;
						decodedByte = 0;
						bitCount = 0;
					}
				}
				if ( 0 < outputCount ) {
					await output.WriteAsync(
						outputBuffer.AsMemory(
							0,
							outputCount
						),
						cancellationToken
					).ConfigureAwait( false );
				}
			}

			if ( 0 != bitCount ) {
				throw InvalidInput();
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static async Task EncodeZ85Async(
		Stream input,
		WrappedAsciiWriter writer,
		CancellationToken cancellationToken
	) {
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			80 * 1024
		);
		var pending = new byte[ 4 ];
		var pendingCount = 0;

		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				var outputCount = 0;
				for (
					var index = 0;
					index < read;
					index++
				) {
					pending[ pendingCount++ ] = inputBuffer[ index ];
					if ( 4 != pendingCount ) {
						continue;
					}
					EncodeZ85Group(
						pending,
						outputBuffer.AsSpan(
							outputCount,
							5
						)
					);
					outputCount += 5;
					pendingCount = 0;
				}
				if ( 0 < outputCount ) {
					await writer.WriteAsync(
						outputBuffer.AsMemory(
							0,
							outputCount
						),
						cancellationToken
					).ConfigureAwait( false );
				}
			}
			if ( 0 != pendingCount ) {
				throw InvalidInput();
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static void EncodeZ85Group(
		ReadOnlySpan<byte> input,
		Span<byte> output
	) {
		uint value = (
			(uint)input[ 0 ] << 24
		) | (
			(uint)input[ 1 ] << 16
		) | (
			(uint)input[ 2 ] << 8
		) | input[ 3 ];
		for (
			var index = 4;
			0 <= index;
			index--
		) {
			output[ index ] = checked(
				(byte)Z85Alphabet[
					(int)(
						value % 85
					)
				]
			);
			value /= 85;
		}
	}

	private static async Task DecodeZ85Async(
		Stream input,
		Stream output,
		bool ignoreGarbage,
		CancellationToken cancellationToken
	) {
		var lookup = CreateLookup(
			Z85Alphabet,
			caseInsensitive: false
		);
		var inputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var outputBuffer = ArrayPool<byte>.Shared.Rent(
			64 * 1024
		);
		var group = new int[ 5 ];
		var groupCount = 0;

		try {
			while ( true ) {
				var read = await input.ReadAsync(
					inputBuffer.AsMemory(),
					cancellationToken
				).ConfigureAwait( false );
				if ( 0 == read ) {
					break;
				}
				var outputCount = 0;
				for (
					var index = 0;
					index < read;
					index++
				) {
					var value = inputBuffer[ index ];
					if ( (byte)'\n' == value ) {
						continue;
					}
					var decoded = lookup[ value ];
					if ( decoded < 0 ) {
						if ( ignoreGarbage ) {
							continue;
						}
						throw InvalidInput();
					}
					group[ groupCount++ ] = decoded;
					if ( 5 != groupCount ) {
						continue;
					}

					ulong numeric = 0;
					for (
						var digit = 0;
						digit < group.Length;
						digit++
					) {
						numeric = numeric * 85 + checked(
							(uint)group[ digit ]
						);
					}
					if ( uint.MaxValue < numeric ) {
						throw InvalidInput();
					}
					var decodedValue = checked(
						(uint)numeric
					);
					outputBuffer[ outputCount++ ] = checked(
						(byte)(
							decodedValue >> 24
							& 0xFF
						)
					);
					outputBuffer[ outputCount++ ] = checked(
						(byte)(
							decodedValue >> 16
							& 0xFF
						)
					);
					outputBuffer[ outputCount++ ] = checked(
						(byte)(
							decodedValue >> 8
							& 0xFF
						)
					);
					outputBuffer[ outputCount++ ] = checked(
						(byte)(
							decodedValue
							& 0xFF
						)
					);
					groupCount = 0;
				}
				if ( 0 < outputCount ) {
					await output.WriteAsync(
						outputBuffer.AsMemory(
							0,
							outputCount
						),
						cancellationToken
					).ConfigureAwait( false );
				}
			}

			if ( 0 != groupCount ) {
				throw InvalidInput();
			}
		} finally {
			ArrayPool<byte>.Shared.Return(
				inputBuffer
			);
			ArrayPool<byte>.Shared.Return(
				outputBuffer
			);
		}
	}

	private static async Task EncodeBase58Async(
		Stream input,
		WrappedAsciiWriter writer,
		CancellationToken cancellationToken
	) {
		var data = await ReadAllBytesAsync(
			input,
			cancellationToken
		).ConfigureAwait( false );
		if ( 0 == data.Length ) {
			return;
		}

		var leadingZeroCount = 0;
		while (
			leadingZeroCount < data.Length
			&& 0 == data[ leadingZeroCount ]
		) {
			cancellationToken.ThrowIfCancellationRequested();
			leadingZeroCount++;
		}

		var value = new BigInteger(
			data,
			isUnsigned: true,
			isBigEndian: true
		);
		var encoded = new List<byte>();
		while ( !value.IsZero ) {
			cancellationToken.ThrowIfCancellationRequested();
			value = BigInteger.DivRem(
				value,
				58,
				out var remainder
			);
			encoded.Add(
				checked(
					(byte)Base58Alphabet[
						(int)remainder
					]
				)
			);
		}
		for (
			var index = 0;
			index < leadingZeroCount;
			index++
		) {
			encoded.Add(
				(byte)'1'
			);
		}
		encoded.Reverse();

		await writer.WriteAsync(
			encoded.ToArray(),
			cancellationToken
		).ConfigureAwait( false );
	}

	private static async Task DecodeBase58Async(
		Stream input,
		Stream output,
		bool ignoreGarbage,
		CancellationToken cancellationToken
	) {
		var data = await ReadAllBytesAsync(
			input,
			cancellationToken
		).ConfigureAwait( false );
		var lookup = CreateLookup(
			Base58Alphabet,
			caseInsensitive: false
		);
		var digits = new List<int>();
		foreach ( var character in data ) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( (byte)'\n' == character ) {
				continue;
			}
			var decoded = lookup[ character ];
			if ( decoded < 0 ) {
				if ( ignoreGarbage ) {
					continue;
				}
				throw InvalidInput();
			}
			digits.Add(
				decoded
			);
		}
		if ( 0 == digits.Count ) {
			return;
		}

		var leadingZeroCount = 0;
		while (
			leadingZeroCount < digits.Count
			&& 0 == digits[ leadingZeroCount ]
		) {
			cancellationToken.ThrowIfCancellationRequested();
			leadingZeroCount++;
		}

		BigInteger value = 0;
		foreach ( var digit in digits ) {
			cancellationToken.ThrowIfCancellationRequested();
			value = value * 58 + digit;
		}
		var decodedBytes = value.IsZero
			? Array.Empty<byte>()
			: value.ToByteArray(
				isUnsigned: true,
				isBigEndian: true
			)
		;

		if ( 0 < leadingZeroCount ) {
			await output.WriteAsync(
				new byte[ leadingZeroCount ],
				cancellationToken
			).ConfigureAwait( false );
		}
		if ( 0 < decodedBytes.Length ) {
			await output.WriteAsync(
				decodedBytes,
				cancellationToken
			).ConfigureAwait( false );
		}
	}

	private static int[] CreateLookup(
		string alphabet,
		bool caseInsensitive
	) {
		var lookup = Enumerable.Repeat(
			-1,
			256
		).ToArray();
		for (
			var index = 0;
			index < alphabet.Length;
			index++
		) {
			var character = checked(
				(byte)alphabet[ index ]
			);
			lookup[ character ] = index;
			if ( caseInsensitive ) {
				lookup[
					checked(
						(byte)char.ToLowerInvariant(
							(char)character
						)
					)
				] = index;
			}
		}
		return lookup;
	}

	private static async Task<byte[]> ReadAllBytesAsync(
		Stream input,
		CancellationToken cancellationToken
	) {
		await using var buffer = new MemoryStream();
		await input.CopyToAsync(
			buffer,
			64 * 1024,
			cancellationToken
		).ConfigureAwait( false );
		return buffer.ToArray();
	}

	private static BaseEncodingException InvalidInput() {
		return new BaseEncodingException(
			"invalid input"
		);
	}

	private sealed class WrappedAsciiWriter {

		private readonly Stream myOutput;
		private readonly long myWrapColumns;
		private long myColumn;
		private bool myWroteData;

		/// <summary>
		/// Initializes a new instance of the WrappedAsciiWriter class.
		/// </summary>
		public WrappedAsciiWriter(
			Stream output,
			long wrapColumns
		) {
			this.myOutput = output;
			this.myWrapColumns = wrapColumns;
		}

		/// <summary>
		/// Writes async.
		/// </summary>
		public async Task WriteAsync(
			ReadOnlyMemory<byte> data,
			CancellationToken cancellationToken
		) {
			if ( data.IsEmpty ) {
				return;
			}
			this.myWroteData = true;
			if ( 0 == this.myWrapColumns ) {
				await this.myOutput.WriteAsync(
					data,
					cancellationToken
				).ConfigureAwait( false );
				return;
			}

			while ( !data.IsEmpty ) {
				var room = this.myWrapColumns - this.myColumn;
				var count = checked(
					(int)Math.Min(
						room,
						data.Length
					)
				);
				await this.myOutput.WriteAsync(
					data.Slice(
						0,
						count
					),
					cancellationToken
				).ConfigureAwait( false );
				this.myColumn += count;
				data = data.Slice(
					count
				);
				if ( this.myColumn == this.myWrapColumns ) {
					await this.myOutput.WriteAsync(
						LineFeed,
						cancellationToken
					).ConfigureAwait( false );
					this.myColumn = 0;
				}
			}
		}

		/// <summary>
		/// Completes the operation and returns its result.
		/// </summary>
		public async Task CompleteAsync(
			CancellationToken cancellationToken
		) {
			if (
				this.myWroteData
				&& 0 < this.myWrapColumns
				&& 0 != this.myColumn
			) {
				await this.myOutput.WriteAsync(
					LineFeed,
					cancellationToken
				).ConfigureAwait( false );
			}
			await this.myOutput.FlushAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

	}

}
