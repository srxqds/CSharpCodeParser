using UnityEngine;
using System.Collections;
using System;

namespace CSharpCodeParser
{
	using Debug = UnityEngine.Debug;

	public class SyntaxToken //: IComparable<SyntaxToken>
	{
		public enum Kind
		{
			Missing,
			Whitespace,
			Comment,
			Preprocessor,
			PreprocessorArguments,
			PreprocessorSymbol,
			PreprocessorDirectiveExpected,
			PreprocessorCommentExpected,
			PreprocessorUnexpectedDirective,
			VerbatimStringLiteral,
			
			LastWSToken, // Marker only
			
			VerbatimStringBegin,
			BuiltInLiteral,
			CharLiteral,
			StringLiteral,
			IntegerLiteral,
			RealLiteral,
			Punctuator,
			Keyword,
			Identifier,
			ContextualKeyword,
			EOF,
		}
		
		public Kind tokenKind;
		public GUIStyle style;
		public ParseTree.Leaf parent;
		//public TextSpan textSpan;
		public string text;
		public int tokenId;
		
		public CsTextBuffer.FormatedLine formatedLine;
		
		public int Line { get { return formatedLine.index; } }
		public int TokenIndex { get { return formatedLine.tokens.IndexOf(this); } }
		
		public static SyntaxToken CreateMissing()
		{
			return new SyntaxToken(Kind.Missing, string.Empty) { parent = null };
		}
		
		public SyntaxToken(Kind kind, string text)
		{
			parent = null;
			tokenKind = kind;
			this.text = string.Intern(text);
			tokenId = -1;
			style = null;
		}
		
		public bool IsMissing()
		{
			return tokenKind == Kind.Missing;
		}
		
		public override string ToString() { return tokenKind +"(\"" + text + "\")"; }
		
		public string Dump() { return "[Token: " + tokenKind + " \"" + text + "\"]"; }
		
		//	public int CompareTo(SyntaxToken other)
		//	{
		//		var t = tokenKind.GetHashCode().CompareTo(tokenKind.GetHashCode());
		//		return t != 0 ? t : text.CompareTo(other.text);
		//	}
	}
}
