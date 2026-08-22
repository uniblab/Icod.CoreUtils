namespace Icod.DiffUtils.Diff;

using System.Globalization;
using System.Text;
using Icod.CommandFramework.RegularExpressions;
using Icod.DiffUtils.Shared.Edits;
using Icod.DiffUtils.Shared.Layout;
using Icod.DiffUtils.Shared.Lines;

/// <summary>Writes the output formats owned by GNU <c>diff</c>.</summary>
internal sealed class DiffOutputWriter {
	private const string IncompleteLineMessage = "\\ No newline at end of file";
	private readonly DiffOptions options;
	private readonly TextWriter output;
	private readonly TextWriter error;
	private readonly CancellationToken cancellationToken;

	/// <summary>Initializes an output writer.</summary>
	public DiffOutputWriter(
		DiffOptions options,
		TextWriter output,
		TextWriter error,
		CancellationToken cancellationToken
	) {
		this.options = options ?? throw new ArgumentNullException( nameof( options ) );
		this.output = output ?? throw new ArgumentNullException( nameof( output ) );
		this.error = error ?? throw new ArgumentNullException( nameof( error ) );
		this.cancellationToken = cancellationToken;
	}

	/// <summary>Writes the selected representation of one comparison.</summary>
	public async Task WriteAsync( DiffInput oldInput, DiffInput newInput, EditScript script ) {
		ArgumentNullException.ThrowIfNull( oldInput );
		ArgumentNullException.ThrowIfNull( newInput );
		ArgumentNullException.ThrowIfNull( script );
		switch ( this.options.OutputStyle ) {
			case DiffOutputStyle.Brief:
				await this.WriteLineAsync( $"Files {oldInput.HeaderName} and {newInput.HeaderName} differ" ).ConfigureAwait( false );
				break;
			case DiffOutputStyle.Context:
				await this.WriteContextAsync( oldInput, newInput, script ).ConfigureAwait( false );
				break;
			case DiffOutputStyle.Unified:
				await this.WriteUnifiedAsync( oldInput, newInput, script ).ConfigureAwait( false );
				break;
			case DiffOutputStyle.Ed:
				await this.WriteEdAsync( script, reverseOrder: true ).ConfigureAwait( false );
				break;
			case DiffOutputStyle.ForwardEd:
				await this.WriteEdAsync( script, reverseOrder: false ).ConfigureAwait( false );
				break;
			case DiffOutputStyle.Rcs:
				await this.WriteRcsAsync( script ).ConfigureAwait( false );
				break;
			case DiffOutputStyle.SideBySide:
				await this.WriteSideBySideAsync( script ).ConfigureAwait( false );
				break;
			case DiffOutputStyle.IfDef:
				await this.WriteIfDefAsync( script ).ConfigureAwait( false );
				break;
			default:
				await this.WriteNormalAsync( script ).ConfigureAwait( false );
				break;
		}
	}

	/// <summary>Writes an identical-files report when requested.</summary>
	public async Task WriteIdenticalAsync( DiffInput oldInput, DiffInput newInput ) {
		if ( this.options.ReportIdenticalFiles ) {
			await this.WriteLineAsync( $"Files {oldInput.HeaderName} and {newInput.HeaderName} are identical" ).ConfigureAwait( false );
		}
	}

	/// <summary>Writes the standard binary-files-differ report.</summary>
	public Task WriteBinaryDifferenceAsync( DiffInput oldInput, DiffInput newInput ) {
		return this.WriteLineAsync( $"Binary files {oldInput.HeaderName} and {newInput.HeaderName} differ" );
	}

	/// <summary>Writes incomplete-input diagnostics required by the ed-oriented formats.</summary>
	/// <returns><see langword="true"/> when an incomplete input makes the comparison troublesome.</returns>
	public async Task<bool> WriteEdIncompleteWarningsAsync( DiffInput oldInput, DiffInput newInput ) {
		if ( this.options.OutputStyle is not DiffOutputStyle.Ed and not DiffOutputStyle.ForwardEd ) {
			return false;
		}
		var oldIncomplete = HasIncompleteFinalLine( oldInput );
		var newIncomplete = HasIncompleteFinalLine( newInput );
		if ( oldIncomplete ) {
			await this.WriteIncompleteEdWarningAsync( oldInput ).ConfigureAwait( false );
		}
		if ( newIncomplete ) {
			await this.WriteIncompleteEdWarningAsync( newInput ).ConfigureAwait( false );
		}
		return oldIncomplete || newIncomplete;
	}

	/// <summary>Filters difference blocks suppressed by blank-line and matching-line policies.</summary>
	public EditScript ApplyDifferenceFilters( EditScript script ) {
		ArgumentNullException.ThrowIfNull( script );
		if ( !this.options.IgnoreBlankLines && 0 == this.options.IgnoredLinePatterns.Count ) {
			return script;
		}
		var differences = script.Differences.Where( difference => !this.IsIgnoredDifference( difference ) ).ToArray();
		return new EditScript( script.Operations, Array.AsReadOnly( differences ) );
	}

	private bool IsIgnoredDifference( DifferenceBlock difference ) {
		var changedLines = difference.Operations
			.Where( operation => EditOperationKind.Equal != operation.Kind )
			.Select( operation => operation.Line )
			.ToArray();
		if ( 0 == changedLines.Length ) {
			return false;
		}
		foreach ( var line in changedLines ) {
			if ( this.options.IgnoreBlankLines && LineNormalizer.IsBlank( line, this.options.ComparisonPolicy ) ) {
				continue;
			}
			var matched = false;
			foreach ( var expression in this.options.IgnoredLinePatterns ) {
				var result = expression.Match( GetOutputContent( line, this.options ), cancellationToken: this.cancellationToken );
				if ( !result.IsSuccess ) {
					throw new IOException( result.Diagnostic?.Message ?? "regular-expression matching failed" );
				}
				if ( result.IsMatch ) {
					matched = true;
					break;
				}
			}
			if ( !matched ) {
				return false;
			}
		}
		return true;
	}

	private async Task WriteNormalAsync( EditScript script ) {
		foreach ( var difference in script.Differences ) {
			if ( 0 == difference.OldLength ) {
				await this.WriteLineAsync( $"{difference.OldStart}a{FormatNormalRange( difference.NewStart, difference.NewLength )}" ).ConfigureAwait( false );
				foreach ( var operation in difference.Operations.Where( operation => EditOperationKind.Insert == operation.Kind ) ) {
					await this.WritePrefixedLineAsync( "> ", operation.Line ).ConfigureAwait( false );
				}
				continue;
			}
			if ( 0 == difference.NewLength ) {
				await this.WriteLineAsync( $"{FormatNormalRange( difference.OldStart, difference.OldLength )}d{difference.NewStart}" ).ConfigureAwait( false );
				foreach ( var operation in difference.Operations.Where( operation => EditOperationKind.Delete == operation.Kind ) ) {
					await this.WritePrefixedLineAsync( "< ", operation.Line ).ConfigureAwait( false );
				}
				continue;
			}
			await this.WriteLineAsync(
				$"{FormatNormalRange( difference.OldStart, difference.OldLength )}c{FormatNormalRange( difference.NewStart, difference.NewLength )}"
			).ConfigureAwait( false );
			foreach ( var operation in difference.Operations.Where( operation => EditOperationKind.Delete == operation.Kind ) ) {
				await this.WritePrefixedLineAsync( "< ", operation.Line ).ConfigureAwait( false );
			}
			await this.WriteLineAsync( "---" ).ConfigureAwait( false );
			foreach ( var operation in difference.Operations.Where( operation => EditOperationKind.Insert == operation.Kind ) ) {
				await this.WritePrefixedLineAsync( "> ", operation.Line ).ConfigureAwait( false );
			}
		}
	}

	private async Task WriteUnifiedAsync( DiffInput oldInput, DiffInput newInput, EditScript script ) {
		await this.WriteLineAsync( BuildHeader( "---", oldInput, 0 ) ).ConfigureAwait( false );
		await this.WriteLineAsync( BuildHeader( "+++", newInput, 1 ) ).ConfigureAwait( false );
		foreach ( var hunk in LineDiffEngine.BuildHunks( script, this.options.ContextLines ) ) {
			var function = await this.FindFunctionAsync( oldInput.Document.Lines, hunk.OldStart ).ConfigureAwait( false );
			var suffix = string.IsNullOrEmpty( function ) ? string.Empty : string.Concat( " ", function );
			await this.WriteLineAsync(
				$"@@ -{FormatUnifiedRange( hunk.OldStart, hunk.OldLength )} +{FormatUnifiedRange( hunk.NewStart, hunk.NewLength )} @@{suffix}"
			).ConfigureAwait( false );
			foreach ( var operation in hunk.Operations ) {
				var prefix = operation.Kind switch {
					EditOperationKind.Delete => "-",
					EditOperationKind.Insert => "+",
					_ => " "
				};
				await this.WritePrefixedLineAsync( prefix, operation.Line ).ConfigureAwait( false );
			}
		}
	}

	private async Task WriteContextAsync( DiffInput oldInput, DiffInput newInput, EditScript script ) {
		await this.WriteLineAsync( BuildHeader( "***", oldInput, 0 ) ).ConfigureAwait( false );
		await this.WriteLineAsync( BuildHeader( "---", newInput, 1 ) ).ConfigureAwait( false );
		foreach ( var hunk in LineDiffEngine.BuildHunks( script, this.options.ContextLines ) ) {
			var function = await this.FindFunctionAsync( oldInput.Document.Lines, hunk.OldStart ).ConfigureAwait( false );
			var suffix = string.IsNullOrEmpty( function ) ? string.Empty : string.Concat( " ", function );
			await this.WriteLineAsync( $"***************{suffix}" ).ConfigureAwait( false );
			await this.WriteLineAsync( $"*** {FormatContextRange( hunk.OldStart, hunk.OldLength )} ****" ).ConfigureAwait( false );
			await this.WriteContextSideAsync( hunk.Operations, oldSide: true ).ConfigureAwait( false );
			await this.WriteLineAsync( $"--- {FormatContextRange( hunk.NewStart, hunk.NewLength )} ----" ).ConfigureAwait( false );
			await this.WriteContextSideAsync( hunk.Operations, oldSide: false ).ConfigureAwait( false );
		}
	}

	private async Task WriteContextSideAsync( IReadOnlyList<EditOperation> operations, bool oldSide ) {
		var requiredKind = oldSide ? EditOperationKind.Delete : EditOperationKind.Insert;
		if ( !operations.Any( operation => requiredKind == operation.Kind ) ) {
			return;
		}
		var index = 0;
		while ( index < operations.Count ) {
			if ( EditOperationKind.Equal == operations[index].Kind ) {
				await this.WritePrefixedLineAsync( "  ", operations[index].Line ).ConfigureAwait( false );
				index++;
				continue;
			}
			var start = index;
			var hasDelete = false;
			var hasInsert = false;
			while ( index < operations.Count && EditOperationKind.Equal != operations[index].Kind ) {
				hasDelete |= EditOperationKind.Delete == operations[index].Kind;
				hasInsert |= EditOperationKind.Insert == operations[index].Kind;
				index++;
			}
			for ( var current = start; current < index; current++ ) {
				var operation = operations[current];
				if ( oldSide && EditOperationKind.Delete == operation.Kind ) {
					await this.WritePrefixedLineAsync( hasInsert ? "! " : "- ", operation.Line ).ConfigureAwait( false );
				} else if ( !oldSide && EditOperationKind.Insert == operation.Kind ) {
					await this.WritePrefixedLineAsync( hasDelete ? "! " : "+ ", operation.Line ).ConfigureAwait( false );
				}
			}
		}
	}

	private async Task WriteEdAsync( EditScript script, bool reverseOrder ) {
		IEnumerable<DifferenceBlock> differences = reverseOrder
			? script.Differences.Reverse()
			: script.Differences;
		foreach ( var difference in differences ) {
			var commandCharacter = 0 == difference.OldLength
				? 'a'
				: 0 == difference.NewLength ? 'd' : 'c';
			var address = 0 == difference.OldLength
				? difference.OldStart.ToString( CultureInfo.InvariantCulture )
				: reverseOrder
					? FormatEdRange( difference.OldStart, difference.OldLength )
					: FormatForwardEdRange( difference.OldStart, difference.OldLength );
			var command = reverseOrder
				? string.Concat( address, commandCharacter )
				: string.Concat( commandCharacter, address );
			await this.WriteLineAsync( command ).ConfigureAwait( false );
			if ( 0 == difference.NewLength ) {
				continue;
			}
			var escapedPeriod = false;
			foreach ( var operation in difference.Operations.Where( operation => EditOperationKind.Insert == operation.Kind ) ) {
				var content = GetOutputContent( operation.Line, this.options );
				if ( reverseOrder && "." == content ) {
					content = "..";
					escapedPeriod = true;
				}
				await this.WriteLineAsync( content ).ConfigureAwait( false );
			}
			await this.WriteLineAsync( "." ).ConfigureAwait( false );
			if ( escapedPeriod ) {
				await this.WriteLineAsync( "s/.//" ).ConfigureAwait( false );
			}
		}
	}

	private async Task WriteRcsAsync( EditScript script ) {
		foreach ( var difference in script.Differences ) {
			if ( 0 < difference.OldLength ) {
				await this.WriteLineAsync( $"d{difference.OldStart + 1} {difference.OldLength}" ).ConfigureAwait( false );
			}
			if ( 0 < difference.NewLength ) {
				await this.WriteLineAsync( $"a{difference.OldStart + difference.OldLength} {difference.NewLength}" ).ConfigureAwait( false );
				foreach ( var operation in difference.Operations.Where( operation => EditOperationKind.Insert == operation.Kind ) ) {
					await this.WriteRawLineAsync( operation.Line, markIncomplete: false ).ConfigureAwait( false );
				}
			}
		}
	}

	private async Task WriteSideBySideAsync( EditScript script ) {
		var fieldWidth = Math.Max( 1, ( this.options.Width - 3 ) / 2 );
		if ( !script.HasDifferences ) {
			foreach ( var operation in script.Operations.Where( operation => operation.OldIndex.HasValue ) ) {
				var text = SideBySideLayout.FitText(
					GetSideContent( operation.Line.Content, this.options ),
					fieldWidth,
					this.options.TabSize,
					this.options.ExpandTabs
				);
				if ( this.options.SuppressCommonLines ) {
					continue;
				}
				await this.WriteLineAsync(
					this.options.LeftColumn ? text : string.Concat( text.PadRight( fieldWidth ), "   ", text )
				).ConfigureAwait( false );
			}
			return;
		}
		foreach ( var row in SideBySideLayout.BuildRows( script ) ) {
			if ( row.IsCommon && this.options.SuppressCommonLines ) {
				continue;
			}
			var left = SideBySideLayout.FitText(
				GetSideContent( row.Left ?? string.Empty, this.options ),
				fieldWidth,
				this.options.TabSize,
				this.options.ExpandTabs
			);
			if ( row.IsCommon && this.options.LeftColumn ) {
				await this.WriteLineAsync( left ).ConfigureAwait( false );
				continue;
			}
			var right = SideBySideLayout.FitText(
				GetSideContent( row.Right ?? string.Empty, this.options ),
				fieldWidth,
				this.options.TabSize,
				this.options.ExpandTabs
			);
			if ( '<' == row.Marker ) {
				await this.WriteLineAsync( string.Concat( left.PadRight( fieldWidth ), " <" ) ).ConfigureAwait( false );
			} else if ( '>' == row.Marker ) {
				await this.WriteLineAsync( string.Concat( new string( ' ', fieldWidth ), " > ", right ) ).ConfigureAwait( false );
			} else {
				await this.WriteLineAsync( string.Concat( left.PadRight( fieldWidth ), " ", row.Marker, " ", right ) ).ConfigureAwait( false );
			}
		}
	}

	private async Task WriteIfDefAsync( EditScript script ) {
		var symbol = this.options.IfDefName ?? throw new InvalidOperationException( "An ifdef symbol is required." );
		var operations = script.Operations;
		if ( !script.HasDifferences ) {
			foreach ( var operation in operations.Where( operation => operation.OldIndex.HasValue ) ) {
				await this.WriteMergedLineAsync( operation.Line ).ConfigureAwait( false );
			}
			return;
		}
		var index = 0;
		while ( index < operations.Count ) {
			if ( EditOperationKind.Equal == operations[index].Kind ) {
				await this.WriteMergedLineAsync( operations[index++].Line ).ConfigureAwait( false );
				continue;
			}
			var deletes = new List<ComparisonLine>();
			var inserts = new List<ComparisonLine>();
			while ( index < operations.Count && EditOperationKind.Equal != operations[index].Kind ) {
				var operation = operations[index++];
				if ( EditOperationKind.Delete == operation.Kind ) {
					deletes.Add( operation.Line );
				} else {
					inserts.Add( operation.Line );
				}
			}
			if ( 0 < deletes.Count ) {
				await this.WriteLineAsync( $"#ifndef {symbol}" ).ConfigureAwait( false );
				foreach ( var line in deletes ) {
					await this.WriteMergedLineAsync( line ).ConfigureAwait( false );
				}
			}
			if ( 0 < inserts.Count ) {
				await this.WriteLineAsync( 0 < deletes.Count ? $"#else /* {symbol} */" : $"#ifdef {symbol}" ).ConfigureAwait( false );
				foreach ( var line in inserts ) {
					await this.WriteMergedLineAsync( line ).ConfigureAwait( false );
				}
			}
			await this.WriteLineAsync( 0 < inserts.Count ? $"#endif /* {symbol} */" : $"#endif /* ! {symbol} */" ).ConfigureAwait( false );
		}
	}

	private static bool HasIncompleteFinalLine( DiffInput input ) {
		return 0 < input.Document.Lines.Count && !input.Document.Lines[^1].HasLineTerminator;
	}

	private async ValueTask WriteIncompleteEdWarningAsync( DiffInput input ) {
		if ( 0 == input.Document.Lines.Count || input.Document.Lines[^1].HasLineTerminator ) {
			return;
		}
		await this.error.WriteLineAsync(
			$"diff: {input.DisplayName}: No newline at end of file".AsMemory(),
			CancellationToken.None
		).ConfigureAwait( false );
		await this.error.WriteLineAsync( ReadOnlyMemory<char>.Empty, CancellationToken.None ).ConfigureAwait( false );
	}

	private async ValueTask WriteMergedLineAsync( ComparisonLine line ) {
		var content = GetOutputContent( line, this.options );
		if ( this.options.ExpandTabs ) {
			content = ExpandTabs( content, this.options.TabSize );
		}
		await this.output.WriteLineAsync( content.AsMemory(), this.cancellationToken ).ConfigureAwait( false );
	}

	private async ValueTask<string?> FindFunctionAsync( IReadOnlyList<ComparisonLine> lines, int oldStart ) {
		if ( null == this.options.FunctionExpression && !this.options.ShowCFunction ) {
			return null;
		}
		for ( var index = Math.Min( oldStart, lines.Count ) - 1; 0 <= index; index-- ) {
			this.cancellationToken.ThrowIfCancellationRequested();
			var value = GetOutputContent( lines[index], this.options );
			if ( null != this.options.FunctionExpression ) {
				var result = await this.options.FunctionExpression.MatchAsync(
					value,
					cancellationToken: this.cancellationToken
				).ConfigureAwait( false );
				if ( !result.IsSuccess ) {
					throw new IOException( result.Diagnostic?.Message ?? "regular-expression matching failed" );
				}
				if ( result.IsMatch ) {
					return value;
				}
			} else if ( LooksLikeCFunction( value ) ) {
				return value;
			}
		}
		return null;
	}

	private static bool LooksLikeCFunction( string value ) {
		var trimmed = value.TrimStart();
		return 0 < trimmed.Length
			&& ( char.IsLetter( trimmed[0] ) || '_' == trimmed[0] )
			&& 0 < trimmed.IndexOf( '(' )
			&& !trimmed.EndsWith( ";", StringComparison.Ordinal );
	}

	private string BuildHeader( string marker, DiffInput input, int labelIndex ) {
		if ( labelIndex < this.options.Labels.Count ) {
			return string.Concat( marker, " ", this.options.Labels[labelIndex] );
		}
		var timestamp = input.Modified ?? ( input.IsMissing ? DateTimeOffset.UnixEpoch : null );
		return timestamp.HasValue
			? string.Concat( marker, " ", input.HeaderName, "\t", FormatTimestamp( timestamp.Value ) )
			: string.Concat( marker, " ", input.HeaderName );
	}

	private async ValueTask WritePrefixedLineAsync( string prefix, ComparisonLine line ) {
		if ( this.options.InitialTab && 0 < prefix.Length ) {
			prefix = char.IsWhiteSpace( prefix[^1] )
				? string.Concat( prefix.AsSpan( 0, prefix.Length - 1 ), "\t" )
				: string.Concat( prefix, "\t" );
		}
		if ( this.options.SuppressBlankEmpty && 0 == GetOutputContent( line, this.options ).Length ) {
			prefix = prefix.TrimEnd( ' ', '\t' );
		}
		await this.output.WriteAsync( prefix.AsMemory(), this.cancellationToken ).ConfigureAwait( false );
		await this.WriteRawLineAsync( line, markIncomplete: true ).ConfigureAwait( false );
	}

	private async ValueTask WriteRawLineAsync( ComparisonLine line, bool markIncomplete ) {
		var content = GetOutputContent( line, this.options );
		if ( this.options.ExpandTabs ) {
			content = ExpandTabs( content, this.options.TabSize );
		}
		await this.output.WriteAsync( content.AsMemory(), this.cancellationToken ).ConfigureAwait( false );
		if ( line.HasLineTerminator || markIncomplete ) {
			await this.output.WriteLineAsync( ReadOnlyMemory<char>.Empty, this.cancellationToken ).ConfigureAwait( false );
		}
		if ( markIncomplete && !line.HasLineTerminator ) {
			await this.WriteLineAsync( IncompleteLineMessage ).ConfigureAwait( false );
		}
	}

	private Task WriteLineAsync( string value ) {
		return this.output.WriteLineAsync( value.AsMemory(), this.cancellationToken );
	}

	private static string GetSideContent( string value, DiffOptions options ) {
		return options.ComparisonPolicy.StripTrailingCarriageReturn && 0 < value.Length && '\r' == value[^1]
			? value[..^1]
			: value;
	}

	private static string GetOutputContent( ComparisonLine line, DiffOptions options ) {
		var value = line.Content;
		if ( options.ComparisonPolicy.StripTrailingCarriageReturn && 0 < value.Length && '\r' == value[^1] ) {
			value = value[..^1];
		}
		return value;
	}

	private static string ExpandTabs( string value, int tabSize ) {
		var builder = new StringBuilder( value.Length );
		var column = 0;
		foreach ( var character in value ) {
			if ( '\t' == character ) {
				var count = tabSize - ( column % tabSize );
				builder.Append( ' ', count );
				column += count;
			} else {
				builder.Append( character );
				column++;
			}
		}
		return builder.ToString();
	}

	private static string FormatNormalRange( int start, int length ) {
		var first = start + 1;
		return 1 == length
			? first.ToString( CultureInfo.InvariantCulture )
			: string.Concat( first.ToString( CultureInfo.InvariantCulture ), ",", ( start + length ).ToString( CultureInfo.InvariantCulture ) );
	}

	private static string FormatEdRange( int start, int length ) => FormatNormalRange( start, length );

	private static string FormatForwardEdRange( int start, int length ) {
		var first = start + 1;
		return 1 == length
			? first.ToString( CultureInfo.InvariantCulture )
			: string.Concat(
				first.ToString( CultureInfo.InvariantCulture ),
				" ",
				( start + length ).ToString( CultureInfo.InvariantCulture )
			);
	}

	private static string FormatUnifiedRange( int start, int length ) {
		var line = 0 == length ? start : start + 1;
		return 1 == length
			? line.ToString( CultureInfo.InvariantCulture )
			: string.Concat( line.ToString( CultureInfo.InvariantCulture ), ",", length.ToString( CultureInfo.InvariantCulture ) );
	}

	private static string FormatContextRange( int start, int length ) {
		if ( 0 == length ) {
			return start.ToString( CultureInfo.InvariantCulture );
		}
		var first = start + 1;
		return 1 == length
			? first.ToString( CultureInfo.InvariantCulture )
			: string.Concat( first.ToString( CultureInfo.InvariantCulture ), ",", ( start + length ).ToString( CultureInfo.InvariantCulture ) );
	}

	private static string FormatTimestamp( DateTimeOffset value ) {
		return value.ToString( "yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture );
	}
}
