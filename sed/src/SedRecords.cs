namespace Icod.LineEditor.Sed;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Icod.CoreUtils.Shared.CommandLine;
using Icod.CoreUtils.Shared.Diagnostics;
using Icod.CoreUtils.Shared.IO;
using Icod.CoreUtils.Shared.Processes;

// Responsibility: input-source and record framing.
public static partial class Command {

	private sealed class SourceSpec {

		public string Path {
			get;
		}

		public SourceSpec(
			string path
		) {
			this.Path = path;
		}

	}

	private sealed class AsyncRecordReader : IDisposable {

		private readonly DelimitedRecordReader myReader;
		private readonly bool myOwnsReader;
		private readonly TextReader myTextReader;

		public AsyncRecordReader(
			TextReader reader,
			bool nullData,
			bool ownsReader
		) {
			this.myTextReader = reader ?? throw new ArgumentNullException(
				nameof( reader )
			);
			this.myOwnsReader = ownsReader;
			this.myReader = new DelimitedRecordReader(
				reader,
				nullData
					? '\0'
					: '\n',
				bufferSize: 8192,
				trimCarriageReturn: !nullData
			);
		}

		public async Task<string?> ReadAsync(
			CancellationToken cancellationToken
		) {
			return await this.myReader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

		public void Dispose() {
			if ( this.myOwnsReader ) {
				this.myTextReader.Dispose();
			}
		}

	}

	private sealed class InputSequence : IDisposable {

		private AsyncRecordReader? myCurrentReader;
		private bool myInitialized;
		private string? myLookahead;
		private bool myLookaheadAvailable;
		private readonly bool myNullData;
		private int mySourceIndex = -1;
		private readonly IReadOnlyList<SourceSpec> mySources;
		private readonly TextReader myStandardInput;

		public string Current {
			get;
			private set;
		} = string.Empty;

		public bool IsLast {
			get;
			private set;
		}

		public int LineNumber {
			get;
			private set;
		}

		public InputSequence(
			IReadOnlyList<SourceSpec> sources,
			TextReader standardInput,
			bool nullData
		) {
			this.mySources = sources;
			this.myStandardInput = standardInput;
			this.myNullData = nullData;
		}

		public async Task<bool> MoveNextAsync(
			CancellationToken cancellationToken
		) {
			if ( !this.myInitialized ) {
				this.myInitialized = true;
				this.myLookahead = await this.ReadRawAsync(
					cancellationToken
				).ConfigureAwait( false );
				this.myLookaheadAvailable = null != this.myLookahead;
			}

			if ( !this.myLookaheadAvailable ) {
				return false;
			}

			this.Current = this.myLookahead ?? string.Empty;
			this.myLookahead = await this.ReadRawAsync(
				cancellationToken
			).ConfigureAwait( false );
			this.myLookaheadAvailable = null != this.myLookahead;
			this.IsLast = !this.myLookaheadAvailable;
			this.LineNumber++;
			return true;
		}

		private async Task<string?> ReadRawAsync(
			CancellationToken cancellationToken
		) {
			while ( true ) {
				if (
					null == this.myCurrentReader
					&& !this.OpenNextSource()
				) {
					return null;
				}

				var value = await this.myCurrentReader!.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( null != value ) {
					return value;
				}

				this.CloseCurrentReader();
			}
		}

		private bool OpenNextSource() {
			this.mySourceIndex++;
			if ( this.mySources.Count <= this.mySourceIndex ) {
				return false;
			}

			var source = this.mySources[ this.mySourceIndex ];
			if ( "-" == source.Path ) {
				this.myCurrentReader = new AsyncRecordReader(
					this.myStandardInput,
					this.myNullData,
					ownsReader: false
				);
			} else {
				this.myCurrentReader = new AsyncRecordReader(
					new StreamReader(
						new FileStream(
							source.Path,
							FileMode.Open,
							FileAccess.Read,
							FileShare.Read,
							8192,
							useAsync: true
						),
						Encoding.UTF8,
						detectEncodingFromByteOrderMarks: true,
						bufferSize: 8192,
						leaveOpen: false
					),
					this.myNullData,
					ownsReader: true
				);
			}
			return true;
		}

		private void CloseCurrentReader() {
			this.myCurrentReader?.Dispose();
			this.myCurrentReader = null;
		}

		public void Dispose() {
			this.CloseCurrentReader();
		}

	}


	private static async Task WriteRecordAsync(
		TextWriter writer,
		string value,
		bool nullData
	) {
		await writer.WriteAsync(
			value
		).ConfigureAwait( false );

		if ( nullData ) {
			await writer.WriteAsync(
				'\0'
			).ConfigureAwait( false );
		} else {
			await writer.WriteLineAsync().ConfigureAwait( false );
		}
	}

}
