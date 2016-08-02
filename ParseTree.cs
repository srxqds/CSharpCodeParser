using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpCodeParser
{
	public class ParseTree
	{
		public static uint resolverVersion = 2;

		public abstract class BaseNode 
		{
			public Node parent;
			public int childIndex;
			public CsGrammar.Node grammarNode;

			private uint _resolvedVersion = 1;

			public int Depth
			{
				get
				{
					var d = 0;
					for (var p = parent; p != null; p = p.parent)
						++d;
					return d;
				}
			}

			public BaseNode CommonParent(BaseNode other)
			{
				if (this == other)
					return this;
				var d = Depth;
				var d1 = other.Depth;
				var n = this;
				while (d < d1)
					other = other.parent;
				while (d > d1)
					n = n.parent;
				while (n != other)
				{
					n = n.parent;
					other = other.parent;
				}
				return n;
			}

			public Leaf FindPreviousLeaf()
			{
				var result = this;
				while (result.childIndex == 0 && result.parent != null)
					result = result.parent;
				if (result.parent == null)
					return null;
				result = result.parent.ChildAt(result.childIndex - 1);
				Node node;
				while ((node = result as Node) != null)
				{
					if (node.numValidNodes == 0)
						return node.FindPreviousLeaf();
					result = node.ChildAt(node.numValidNodes - 1);
				}
				return result as Leaf;
			}

			public Leaf FindNextLeaf()
			{
				var result = this;
				while (result.parent != null && result.childIndex == result.parent.numValidNodes - 1)
					result = result.parent;
				if (result.parent == null)
					return null;
				result = result.parent.ChildAt(result.childIndex + 1);
				Node node;
				while ((node = result as Node) != null)
				{
					if (node.numValidNodes == 0)
						return node.FindNextLeaf();
					result = node.ChildAt(0);
				}
				return result as Leaf;
			}

			public BaseNode FindPreviousNode()
			{
				var result = this;
				while (result.childIndex == 0 && result.parent != null)
					result = result.parent;
				if (result.parent == null)
					return null;
				result = result.parent.ChildAt(result.childIndex - 1);
				return result;
			}

			public abstract void Dump(StringBuilder sb, int indent);

			public bool IsAncestorOf(BaseNode node)
			{
				while (node != null)
					if (node.parent == this)
						return true;
					else
						node = node.parent;
				return false;
			}

			public Node FindParentByName(string ruleName)
			{
				var result = parent;
				while (result != null && result.RuleName != ruleName)
					result = result.parent;
				return result;
			}

			public override string ToString()
			{
				var sb = new StringBuilder();
				Dump(sb, 1);
				return sb.ToString();
			}


		}

		public class Leaf : BaseNode
		{
			public int line {
				get {
					return token != null ? token.Line : 0;
				}
			}
			public int tokenIndex {
				get {
					return token != null ? token.formatedLine.tokens.IndexOf(token) : 0;
				}
			}
			public SyntaxToken token;

			public Leaf() {}

			public Leaf(Scanner scanner)
			{
				token = scanner.Current;
				token.parent = this;
			}

			public override void Dump(StringBuilder sb, int indent)
			{
				sb.Append(' ', 2 * indent);
				sb.Append(childIndex);
				sb.Append(" ");
				sb.Append(token);
				//sb.Append(' ');
				//sb.Append((line + 1));
				//sb.Append(':');
				//sb.Append(tokenIndex);
				sb.AppendLine();
			}
		}

		public class Node : BaseNode
		{
			protected List<BaseNode> nodes = new List<BaseNode>();
			public int numValidNodes { get { return nodes.Count; } }
			public IEnumerable<BaseNode> Nodes { get { return nodes; } }

			public SemanticFlags semantics
			{
				get
				{
					var peer = ((CsGrammar.Id) grammarNode).peer;
					if (peer == null)
						Debug.Log("no peer for " + grammarNode);
					return peer != null ? ((CsGrammar.Rule) peer).semantics : SemanticFlags.None;
				}
			}

			public Node(CsGrammar.Id rule)
			{
				grammarNode = rule;
			}

			public BaseNode ChildAt(int index)
			{
				if (index < 0)
					index += numValidNodes;
				return index >= 0 && index < numValidNodes ? nodes[index] : null;
			}

			public Leaf LeafAt(int index)
			{
				if (index < 0)
					index += numValidNodes;
				return index >= 0 && index < numValidNodes ? nodes[index] as Leaf : null;
			}

			public Node NodeAt(int index)
			{
				if (index < 0)
					index += numValidNodes;
				return index >= 0 && index < numValidNodes ? nodes[index] as Node : null;
			}

			public string RuleName
			{
				get { return ((CsGrammar.Id) grammarNode).GetName(); }
			}


			public Leaf AddToken(Scanner scanner)
			{
				var leaf = new Leaf(scanner) { parent = this, childIndex = numValidNodes };
				if (numValidNodes == nodes.Count)
				{
					nodes.Add(leaf);
				}

				return leaf;
			}

			public Node AddNode(CsGrammar.Id rule, Scanner scanner, out bool skipParsing)
			{
				skipParsing = false;

				var node = new Node(rule) { parent = this, childIndex = numValidNodes };
				nodes.Add(node);
				return node;
			}

			public BaseNode FindChildByName(params string[] name)
			{
				BaseNode result = this;
				foreach (var n in name)
				{
					var node = result as Node;
					if (node == null)
						return null;

					var children = node.nodes;
					result = null;
					for (var i = 0; i < node.numValidNodes; i++)
					{
						var child = children[i];
						if (child.grammarNode != null && child.grammarNode.ToString() == n)
						{
							result = child;
							break;
						}
					}
					if (result == null)
						return null;
				}
				return result;
			}

			public override void Dump(StringBuilder sb, int indent)
			{
				sb.Append(' ', 2 * indent);
				sb.Append(childIndex);
				sb.Append(' ');
				var id = grammarNode as CsGrammar.Id;
				if (id != null && id.Rule != null)
				{
					sb.AppendLine(id.Rule.GetNt());
				}

				++indent;
				for (var i = 0; i < numValidNodes; ++i)
					nodes[i].Dump(sb, indent);
			}
		
			public Leaf GetFirstLeaf(bool validNodesOnly = true)
			{
				var count = validNodesOnly ? numValidNodes : nodes.Count;
				for (int i = 0; i < count; i++)
				{
					var child = nodes[i];
					var leaf = child as Leaf;
					if (leaf != null)
						return leaf;
					leaf = ((Node) child).GetFirstLeaf(validNodesOnly);
					if (leaf != null)
						return leaf;
				}
				return null;
			}

			public Leaf GetLastLeaf()
			{
				for (int i = numValidNodes; i-- > 0; )
				{
					var child = nodes[i];
					var leaf = child as Leaf;
					if (leaf != null)
					{
						if (leaf.token == null)
						{
							continue;
						}
						return leaf;
					}
					leaf = ((Node)child).GetLastLeaf();
					if (leaf != null)
						return leaf;
				}
				return null;
			}

		}

		public Node root;

		public override string ToString()
		{
			var sb = new StringBuilder();
			root.Dump(sb, 0);
			return sb.ToString();
		}
	}
}