using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

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
		
		/*public class RegionTree
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
		}*/


		public class FormatedLine
		{
			//[System.NonSerialized]
			public BlockState blockState;
			//[System.NonSerialized]
			//public RegionTree regionTree;
			//[SerializeField, HideInInspector]
			//public int lastChange = -1;
			//[SerializeField, HideInInspector]
			//public int savedVersion = -1;
			//[System.NonSerialized]
			public List<SyntaxToken> tokens;
			//[System.NonSerialized]
			//public int laLines;
			[System.NonSerialized]
			public int index;
		}

		//public RegionTree rootRegion = new RegionTree();
		

		public FormatedLine[] formatedLines = new FormatedLine[0];

		public List<string> lines = new List<string>();

		public CsParser parser = new CsParser();

		private string lineEnding = "\n";


		public void LoadCsCode(string content)
		{
			lines = new List<string>(content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
			formatedLines = new FormatedLine[lines.Count];
			for (int i=0; i<lines.Count; i++) 
			{
				FormatLine(i);
			}
		}

		private void FormatLine(int lineIndex)
		{
			var formateLine = this.formatedLines [lineIndex];
			if (formateLine == null) 
			{
				formateLine = new FormatedLine();
				formateLine.index = lineIndex;
			}
			if(lineIndex > 0)
			{
				formateLine.blockState = this.formatedLines[lineIndex -1].blockState;
			}
			parser.Tokenize (this.lines[lineIndex], formateLine);

		}
	}

}
