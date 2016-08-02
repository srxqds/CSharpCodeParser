using UnityEngine;
using System;

public class Test1<T>
{

	public T gen;
	public int field;
	public int GetValue()
	{
		return 0;
	}

	[UnityEditor.MenuItem("Test")]
	public static void AttributeTest()
	{
	}
}
