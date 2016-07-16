using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace CSharpCodeParser
{
	public class CsParser
	{
		public static string[] BuiltInLiterals { get { return scriptLiterals; } }


		public static bool IsWhitespace(char word)
		{
			return Array.BinarySearch(whitespaces, word, StringComparer.Ordinal) >= 0;
		}

		public static bool IsBuiltInType(string word)
		{
			return Array.BinarySearch(builtInTypes, word, StringComparer.Ordinal) >= 0;
		}

		public static bool IsKeyword(string word)
		{
			return Array.BinarySearch(keywords, word, StringComparer.Ordinal) >= 0;
		}
		
		public static bool IsOperator(string text)
		{
			return operators.Contains(text);
		}
		
		public HashSet<string> scriptDefines = new HashSet<string>();
		
		public bool scriptDefinesChanged;

		protected static readonly string[] scriptLiterals = new string[] { "false", "null", "true", };
	
		private static readonly char[] whitespaces = { ' ', '\t' };
		
		private static readonly string[] keywords = new string[] {
			"abstract", "as", "base", "break", "case", "catch", "checked", "class", "const", "continue",
			"default", "delegate", "do", "else", "enum", "event", "explicit", "extern", "finally",
			"fixed", "for", "foreach", "goto", "if", "implicit", "in", "interface", "internal", "is",
			"lock", "namespace", "new", "operator", "out", "override", "params", "private",
			"protected", "public", "readonly", "ref", "return", "sealed", "sizeof", "stackalloc", "static",
			"struct", "switch", "this", "throw", "try", "typeof", "unchecked", "unsafe", "using", "virtual",
			"volatile", "while"
		};
		
		//private static readonly string[] csPunctsAndOps = {
		//	"{", "}", ";", "#", ".", "(", ")", "[", "]", "++", "--", "->", "+", "-",
		//	"!", "~", "++", "--", "&", "*", "/", "%", "+", "-", "<<", ">>", "<", ">",
		//	"<=", ">=", "==", "!=", "&", "^", "|", "&&", "||", "??", "?", "::", ":",
		//	"=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", "=>"
		//};
		
		private static readonly HashSet<string> operators = new HashSet<string>{
			"++", "--", "->", "+", "-", "!", "~", "++", "--", "&", "*", "/", "%", "+", "-", "<<", ">>", "<", ">",
			"<=", ">=", "==", "!=", "&", "^", "|", "&&", "||", "??", "?", "::", ":",
			"=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", "=>"
		};
		
		private static readonly string[] preprocessorKeywords = new string[] {
			"define", "elif", "else", "endif", "endregion", "error", "if", "line", "pragma", "region", "undef", "warning"
		};
		
		private static readonly string[] builtInTypes = new string[] {
			"bool", "byte", "char", "decimal", "double", "float", "int", "long", "object", "sbyte", "short",
			"string", "uint", "ulong", "ushort", "void"
		};
		# region gdsfs //sdfsdf
		#endregion
#if Debug //comment
#else //comment
#endif
		#region "Test"
		#endregion "Test"

		public void Tokenize(string line,CsTextBuffer.FormatedLine formatedLine)
		{
			var tokens = new List<SyntaxToken>();
			formatedLine.tokens = tokens;
			
			int startAt = 0;
			int length = line.Length;
			SyntaxToken token;
			
			SyntaxToken ws = ScanWhitespace(line, ref startAt);
			if (ws != null)
			{
				tokens.Add(ws);
				ws.formatedLine = formatedLine;
			}
			
			if (formatedLine.blockState == CsTextBuffer.BlockState.None && startAt < length && line[startAt] == '#')
			{
				tokens.Add(new SyntaxToken(SyntaxToken.Kind.Preprocessor, "#") { formatedLine = formatedLine });
				++startAt;
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				var error = false;
				var commentsOnly = false;
				var preprocessorCommentsAllowed = true;
				
				token = ScanWord(line, ref startAt);
				if (Array.BinarySearch(preprocessorKeywords, token.text) < 0)
				{
					error = true;
				}
				else
				{
					token.tokenKind = SyntaxToken.Kind.Preprocessor;
					tokens.Add(token);
					token.formatedLine = formatedLine;
					
					ws = ScanWhitespace(line, ref startAt);
					if (ws != null)
					{
						tokens.Add(ws);
						ws.formatedLine = formatedLine;
					}
					
					if (token.text == "if")
					{
						ParsePPOrExpression(line, formatedLine, ref startAt);
						commentsOnly = true;

					}
					else if (token.text == "elif")
					{
						ParsePPOrExpression(line, formatedLine, ref startAt);
						commentsOnly = true;
					}
					else if (token.text == "else")
					{
						commentsOnly = true;
					}
					else if (token.text == "endif")
					{
						commentsOnly = true;
					}
					else if (token.text == "region")  //region Tag
					{
						preprocessorCommentsAllowed = false;
					}
					else if (token.text == "endregion")
					{
						preprocessorCommentsAllowed = false;
					}
					else if (token.text == "define" || token.text == "undef")
					{
						var symbol = ScanIdentifierOrKeyword(line, ref startAt);
						if (symbol != null && symbol.text != "true" && symbol.text != "false")
						{
							symbol.tokenKind = SyntaxToken.Kind.PreprocessorSymbol;
							formatedLine.tokens.Add(symbol);
							symbol.formatedLine = formatedLine;
							
							scriptDefinesChanged = true;
						}
					}
					//what syntax?
					else if (token.text == "error" || token.text == "warning")
					{
						preprocessorCommentsAllowed = false;
					}
				}
				
				if (!preprocessorCommentsAllowed)
				{
					ws = ScanWhitespace(line, ref startAt);
					if (ws != null)
					{
						tokens.Add(ws);
						ws.formatedLine = formatedLine;
					}
					if (startAt < length)
					{
						var textArgument = line.Substring(startAt);
						textArgument.TrimEnd(new [] {' ', '\t'});
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, textArgument) { formatedLine = formatedLine });
						startAt = length - textArgument.Length;
						//bug?
						if (startAt < length)
							tokens.Add(new SyntaxToken(SyntaxToken.Kind.Whitespace, line.Substring(startAt)) { formatedLine = formatedLine });
					}
					return;
				}
				
				while (startAt < length)
				{
					ws = ScanWhitespace(line, ref startAt);
					if (ws != null)
					{
						tokens.Add(ws);
						ws.formatedLine = formatedLine;
						continue;
					}
					
					var firstChar = line[startAt];
					if (startAt < length - 1 && firstChar == '/' && line[startAt + 1] == '/')
					{
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.Comment, line.Substring(startAt)) { formatedLine = formatedLine });
						break;
					}
					else if (commentsOnly)
					{
						error = true;
						//tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorCommentExpected, line.Substring(startAt)) { formatedLine = formatedLine });
						break;						
					}
					//? why
					if (char.IsLetterOrDigit(firstChar) || firstChar == '_')
					{
						token = ScanWord(line, ref startAt);
						token.tokenKind = SyntaxToken.Kind.PreprocessorArguments;
						tokens.Add(token);
						token.formatedLine = formatedLine;
					}
					else if (firstChar == '"')
					{
						token = ScanStringLiteral(line, ref startAt);
						token.tokenKind = SyntaxToken.Kind.PreprocessorArguments;
						tokens.Add(token);
						token.formatedLine = formatedLine;
					}
					else if (firstChar == '\'')
					{
						token = ScanCharLiteral(line, ref startAt);
						token.tokenKind = SyntaxToken.Kind.PreprocessorArguments;
						tokens.Add(token);
						token.formatedLine = formatedLine;
					}
					else
					{
						token = new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, firstChar.ToString()) { formatedLine = formatedLine };
						tokens.Add(token);
						++startAt;
					}
					
					if (error)
					{
						//token.tokenKind = SyntaxToken.Kind.PreprocessorDirectiveExpected;
					}
				}
				
				return;
			}
			
			//var inactiveLine = false;//formatedLine.regionTree.kind > CsTextBuffer.RegionTree.Kind.LastActive;
			
			while (startAt < length)
			{
				switch (formatedLine.blockState)
				{
				case CsTextBuffer.BlockState.None:
					ws = ScanWhitespace(line, ref startAt);
					if (ws != null)
					{
						tokens.Add(ws);
						ws.formatedLine = formatedLine;
						continue;
					}
					//?
					/*if (inactiveLine)
					{
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.Comment, line.Substring(startAt)) { formatedLine = formatedLine });
						startAt = length;
						break;
					}*/
					
					if (line[startAt] == '/' && startAt < length - 1)
					{
						if (line[startAt + 1] == '/')
						{
							tokens.Add(new SyntaxToken(SyntaxToken.Kind.Comment, "//") { formatedLine = formatedLine });
							startAt += 2;
							tokens.Add(new SyntaxToken(SyntaxToken.Kind.Comment, line.Substring(startAt)) { formatedLine = formatedLine });
							startAt = length;
							break;
						}
						else if (line[startAt + 1] == '*')
						{
							tokens.Add(new SyntaxToken(SyntaxToken.Kind.Comment, "/*") { formatedLine = formatedLine });
							startAt += 2;
							formatedLine.blockState = CsTextBuffer.BlockState.CommentBlock;
							break;
						}
					}
					
					if (line[startAt] == '\'')
					{
						token = ScanCharLiteral(line, ref startAt);
						tokens.Add(token);
						token.formatedLine = formatedLine;
						break;
					}
					
					if (line[startAt] == '\"')
					{
						token = ScanStringLiteral(line, ref startAt);
						tokens.Add(token);
						token.formatedLine = formatedLine;
						break;
					}
					
					if (startAt < length - 1 && line[startAt] == '@' && line[startAt + 1] == '\"')
					{
						token = new SyntaxToken(SyntaxToken.Kind.VerbatimStringBegin, line.Substring(startAt, 2)) { formatedLine = formatedLine };
						tokens.Add(token);
						startAt += 2;
						formatedLine.blockState = CsTextBuffer.BlockState.StringBlock;
						break;
					}
					
					if (line[startAt] >= '0' && line[startAt] <= '9'
					    || startAt < length - 1 && line[startAt] == '.' && line[startAt + 1] >= '0' && line[startAt + 1] <= '9')
					{
						token = ScanNumericLiteral(line, ref startAt);
						tokens.Add(token);
						token.formatedLine = formatedLine;
						break;
					}
					
					token = ScanIdentifierOrKeyword(line, ref startAt);
					if (token != null)
					{
						tokens.Add(token);
						token.formatedLine = formatedLine;
						break;
					}
					
					// Multi-character operators / punctuators
					// "++", "--", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||", "??", "+=", "-=", "*=", "/=", "%=",
					// "&=", "|=", "^=", "<<=", ">>=", "=>", "::"
					var punctuatorStart = startAt++;
					if (startAt < line.Length)
					{
						switch (line[punctuatorStart])
						{
							case '?':
								if (line[startAt] == '?')
									++startAt;
								break;
							case '+':
								if (line[startAt] == '+' || line[startAt] == '=')
									++startAt;
								break;
							case '-':
								if (line[startAt] == '-' || line[startAt] == '=')
									++startAt;
								break;
							case '<':
								if (line[startAt] == '=')
									++startAt;
								else if (line[startAt] == '<')
								{
									++startAt;
									if (startAt < line.Length && line[startAt] == '=')
										++startAt;
								}
								break;
							case '>':
								if (line[startAt] == '=')
									++startAt;
								//else if (startAt < line.Length && line[startAt] == '>')
								//{
								//    ++startAt;
								//    if (line[startAt] == '=')
								//        ++startAt;
								//}
								break;
							case '=':
								if (line[startAt] == '=' || line[startAt] == '>')
									++startAt;
								break;
							case '&':
								if (line[startAt] == '=' || line[startAt] == '&')
									++startAt;
								break;
							case '|':
								if (line[startAt] == '=' || line[startAt] == '|')
									++startAt;
								break;
							case '*':
							case '/':
							case '%':
							case '^':
							case '!':
								if (line[startAt] == '=')
									++startAt;
								break;
							case ':':
								if (line[startAt] == ':')
									++startAt;
								break;
						}
					}
					tokens.Add(new SyntaxToken(SyntaxToken.Kind.Punctuator, line.Substring(punctuatorStart, startAt - punctuatorStart)) { formatedLine = formatedLine });
					break;
					
				case CsTextBuffer.BlockState.CommentBlock:
					int commentBlockEnd = line.IndexOf("*/", startAt, StringComparison.Ordinal);
					if (commentBlockEnd == -1)
					{
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.Comment, line.Substring(startAt)) { formatedLine = formatedLine });
						startAt = length;
					}
					else
					{
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.Comment, line.Substring(startAt, commentBlockEnd + 2 - startAt)) { formatedLine = formatedLine });
						startAt = commentBlockEnd + 2;
						formatedLine.blockState = CsTextBuffer.BlockState.None;
					}
					break;
					
				case CsTextBuffer.BlockState.StringBlock:
					int i = startAt;
					int closingQuote = line.IndexOf('\"', startAt);
					while (closingQuote != -1 && closingQuote < length - 1 && line[closingQuote + 1] == '\"')
					{
						i = closingQuote + 2;
						closingQuote = line.IndexOf('\"', i);
					}
					if (closingQuote == -1)
					{
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.VerbatimStringLiteral, line.Substring(startAt)) { formatedLine = formatedLine });
						startAt = length;
					}
					else
					{
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.VerbatimStringLiteral, line.Substring(startAt, closingQuote - startAt)) { formatedLine = formatedLine });
						startAt = closingQuote;
						tokens.Add(new SyntaxToken(SyntaxToken.Kind.VerbatimStringLiteral, line.Substring(startAt, 1)) { formatedLine = formatedLine });
						++startAt;
						formatedLine.blockState = CsTextBuffer.BlockState.None;
					}
					break;
				}
			}
		}


	
		protected static SyntaxToken ScanWhitespace(string line, ref int startAt)
		{
			int i = startAt;
			while (i < line.Length && IsWhitespace(line[i]))
				++i;
			if (i == startAt)
				return null;
			
			var token = new SyntaxToken(SyntaxToken.Kind.Whitespace, line.Substring(startAt, i - startAt));
			startAt = i;
			return token;
		}

		protected static SyntaxToken ScanWord(string line, ref int startAt)
		{
			int i = startAt;
			while (i < line.Length)
			{
				if (!Char.IsLetterOrDigit(line, i) && line[i] != '_')
					break;
				++i;
			}
			var token = new SyntaxToken(SyntaxToken.Kind.Identifier, line.Substring(startAt, i - startAt));
			startAt = i;
			return token;
		}
		
		protected static bool ScanUnicodeEscapeChar(string line, ref int startAt)
		{
			if (startAt >= line.Length - 5)
				return false;
			if (line[startAt] != '\\')
				return false;
			int i = startAt + 1;
			if (line[i] != 'u' && line[i] != 'U')
				return false;
			var n = line[i] == 'u' ? 4 : 8;
			++i;
			while (n > 0)
			{
				if (!ScanHexDigit(line, ref i))
					break;
				--n;
			}
			if (n == 0)
			{
				startAt = i;
				return true;
			}
			return false;
		}
		
		protected static SyntaxToken ScanCharLiteral(string line, ref int startAt)
		{
			var i = startAt + 1;
			while (i < line.Length)
			{
				if (line[i] == '\'')
				{
					++i;
					break;
				}
				if (line[i] == '\\' && i < line.Length - 1)
					++i;
				++i;
			}
			var token = new SyntaxToken(SyntaxToken.Kind.CharLiteral, line.Substring(startAt, i - startAt));
			startAt = i;
			return token;
		}
		
		protected static SyntaxToken ScanStringLiteral(string line, ref int startAt)
		{
			var i = startAt + 1;
			while (i < line.Length)
			{
				if (line[i] == '\"')
				{
					++i;
					break;
				}
				if (line[i] == '\\' && i < line.Length - 1)
					++i;
				++i;
			}
			var token = new SyntaxToken(SyntaxToken.Kind.StringLiteral, line.Substring(startAt, i - startAt));
			startAt = i;
			return token;
		}
		
		protected static SyntaxToken ScanNumericLiteral(string line, ref int startAt)
		{
			bool hex = false;
			bool point = false;
			bool exponent = false;
			var i = startAt;
			
			SyntaxToken token;
			
			char c;
			if (line[i] == '0' && i < line.Length - 1 && (line[i + 1] == 'x' || line[i + 1] == 'X'))
			{
				i += 2;
				hex = true;
				while (i < line.Length)
				{
					c = line[i];
					if (c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')
						++i;
					else
						break;
				}
			}
			else
			{
				while (i < line.Length && line[i] >= '0' && line[i] <= '9')
					++i;
			}
			
			if (i > startAt && i < line.Length)
			{
				c = line[i];
				if (c == 'l' || c == 'L' || c == 'u' || c == 'U')
				{
					++i;
					if (i < line.Length)
					{
						if (c == 'l' || c == 'L')
						{
							if (line[i] == 'u' || line[i] == 'U')
								++i;
						}
						else if (line[i] == 'l' || line[i] == 'L')
							++i;
					}
					token = new SyntaxToken(SyntaxToken.Kind.IntegerLiteral, line.Substring(startAt, i - startAt));
					startAt = i;
					return token;
				}
			}
			
			if (hex)
			{
				token = new SyntaxToken(SyntaxToken.Kind.IntegerLiteral, line.Substring(startAt, i - startAt));
				startAt = i;
				return token;
			}
			
			while (i < line.Length)
			{
				c = line[i];
				
				if (!point && !exponent && c == '.')
				{
					if (i < line.Length - 1 && line[i+1] >= '0' && line[i+1] <= '9')
					{
						point = true;
						++i;
						continue;
					}
					else
					{
						break;
					}
				}
				if (!exponent && i > startAt && (c == 'e' || c == 'E'))
				{
					exponent = true;
					++i;
					if (i < line.Length && (line[i] == '-' || line[i] == '+'))
						++i;
					continue;
				}
				if (c == 'f' || c == 'F' || c == 'd' || c == 'D' || c == 'm' || c == 'M')
				{
					point = true;
					++i;
					break;
				}
				if (c < '0' || c > '9')
					break;
				++i;
			}
			token = new SyntaxToken(
				point || exponent ? SyntaxToken.Kind.RealLiteral : SyntaxToken.Kind.IntegerLiteral,
				line.Substring(startAt, i - startAt));
			startAt = i;
			return token;
		}
		
		protected static bool ScanHexDigit(string line, ref int i)
		{
			if (i >= line.Length)
				return false;
			char c = line[i];
			if (c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f')
			{
				++i;
				return true;
			}
			return false;
		}
		
		protected static SyntaxToken ScanIdentifierOrKeyword(string line, ref int startAt)
		{
			bool identifier = false;
			int i = startAt;
			if (i >= line.Length)
				return null;
			
			char c = line[i];
			if (c == '@')
			{
				identifier = true;
				++i;
			}
			if (i < line.Length)
			{
				c = line[i];
				if (char.IsLetter(c) || c == '_')
				{
					++i;
				}
				else if (!ScanUnicodeEscapeChar(line, ref i))
				{
					if (i == startAt)
						return null;
					var partialWord = line.Substring(startAt, i - startAt);
					startAt = i;
					return new SyntaxToken(SyntaxToken.Kind.Identifier, partialWord);
				}
				else
				{
					identifier = true;
				}
				
				while (i < line.Length)
				{
					if (char.IsLetterOrDigit(line, i) || line[i] == '_')
						++i;
					else if (!ScanUnicodeEscapeChar(line, ref i))
						break;
					else
						identifier = true;
				}
			}
			
			var word = line.Substring(startAt, i - startAt);
			startAt = i;

			if (!identifier && !IsKeyword(word) && !IsBuiltInType(word))
				identifier = true;
			return new SyntaxToken(identifier ? SyntaxToken.Kind.Identifier : SyntaxToken.Kind.Keyword, word);
		}
		
		protected bool ParsePPOrExpression(string line, CsTextBuffer.FormatedLine formatedLine, ref int startAt)
		{
			if (startAt >= line.Length)
			{
				//TODO: Insert missing token
				return true;
			}
			
			var lhs = ParsePPAndExpression(line, formatedLine, ref startAt);
			
			var ws = ScanWhitespace(line, ref startAt);
			if (ws != null)
			{
				formatedLine.tokens.Add(ws);
				ws.formatedLine = formatedLine;
			}
			
			if (startAt + 1 < line.Length && line[startAt] == '|' && line[startAt + 1] == '|')
			{
				formatedLine.tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, "||") { formatedLine = formatedLine });
				startAt += 2;
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				var rhs = ParsePPOrExpression(line, formatedLine, ref startAt);
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				return lhs || rhs;
			}
			
			return lhs;
		}

		//why call ParsePPEqualityExpression ?
		protected bool ParsePPAndExpression(string line, CsTextBuffer.FormatedLine formatedLine, ref int startAt)
		{
			if (startAt >= line.Length)
			{
				//TODO: Insert missing token
				return true;
			}
			
			var lhs = ParsePPEqualityExpression(line, formatedLine, ref startAt);
			
			var ws = ScanWhitespace(line, ref startAt);
			if (ws != null)
			{
				formatedLine.tokens.Add(ws);
				ws.formatedLine = formatedLine;
			}
			
			if (startAt + 1 < line.Length && line[startAt] == '&' && line[startAt + 1] == '&')
			{
				formatedLine.tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, "&&") { formatedLine = formatedLine });
				startAt += 2;
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				var rhs = ParsePPAndExpression(line, formatedLine, ref startAt);
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				return lhs && rhs;
			}
			
			return lhs;
		}

		// ==,!!,!==! they are equals.
		protected bool ParsePPEqualityExpression(string line, CsTextBuffer.FormatedLine formatedLine, ref int startAt)
		{
			if (startAt >= line.Length)
			{
				//TODO: Insert missing token
				return true;
			}
			
			var lhs = ParsePPUnaryExpression(line, formatedLine, ref startAt);
			
			var ws = ScanWhitespace(line, ref startAt);
			if (ws != null)
			{
				formatedLine.tokens.Add(ws);
				ws.formatedLine = formatedLine;
			}
			
			if (startAt + 1 < line.Length && (line[startAt] == '=' || line[startAt + 1] == '!') && line[startAt + 1] == '=')
			{
				var equality = line[startAt] == '=';
				formatedLine.tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, equality ? "==" : "!=") { formatedLine = formatedLine });
				startAt += 2;
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				var rhs = ParsePPEqualityExpression(line, formatedLine, ref startAt);
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				return equality ? lhs == rhs : lhs != rhs;
			}
			
			return lhs;
		}
		
		protected bool ParsePPUnaryExpression(string line, CsTextBuffer.FormatedLine formatedLine, ref int startAt)
		{
			if (startAt >= line.Length)
			{
				//TODO: Insert missing token
				return true;
			}
			
			if (line[startAt] == '!')
			{
				formatedLine.tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, "!") { formatedLine = formatedLine });
				++startAt;
				
				var ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				var result = ParsePPUnaryExpression(line, formatedLine, ref startAt);
				
				ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				return !result;
			}
			
			return ParsePPPrimaryExpression(line, formatedLine, ref startAt);
		}
		
		protected bool ParsePPPrimaryExpression(string line, CsTextBuffer.FormatedLine formatedLine, ref int startAt)
		{
			if (line[startAt] == '(')
			{
				formatedLine.tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, "(") { formatedLine = formatedLine });
				++startAt;
				
				var ws = ScanWhitespace(line, ref startAt);
				if (ws != null)
				{
					formatedLine.tokens.Add(ws);
					ws.formatedLine = formatedLine;
				}
				
				var result = ParsePPOrExpression(line, formatedLine, ref startAt);
				
				if (startAt >= line.Length)
				{
					//TODO: Insert missing token
					return result;
				}
				
				if (line[startAt] == ')')
				{
					formatedLine.tokens.Add(new SyntaxToken(SyntaxToken.Kind.PreprocessorArguments, ")") { formatedLine = formatedLine });
					++startAt;
					
					ws = ScanWhitespace(line, ref startAt);
					if (ws != null)
					{
						formatedLine.tokens.Add(ws);
						ws.formatedLine = formatedLine;
					}
					
					return result;
				}
				
				//TODO: Insert missing token
				return result;
			}
			
			var symbolResult = ParsePPSymbol(line, formatedLine, ref startAt);
			
			var ws2 = ScanWhitespace(line, ref startAt);
			if (ws2 != null)
			{
				formatedLine.tokens.Add(ws2);
				ws2.formatedLine = formatedLine;
			}
			
			return symbolResult;
		}
		
		protected bool ParsePPSymbol(string line, CsTextBuffer.FormatedLine formatedLine, ref int startAt)
		{
			var word = ScanIdentifierOrKeyword(line, ref startAt);
			if (word == null)
				return true;
			
			word.tokenKind = SyntaxToken.Kind.PreprocessorSymbol;
			formatedLine.tokens.Add(word);
			word.formatedLine = formatedLine;
			
			if (word.text == "true")
			{
				return true;
			}
			if (word.text == "false")
			{
				return false;
			}
			
			if (scriptDefines == null)
				scriptDefines = new HashSet<string>(UnityEditor.EditorUserBuildSettings.activeScriptCompilationDefines);
			
			var isDefined = scriptDefines.Contains(word.text);
			return isDefined;
		}
		

	}


}
