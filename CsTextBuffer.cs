using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Utility;
using System.Text;
using System;

namespace CSharpCodeParser
{


	public class CsTextBuffer
	{

		public enum BlockState : byte
		{
			None = 0,
			CommentBlock = 1,
			StringBlock = 2,
		}

		public class FormatedLine
		{
			public BlockState blockState;
			public List<SyntaxToken> tokens;
			[System.NonSerialized]
			public int index;

			public override string ToString()
			{
				return ExtensionUtility.ToString (tokens);
			}
		}
		

		public FormatedLine[] formatedLines = new FormatedLine[0];

		public List<string> lines = new List<string>();

		public CsParser parser = new CsParser();

		private string lineEnding = "\n";


		public void LoadCsCode(string content)
		{
			lines = new List<string>(content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
			formatedLines = new FormatedLine[lines.Count-1];
			for (int i=0; i<lines.Count-1; i++) 
			{
				FormatLine(i);
			}
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder ();
			foreach(var formatedLine in this.formatedLines)
			{
				sb.AppendLine(formatedLine.ToString());
			}
			return sb.ToString();
		}

		private void FormatLine(int lineIndex)
		{
			var formateLine = this.formatedLines [lineIndex];
			if (formateLine == null) 
			{
				formateLine = new FormatedLine();
				this.formatedLines[lineIndex] = formateLine;
				formateLine.index = lineIndex;
			}
			if(lineIndex > 0)
			{
				formateLine.blockState = this.formatedLines[lineIndex -1].blockState;
			}
			parser.Tokenize (this.lines[lineIndex], formateLine);

		}
	}

	public class Scanner: IEnumerator<SyntaxToken>
	{
		public readonly string fileName;
		
		readonly CsGrammar grammar;
		readonly CsTextBuffer.FormatedLine[] lines;
		List<SyntaxToken> tokens;
		
		int currentLine = -1;
		int currentTokenIndex = -1;
		
		private static SyntaxToken EOF;
		
		public CsGrammar.Node CurrentGrammarNode { get; set; }
		private ParseTree.Node _currentPTN;
		public ParseTree.Node CurrentParseTreeNode {
			get { return _currentPTN; }
			set { _currentPTN = value; }
		}
		
		private int maxScanDistance;
		public bool KeepScanning { get { return maxScanDistance > 0; } }
		
		public int CurrentLine() { return currentLine + 1; }
		public int CurrentTokenIndex() { return currentTokenIndex; }
		
		public Scanner(CsGrammar grammar, CsTextBuffer.FormatedLine[] formatedLines, string fileName)
		{
			this.grammar = grammar;
			this.fileName = fileName;
			lines = formatedLines;
			
			if (EOF == null)
			EOF = new SyntaxToken(SyntaxToken.Kind.EOF, string.Empty) { tokenId = grammar.tokenEOF };
		}
		
		public bool Lookahead(CsGrammar.Node node, int maxDistance = int.MaxValue)
		{
			if (tokens == null && currentLine > 0)
				return false;
			
			
			var line = currentLine;
			var index = currentTokenIndex;
			//	var realIndex = nonTriviaTokenIndex;
			
			var temp = maxScanDistance;
			maxScanDistance = maxDistance;
			var match = node.Scan(this);
			maxScanDistance = temp;
			
			currentLine = line;
			currentTokenIndex = index;
			//	nonTriviaTokenIndex = realIndex;
			tokens = currentLine < lines.Length ? lines[currentLine].tokens : null;
			
			return match;
		}
		public SyntaxToken Lookahead(int offset, bool skipWhitespace = true)
		{
			if (!skipWhitespace)
			{
				return currentTokenIndex + 1 < tokens.Count ? tokens[currentTokenIndex + 1] : EOF;
			}
			
			var t = tokens;
			var cl = currentLine;
			var cti = currentTokenIndex;
			
			while (offset --> 0)
			{
				if (!MoveNext())
				{
					tokens = t;
					currentLine = cl;
					currentTokenIndex = cti;
					return EOF;
				}
			}
			var token = tokens[currentTokenIndex];
			
			tokens = t;
			currentLine = cl;
			currentTokenIndex = cti;
			return token;
		}
		
		public SyntaxToken CurrentToken()
		{
			return null;
		}
		
		public bool Seeking { get; set; }
		
		public SyntaxToken Current
		{
			get
			{
				return tokens != null ? tokens[currentTokenIndex] : EOF;
			}
		}
		
		object System.Collections.IEnumerator.Current
		{
			get { return Current; }
		}
		
		public bool MoveNext()
		{
			if (maxScanDistance > 0)
				--maxScanDistance;
			
			while (MoveNextSingle())
			{
				if (tokens[currentTokenIndex].tokenId == -1)
				{
					var token = tokens[currentTokenIndex];
					switch (token.tokenKind)
					{
					case SyntaxToken.Kind.Missing:
					case SyntaxToken.Kind.Whitespace:
					case SyntaxToken.Kind.Comment:
					case SyntaxToken.Kind.EOF:
					case SyntaxToken.Kind.Preprocessor:
					case SyntaxToken.Kind.PreprocessorSymbol:
					case SyntaxToken.Kind.PreprocessorArguments:
					case SyntaxToken.Kind.VerbatimStringLiteral:
						break;
					case SyntaxToken.Kind.Punctuator:
					case SyntaxToken.Kind.Keyword:
					case SyntaxToken.Kind.BuiltInLiteral:
						token.tokenId = grammar.TokenToId(token.text);
						break;
					case SyntaxToken.Kind.Identifier:
					case SyntaxToken.Kind.ContextualKeyword:
						token.tokenId = grammar.tokenIdentifier;
						break;
					case SyntaxToken.Kind.IntegerLiteral:
					case SyntaxToken.Kind.RealLiteral:
					case SyntaxToken.Kind.CharLiteral:
					case SyntaxToken.Kind.StringLiteral:
					case SyntaxToken.Kind.VerbatimStringBegin:
						token.tokenId = grammar.tokenLiteral;
						break;
					default:
						throw new ArgumentOutOfRangeException();
					}
				}
				
				if (tokens[currentTokenIndex].tokenKind > SyntaxToken.Kind.VerbatimStringLiteral)
				{
					return true;
				}
			}
			tokens = null;
			++currentLine;
			currentTokenIndex = -1;
			return false;
		}
		
		public bool MoveNextSingle()
		{
			while (tokens == null)
			{
				if (currentLine + 1 >= lines.Length)
					return false;
				currentTokenIndex = -1;
				tokens = lines[++currentLine].tokens;
			}
			while (currentTokenIndex + 1 >= tokens.Count)
			{
				if (currentLine + 1 >= lines.Length)
				{
					tokens = null;
					return false;
				}
				currentTokenIndex = -1;
				tokens = lines[++currentLine].tokens;
				while (tokens == null)
				{
					if (currentLine + 1 >= lines.Length)
						return false;
					tokens = lines[++currentLine].tokens;
				}
			}
			++currentTokenIndex;
			return true;
		}
		
		public void Reset()
		{
		}
		
		public void Dispose()
		{
		}
	}

}
