namespace Icod.CoreUtils.Shared.IO;

using System.Text;
using Icod.CoreUtils.Shared.Diagnostics;

/// <summary>
/// Owns a text or binary input source opened from an operand.
/// </summary>
public sealed class InputSource : IDisposable, IAsyncDisposable {

	private readonly object? myOwnedResource;

	/// <summary>Gets the binary stream when opened in binary mode.</summary>
	public Stream? BinaryStream {
		get;
	}

	/// <summary>Gets a user-facing source name.</summary>
	public string DisplayName {
		get;
	}

	/// <summary>Gets whether this source is standard input.</summary>
	public bool IsStandardInput {
		get;
	}

	/// <summary>Gets the text reader when opened in text mode.</summary>
	public TextReader? TextReader {
		get;
	}

	private InputSource(
		string displayName,
		bool isStandardInput,
		TextReader? textReader,
		Stream? binaryStream,
		object? ownedResource
	) {
		this.DisplayName = displayName;
		this.IsStandardInput = isStandardInput;
		this.TextReader = textReader;
		this.BinaryStream = binaryStream;
		this.myOwnedResource = ownedResource;
	}

	/// <summary>Opens an operand for asynchronous binary access.</summary>
	public static InputSource OpenBinary(
		InputOperand operand,
		CommandContext context,
		int bufferSize = StreamOperations.DefaultBufferSize
	) {
		ArgumentNullException.ThrowIfNull(
			context
		);
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}
		if ( operand.IsStandardInput ) {
			return new InputSource(
				operand.DisplayName,
				true,
				null,
				context.StandardInputStream ?? throw new InvalidOperationException(
					"A binary standard-input stream was not supplied."
				),
				null
			);
		}

		var stream = new FileStream(
			operand.Value,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete,
			bufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		return new InputSource(
			operand.DisplayName,
			false,
			null,
			stream,
			stream
		);
	}

	/// <summary>Opens an operand for asynchronous text access.</summary>
	public static InputSource OpenText(
		InputOperand operand,
		CommandContext context,
		Encoding? encoding = null,
		bool detectEncodingFromByteOrderMarks = true,
		int bufferSize = 4096
	) {
		ArgumentNullException.ThrowIfNull(
			context
		);
		if ( bufferSize <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( bufferSize )
			);
		}
		if ( operand.IsStandardInput ) {
			return new InputSource(
				operand.DisplayName,
				true,
				context.StandardInput,
				null,
				null
			);
		}

		var stream = new FileStream(
			operand.Value,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete,
			StreamOperations.DefaultBufferSize,
			FileOptions.Asynchronous | FileOptions.SequentialScan
		);
		var reader = new StreamReader(
			stream,
			encoding ?? Encoding.UTF8,
			detectEncodingFromByteOrderMarks,
			bufferSize,
			leaveOpen: false
		);
		return new InputSource(
			operand.DisplayName,
			false,
			reader,
			null,
			reader
		);
	}

	/// <inheritdoc/>
	public void Dispose() {
		if ( this.myOwnedResource is IDisposable disposable ) {
			disposable.Dispose();
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync() {
		if ( this.myOwnedResource is IAsyncDisposable asynchronous ) {
			await asynchronous.DisposeAsync().ConfigureAwait( false );
		} else if ( this.myOwnedResource is IDisposable disposable ) {
			disposable.Dispose();
		}
	}

}
