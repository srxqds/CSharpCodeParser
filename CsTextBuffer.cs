using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace CSharpCodeParser
{
	public struct TextPosition
	{
		public int line;
		public int index;
		
		public TextPosition(int line, int index)
		{
			this.line = line;
			this.index = index;
		}
		
		public static TextPosition operator + (TextPosition other, int offset)
		{
			return new TextPosition { line = other.line, index = other.index + offset };
		}
		
		public static bool operator == (TextPosition lhs, TextPosition rhs)
		{
			return lhs.line == rhs.line && lhs.index == rhs.index;
		}
		
		public static bool operator != (TextPosition lhs, TextPosition rhs)
		{
			return lhs.line != rhs.line || lhs.index != rhs.index;
		}
		
		public static bool operator < (TextPosition lhs, TextPosition rhs)
		{
			return lhs.line < rhs.line || lhs.line == rhs.line && lhs.index < rhs.index;
		}
		
		public static bool operator <= (TextPosition lhs, TextPosition rhs)
		{
			return lhs.line < rhs.line || lhs.line == rhs.line && lhs.index <= rhs.index;
		}
		
		public static bool operator > (TextPosition lhs, TextPosition rhs)
		{
			return lhs.line > rhs.line || lhs.line == rhs.line && lhs.index > rhs.index;
		}
		
		public static bool operator >= (TextPosition lhs, TextPosition rhs)
		{
			return lhs.line > rhs.line || lhs.line == rhs.line && lhs.index >= rhs.index;
		}
		
		public override bool Equals(object obj)
		{
			if (!(obj is TextPosition))
				return false;
			
			var rhs = (TextPosition) obj;
			return line == rhs.line && index == rhs.index;
		}
		
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				var hash = (int)2166136261;
				hash = hash * 16777619 ^ line.GetHashCode();
				hash = hash * 16777619 ^ index.GetHashCode();
				return hash;
			}
		}
		
		/*public bool Move(FGTextBuffer textBuffer, int offset)
		{
			while (offset > 0)
			{
				var lineLength = textBuffer.lines[line].Length;
				if (index + offset <= lineLength)
				{
					index += offset;
					if (index == lineLength)
					{
						index = 0;
						++line;
					}
					return true;
				}
				
				offset -= lineLength - index;
				++line;
				index = 0;
				
				if (line >= textBuffer.lines.Count)
				{
					line = textBuffer.lines.Count;
					index = 0;
					return false;
				}
			}
			
			while (offset < 0)
			{
				if (index + offset >= 0)
				{
					index += offset;
					return true;
				}
				
				offset += index;
				--line;
				if (line < 0)
				{
					line = 0;
					index = 0;
					return false;
				}
				index = textBuffer.lines[line].Length;
			}
			
			return true;
		}*/
		
		public override string ToString()
		{
			return "TextPosition (line: " + line + ", index: " + index + ")";
		}
	}
	
	public struct TextOffset
	{
		public int lines;
		public int indexOffset;
	}
	
	public struct TextSpan
	{
		public int line;
		public int index;
		public int lineOffset;
		public int indexOffset;
		
		public override string ToString()
		{
			return "TextSpan{ line = " + (line+1) + ", fromChar = " + index + ", lineOffset = " + lineOffset + ", toChar = " + indexOffset + " }";
		}
		
		public static TextSpan CreateEmpty(TextPosition position)
		{
			return new TextSpan { line = position.line, index = position.index };
		}
		
		public static TextSpan Create(TextPosition from, TextPosition to)
		{
			return new TextSpan
			{
				line = from.line,
				index = from.index,
				lineOffset = to.line - from.line,
				indexOffset = to.index - (to.line == from.line ? from.index : 0)
			};
		}
		
		public static TextSpan CreateBetween(TextSpan from, TextSpan to)
		{
			return Create(from.EndPosition, to.StartPosition);
		}
		
		public static TextSpan CreateEnclosing(TextSpan from, TextSpan to)
		{
			return Create(from.StartPosition, to.EndPosition);
		}
		
		public static TextSpan Create(TextPosition start, TextOffset length)
		{
			return new TextSpan
			{
				line = start.line,
				index = start.index,
				lineOffset = length.lines,
				indexOffset = length.indexOffset
			};
		}
		
		public TextPosition StartPosition
		{
			get { return new TextPosition { line = line, index = index }; }
			set
			{
				if (value.line == line + lineOffset)
				{
					line = value.line;
					lineOffset = 0;
					indexOffset = index + indexOffset - value.index;
					index = value.index;
				}
				else
				{
					lineOffset = line + lineOffset - value.line;
					line = value.line;
					index = value.index;
				}
			}
		}
		
		public TextPosition EndPosition
		{
			get { return new TextPosition { line = line + lineOffset, index = indexOffset + (lineOffset == 0 ? index : 0) }; }
			set
			{
				if (value.line == line)
				{
					lineOffset = 0;
					indexOffset = value.index - index;
				}
				else
				{
					lineOffset = value.line - line;
					indexOffset = value.index;
				}
			}
		}
		
		public void Offset(int deltaLines, int deltaIndex)
		{
			line += deltaLines;
			index += deltaIndex;
		}
		
		public bool Contains(TextPosition position)
		{
			return !(position.line < line
			         || position.line == line && (position.index < index || lineOffset == 0 && position.index > index + indexOffset)
			         || position.line > line + lineOffset
			         || position.line == line + lineOffset && position.index > indexOffset);
		}
	}

	public class CsTextBuffer
	{

		public enum BlockState : byte
		{
			None = 0,
			CommentBlock = 1,
			StringBlock = 2,
		}
		
		public class RegionTree
		{
			public enum Kind
			{
				None,
				Region,
				If,
				Elif,
				Else,
				
				LastActive,
				
				InactiveRegion,
				InactiveIf,
				InactiveElif,
				InactiveElse
			}
			public Kind kind;
			public FormatedLine line;
			public RegionTree parent;
			public List<RegionTree> children;
		}


		public class FormatedLine
		{
			[System.NonSerialized]
			public BlockState blockState;
			[System.NonSerialized]
			public RegionTree regionTree;
			[SerializeField, HideInInspector]
			public int lastChange = -1;
			[SerializeField, HideInInspector]
			public int savedVersion = -1;
			[System.NonSerialized]
			public List<SyntaxToken> tokens;
			[System.NonSerialized]
			public int laLines;
			[System.NonSerialized]
			public int index;
		}

		public RegionTree rootRegion = new RegionTree();
		

		public FormatedLine[] formatedLines = new FormatedLine[0];

		public List<string> lines = new List<string>();

		private string lineEnding = "\n";
	}

}
