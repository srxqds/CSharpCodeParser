using UnityEditor;
using CSharpCodeParser;
using System.IO;
using UnityEngine;

public class Test 
{
	[MenuItem("Test/CsParser")]
	public static void DoTest()
	{
		CsTextBuffer tb = new CsTextBuffer ();
		string cs = File.ReadAllText (Application.dataPath + "/Plugins/Editor/Test1.cs");
		tb.LoadCsCode (cs);
		Debug.LogError (tb);
		CsGrammar csGrammar = new CsGrammar ();
		Scanner scanner = new Scanner (csGrammar, tb.formatedLines, "test");
		ParseTree parseTree = csGrammar.parser.ParseAll (scanner);
		Debug.LogError (parseTree);
	}

	[MenuItem("Test/DoTest1")]
	public static void DoTest1()
	{
		Debug.Log(CsParser.IsWhitespace ('\t'));

		CsGrammar csGrammar = new CsGrammar ();
		Debug.LogError (csGrammar.parser);
	}
}
