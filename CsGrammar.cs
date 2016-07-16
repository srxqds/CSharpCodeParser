using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpCodeParser
{


	public interface IVisitableTreeNode<NonLeaf, Leaf>
	{
		bool Accept(IHierarchicalVisitor<NonLeaf, Leaf> visitor);
	}
	
	public interface IHierarchicalVisitor<NonLeaf, Leaf>
	{
		bool Visit(Leaf leafNode);
		bool VisitEnter(NonLeaf nonLeafNode);
		bool VisitLeave(NonLeaf nonLeafNode);
	}
	public class ParseTree
	{
		public static uint resolverVersion = 2;

		public abstract class BaseNode : IVisitableTreeNode<Node, Leaf>
		{
			public Node parent;
			public int childIndex;
			public CsGrammar.Node grammarNode;
			public bool missing;
			public string syntaxError;
			public string semanticError;
			
			private uint _resolvedVersion = 1;
			private SymbolDefinition _resolvedSymbol;
			public SymbolDefinition resolvedSymbol 
			{
				get {
					if (_resolvedSymbol != null && _resolvedVersion != 0 &&
					    (_resolvedVersion != resolverVersion || !_resolvedSymbol.IsValid())
					    )
						_resolvedSymbol = null;
					return _resolvedSymbol;
				}
				set {
					if (_resolvedVersion == 0)
					{
						//Debug.LogWarning("Whoops!");
						return;
					}
					_resolvedVersion = resolverVersion;
					_resolvedSymbol = value;
				}
			}
			
			public void SetDeclaredSymbol(SymbolDefinition symbol)
			{
				_resolvedSymbol = symbol;
				_resolvedVersion = 0;
			}
			
			public abstract bool Accept(IHierarchicalVisitor<Node, Leaf> visitor);
			
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
			
			//	public abstract void RemoveToken();
			
			public abstract string Print();
			
			public abstract bool HasLeafs(bool validNodesOnly = true);
			
			public abstract bool HasErrors(bool validNodesOnly = true);
			
			public abstract bool IsLit(string litText);
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
			
			public Leaf(CsGrammar.IScanner scanner)
			{
				//line = scanner.CurrentLine() - 1;
				//tokenIndex = scanner.CurrentTokenIndex();
				token = scanner.Current;
				token.parent = this;
			}
			
			public bool TryReuse(CsGrammar.IScanner scanner)
			{
				if (token == null)
					return false;
				var current = scanner.Current;
				if (current.parent == this)//token.text == current.text && token.tokenKind == current.tokenKind)
				{
					//	line = scanner.CurrentLine() - 1;
					//	tokenIndex = scanner.CurrentTokenIndex();
					token.parent = this;
					return true;
				}
				return false;
			}
			
			public override bool Accept(IHierarchicalVisitor<Node, Leaf> visitor)
			{
				return visitor.Visit(this);
			}
			
			public override void Dump(StringBuilder sb, int indent)
			{
				sb.Append(' ', 2 * indent);
				sb.Append(childIndex);
				sb.Append(" ");
				if (syntaxError != null)
					sb.Append("? ");
				sb.Append(token);
				sb.Append(' ');
				sb.Append((line + 1));
				sb.Append(':');
				sb.Append(tokenIndex);
				if (syntaxError != null)
					sb.Append(' ').Append(syntaxError);
				sb.AppendLine();
			}
			
			public void RemoveToken()
			{
				//	token.parent = null;
				//	token = null;
				//	if (parent != null)
				//		parent.RemoveNodeAt(childIndex);
			}
			
			public void ReparseToken()
			{
				if (token != null)
				{
					token.parent = null;
					token = null;
				}
				if (parent != null)
					parent.RemoveNodeAt(childIndex/*, false*/);
			}
			
			public override string Print()
			{
				var lit = grammarNode as CsGrammar.Lit;
				if (lit != null)
					return lit.pretty;
				return token != null ? token.text : "";
			}
			
			public override bool IsLit(string litText)
			{
				var lit = grammarNode as CsGrammar.Lit;
				return lit != null && lit.body == litText;
			}
			
			public override bool HasLeafs(bool validNodesOnly = true)
			{
				return true;
			}
			
			public override bool HasErrors(bool validNodesOnly = true)
			{
				return syntaxError != null;
			}
		}
		
		public class Node : BaseNode
		{
			protected List<BaseNode> nodes = new List<BaseNode>();
			public int numValidNodes { get; protected set; }
			public IEnumerable<BaseNode> Nodes { get { return nodes; } }
			
			public Scope scope;
			public SymbolDeclaration declaration;
			
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
			
			public override bool Accept(IHierarchicalVisitor<Node, Leaf> visitor)
			{
				if (visitor.VisitEnter(this))
				{
					foreach (var child in nodes)
						if (!child.Accept(visitor))
							break;
				}
				return visitor.VisitLeave(this);
			}
			
			//static int createdTokensCounter;
			//static int reusedTokensCounter;
			
			public Leaf AddToken(CsGrammar.IScanner scanner)
			{
				if (numValidNodes < nodes.Count)
				{
					var reused = nodes[numValidNodes] as Leaf;
					if (reused != null && reused.TryReuse(scanner))
					{
						//	reused.missing = false;
						//	reused.errors = null;
						
						//reused.parent = this;
						//reused.childIndex = numValidNodes;
						++numValidNodes;
						
						//if (++reusedTokensCounter + createdTokensCounter == 1)
						//{
						//	UnityEditor.EditorApplication.delayCall += () => {
						//		Debug.Log("Tokens - Created: " + createdTokensCounter + " Reused: " + reusedTokensCounter);
						//		createdTokensCounter = 0;
						//		reusedTokensCounter = 0;
						//	};
						//}
						
						//	Debug.Log("reused " + reused.token.text + " at line " + scanner.CurrentLine());
						return reused;
					}
				}
				
				//if (reusedTokensCounter + ++createdTokensCounter == 1)
				//{
				//	UnityEditor.EditorApplication.delayCall += () => {
				//		Debug.Log("Tokens - Created: " + createdTokensCounter + " Reused: " + reusedTokensCounter);
				//		createdTokensCounter = 0;
				//		reusedTokensCounter = 0;
				//	};
				//}
				
				var leaf = new Leaf(scanner) { parent = this, childIndex = numValidNodes };
				if (numValidNodes == nodes.Count)
				{
					nodes.Add(leaf);
					++numValidNodes;
				}
				else
				{
					nodes.Insert(numValidNodes++, leaf);
					for (var i = numValidNodes; i < nodes.Count; ++i)
						++nodes[i].childIndex;
				}
				
				return leaf;
			}
			
			public Leaf AddToken(SyntaxToken token)
			{
				if (!token.IsMissing() && numValidNodes < nodes.Count)
				{
					var reused = nodes[numValidNodes] as Leaf;
					if (reused != null && reused.token.text == token.text && reused.token.tokenKind == token.tokenKind)
					{
						reused.missing = false;
						reused.syntaxError = null;
						
						reused.token = token;
						reused.parent = this;
						reused.childIndex = numValidNodes;
						++numValidNodes;
						
						Debug.Log("reused " + reused.token + " from line " + (reused.line + 1));
						return reused;
					}
				}
				
				var leaf = new Leaf { token = token, parent = this, childIndex = numValidNodes };
				if (numValidNodes == nodes.Count)
				{
					nodes.Add(leaf);
					++numValidNodes;
				}
				else
				{
					nodes.Insert(numValidNodes++, leaf);
					for (var i = numValidNodes; i < nodes.Count; ++i)
						++nodes[i].childIndex;
				}
				
				return leaf;
			}
			
			public int InvalidateFrom(int index)
			{
				var numInvalidated = Mathf.Max(0, numValidNodes - index);
				numValidNodes -= numInvalidated;
				//for (var i = index; i < nodes.Count; ++i)
				//    nodes[i].parent = null;
				//nodes.RemoveRange(index, nodes.Count - index);
				return numInvalidated;
			}
			
			public void RemoveNodeAt(int index/*, bool canReuse = true*/)
			{
				if (index >= nodes.Count)
					return;
				
				//if (!canReuse)
				nodes[index].parent = null;
				
				if (index < numValidNodes)
					--numValidNodes;
				//if (!canReuse || index <= numValidNodes)
				{
					var node = nodes[index] as Node;
					if (node != null)
						node.Dispose();
					nodes.RemoveAt(index);
					for (var i = index; i < nodes.Count; ++i)
						--nodes[i].childIndex;
				}
				if (/*nodes.Count == 0 &&*/ parent != null && !HasLeafs(false))
					parent.RemoveNodeAt(childIndex/*, canReuse*/);
			}
			
			//static int reusedNodesCounter, createdNodesCounter;
			
			public Node AddNode(CsGrammar.Id rule, CsGrammar.IScanner scanner, out bool skipParsing)
			{
				skipParsing = false;
				
				bool removedReusable = false;
				
				if (numValidNodes < nodes.Count)
				{
					var reusable = nodes[numValidNodes] as Node;
					if (reusable != null)
					{
						var firstLeaf = reusable.GetFirstLeaf(false);
						if (reusable.grammarNode != rule)
						{
							//	Debug.Log("Cannot reuse (different rules) " + rule.GetName() + " at line " + scanner.CurrentLine() + ":"
							//		+ scanner.CurrentTokenIndex() + " vs. " + reusable.RuleName);
							if (firstLeaf == null || firstLeaf.token == null || firstLeaf.line <= scanner.CurrentLine() - 1)
							{
								reusable.Dispose();
								removedReusable = true;
							}
						}
						else
						{
							if (firstLeaf != null && firstLeaf.token != null && firstLeaf.line > scanner.CurrentLine() - 1)
							{
								// Ignore this node for now
							}
							else if (firstLeaf == null || firstLeaf.token != null && firstLeaf.syntaxError != null)
							{
								//	Debug.Log("Could reuse " + rule.GetName() + " at line " + scanner.CurrentLine() + ":"
								//		+ scanner.CurrentTokenIndex() + " (firstLeaf is null) ");
								reusable.Dispose();
								removedReusable = true;
								//	nodes.RemoveAt(numValidNodes);
								//	for (var i = numValidNodes; i < nodes.Count; ++i)
								//		--nodes[i].childIndex;
							}
							else if (firstLeaf.token == scanner.Current)
							{
								var lastLeaf = reusable.GetLastLeaf();
								if (lastLeaf != null && !reusable.HasErrors())
								{
									if (lastLeaf.token != null)
									{
										//if (++reusedNodesCounter + createdNodesCounter == 1)
										//{
										//	UnityEditor.EditorApplication.delayCall += () => {
										//		Debug.Log("Nodes - Created: " + createdNodesCounter + " Reused: " + reusedNodesCounter);
										//		createdNodesCounter = 0;
										//		reusedNodesCounter = 0;
										//	};
										//}
										
										/*var moved =*/ ((CsGrammar.Scanner) scanner).MoveAfterLeaf(lastLeaf);
										//	Debug.Log(moved  + " skipping to " + scanner.CurrentGrammarNode + " at " + lastLeaf.line +":" + lastLeaf.tokenIndex);
										skipParsing = true;
										//}
										
										//if (lastLeaf == null || lastLeaf.token == null)
										//{
										////	Debug.LogWarning("lastLeaf has no token! " + lastLeaf);
										//}
										//else
										//{
										//	Debug.Log("Reused full " + rule.GetName() + " from line " + (firstLeaf.line + 1) + " up to line " + scanner.CurrentLine() + ":"
										//		+ scanner.CurrentTokenIndex() + " (" + scanner.Current.text + "...) ");
										++numValidNodes;
										return scanner.CurrentParseTreeNode;
									}
								}
								else
								{
									//Debug.Log(firstLeaf.line);
									reusable.Dispose();
									removedReusable = true;
								}
							}
							else if (reusable.numValidNodes == 0)
							{
								//if (++reusedNodesCounter + createdNodesCounter == 1)
								//{
								//	UnityEditor.EditorApplication.delayCall += () => {
								//		Debug.Log("Nodes - Created: " + createdNodesCounter + " Reused: " + reusedNodesCounter);
								//		createdNodesCounter = 0;
								//		reusedNodesCounter = 0;
								//	};
								//}
								
								//Debug.Log("Reusing " + rule.GetName() + " at line " + scanner.CurrentLine() + ":"
								//	+ scanner.CurrentTokenIndex() + " (" + scanner.Current.text + "...) reusable.numValidNodes is 0");
								++numValidNodes;
								reusable.syntaxError = null;
								reusable.missing = false;
								return reusable;
							}
							else if (scanner.Current != null && (firstLeaf.token == null || firstLeaf.line <= scanner.CurrentLine() - 1))
							{
								//	Debug.Log("Cannot reuse " + rule.GetName() + " at line " + scanner.CurrentLine() + ":"
								//		+ scanner.CurrentTokenIndex() + " (" + scanner.Current.text + "...) ");
								reusable.Dispose();
								if (firstLeaf.token == null || firstLeaf.line == scanner.CurrentLine() - 1)
								{
									removedReusable = true;
								}
								else
								{
									nodes.RemoveAt(numValidNodes);
									for (var i = numValidNodes; i < nodes.Count; ++i)
										--nodes[i].childIndex;
									return AddNode(rule, scanner, out skipParsing);
								}
							}
							else
							{
								//	Debug.Log("Not reusing anything (scanner.Current is null). reusable is " + reusable.RuleName);
							}
						}
					}
				}
				
				//if (reusedNodesCounter + ++createdNodesCounter == 1)
				//{
				//	UnityEditor.EditorApplication.delayCall += () => {
				//		Debug.Log("Nodes - Created: " + createdNodesCounter + " Reused: " + reusedNodesCounter);
				//		createdNodesCounter = 0;
				//		reusedNodesCounter = 0;
				//	};
				//}
				
				var node = new Node(rule) { parent = this, childIndex = numValidNodes };
				if (numValidNodes == nodes.Count)
				{
					nodes.Add(node);
					++numValidNodes;
				}
				else
				{
					if (removedReusable)
						nodes[numValidNodes] = node;
					else
						nodes.Insert(numValidNodes, node);
					++numValidNodes;
					//	for (var i = numValidNodes; i < nodes.Count; ++i)
					//		++nodes[i].childIndex;
				}
				if (numValidNodes < nodes.Count && nodes[numValidNodes].childIndex != numValidNodes)
					for (var i = numValidNodes; i < nodes.Count; ++i)
						nodes[i].childIndex = i;
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
					if (syntaxError != null)
						sb.Append("? ");
					sb.AppendLine(id.Rule.GetNt());
					if (syntaxError != null)
						sb.Append(' ').AppendLine(syntaxError);
				}
				
				++indent;
				for (var i = 0; i < numValidNodes; ++i)
					nodes[i].Dump(sb, indent);
			}
			
			public override string Print()
			{
				var result = string.Empty;
				for (var i = 0; i < numValidNodes; i++)
				{
					var child = nodes[i];
					result += child.Print();
				}
				return result;
			}
			
			public override bool IsLit(string litText)
			{
				return false;
			}
			
			public override bool HasLeafs(bool validNodesOnly = true)
			{
				var count = validNodesOnly ? numValidNodes : nodes.Count;
				for (var i = 0; i < count; i++)
				{
					var n = nodes[i];
					if (n.HasLeafs(validNodesOnly))
						return true;
				}
				return false;
			}
			
			public override bool HasErrors(bool validNodesOnly = true)
			{
				var count = validNodesOnly ? numValidNodes : nodes.Count;
				for (int i = 0; i < count; i++)
				{
					var n = nodes[i];
					if (n.HasErrors(validNodesOnly))
						return true;
				}
				return false;
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
							//	Debug.LogWarning("Leaf has no token! " + leaf);
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
			
			public void Exclude()
			{
				if (nodes.Count != 1)
					return;
				parent.nodes[childIndex] = nodes[0];
				nodes[0].parent = parent;
				nodes[0].childIndex = childIndex;
			}
			
			//public bool TryReuse(CsGrammar.IScanner scanner)
			//{
			//    if (numValidNodes == nodes.Count)
			//        return false;
			//    if (scanner.CurrentParseTreeNode)
			//}
			
			public void Reduce()
			{
				//if (numValidNodes < nodes.Count)
				//{
				//    for (var i = numValidNodes; i < nodes.Count; ++i)
				//        nodes[i].parent = null;
				////	nodes.RemoveRange(numValidNodes, nodes.Count - numValidNodes);
				//}
			}
			
			public void CleanUp()
			{
				for (var i = nodes.Count; i --> 0; )
				{
					var child = nodes[i] as Node;
					if (child != null)
						child.CleanUp();
				}
				if (numValidNodes < nodes.Count)
				{
					for (var j = nodes.Count; j --> numValidNodes; )
					{
						var child = nodes[j] as ParseTree.Node;
						if (child != null)
							child.Dispose();
					}
					nodes.RemoveRange(numValidNodes, nodes.Count - numValidNodes);
				}
			}
			
			public void Dispose()
			{
				for (var i = nodes.Count; i --> 0; )
				{
					var child = nodes[i] as Node;
					if (child != null)
						child.Dispose();
				}
				
				if (declaration != null && declaration.scope != null)
				{
					//if (declaration.definition != null)
					//	Debug.Log("Removing " + declaration.definition.ReflectionName);
					//else
					//	Debug.Log("Removing null declaration! " + declaration.kind);
					declaration.scope.RemoveDeclaration(declaration);
					++ParseTree.resolverVersion;
					if (ParseTree.resolverVersion == 0)
						++ParseTree.resolverVersion;
				}
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

	[Flags]
	public enum IdentifierCompletionsType
	{
		None				= 1<<0,
		Namespace			= 1<<1,
		TypeName			= 1<<2,
		ArrayType			= 1<<3,
		NonArrayType		= 1<<4,
		ValueType			= 1<<5,
		SimpleType			= 1<<6,
		ExceptionClassType	= 1<<7,
		AttributeClassType	= 1<<8,
		Member				= 1<<9,
		Static				= 1<<10,
		Value				= 1<<11,
		ArgumentName		= 1<<12,
		MemberName          = 1<<13,
	}

	public abstract class CsGrammar
	{
        public Rule r_compilationUnit { get; private set; }
        

        readonly Id EOF, IDENTIFIER, NAME, LITERAL;

        public int tokenIdentifier,
            tokenName,
            tokenLiteral,
            tokenAttribute,
            tokenStatement,
            tokenClassBody,
            tokenStructBody,
            tokenInterfaceBody,
            tokenNamespaceBody,
            tokenBinaryOperator,
            tokenExpectedType,
            tokenMemberInitializer,
            tokenNamedParameter,
            tokenEOF;

        void InitializeTokenCategories()
        {
            tokenIdentifier = TokenToId("IDENTIFIER");
            tokenName = TokenToId("NAME");
            tokenLiteral = TokenToId("LITERAL");
            tokenAttribute = TokenToId(".ATTRIBUTE");
            tokenStatement = TokenToId(".STATEMENT");
            tokenClassBody = TokenToId(".CLASSBODY");
            tokenStructBody = TokenToId(".STRUCTBODY");
            tokenInterfaceBody = TokenToId(".INTERFACEBODY");
            tokenNamespaceBody = TokenToId(".NAMESPACEBODY");
            tokenBinaryOperator = TokenToId(".BINARYOPERATOR");
            tokenExpectedType = TokenToId(".EXPECTEDTYPE");
            tokenMemberInitializer = TokenToId(".MEMBERINITIALIZER");
            tokenNamedParameter = TokenToId(".NAMEDPARAMETER");
            tokenEOF = TokenToId("EOF");
        }

        private Parser parser;

        public CsGrammar()
        {

            EOF = new Id("EOF");
            IDENTIFIER = new Id("IDENTIFIER");
            LITERAL = new Id("LITERAL");
            NAME = new NameId();

            //	NAME = new Id("IDENTIFIER");
            //	tokenName = TokenToId("NAME");
            //	NAME.lookahead.Add(new TokenSet(tokenName));

            var externAliasDirective = new Id("externAliasDirective");
            var usingDirective = new Id("usingDirective");
            //var globalAttribute = new Id("globalAttribute");
            var namespaceMemberDeclaration = new Id("namespaceMemberDeclaration");
            var namespaceDeclaration = new Id("namespaceDeclaration");
            var qualifiedIdentifier = new Id("qualifiedIdentifier");
            var namespaceBody = new Id("namespaceBody");
            //var typeDeclaration = new Id("typeDeclaration");
            var attributes = new Id("attributes");
            var modifiers = new Id("modifiers");
            var typeParameterList = new Id("typeParameterList");
            var typeParameter = new Id("typeParameter");
            var classBase = new Id("classBase");
            var typeParameterConstraintsClauses = new Id("typeParameterConstraintsClauses");
            var typeParameterConstraintsClause = new Id("typeParameterConstraintsClause");
            var classBody = new Id("classBody");
            var classMemberDeclaration = new Id("classMemberDeclaration");
            var attribute = new Id("attribute");
            var typeName = new Id("typeName");
            var argumentList = new Id("argumentList");
            var attributeArgumentList = new Id("attributeArgumentList");
            var expression = new Id("expression");
            var constantExpression = new Id("constantExpression");
            var primaryExpression = new Id("primaryExpression");
            var arrayCreationExpression = new Id("arrayCreationExpression");
            var implicitArrayCreationExpression = new Id("implicitArrayCreationExpression");
            var constantDeclaration = new Id("constantDeclaration");
            var fieldDeclaration = new Id("fieldDeclaration");
            var methodDeclaration = new Id("methodDeclaration");
            var propertyDeclaration = new Id("propertyDeclaration");
            var eventDeclaration = new Id("eventDeclaration");
            var indexerDeclaration = new Id("indexerDeclaration");
            var operatorDeclaration = new Id("operatorDeclaration");
            var constructorDeclaration = new Id("constructorDeclaration");
            var destructorDeclaration = new Id("destructorDeclaration");
            //var staticConstructorDeclaration = new Id("staticConstructorDeclaration");
            var constantDeclarators = new Id("constantDeclarators");
            var constantDeclarator = new Id("constantDeclarator");
            var type = new Id("type");
            var type2 = new Id("type2");
            var predefinedType = new Id("predefinedType");
            var variableDeclarators = new Id("variableDeclarators");
            var variableDeclarator = new Id("variableDeclarator");
            var arrayInitializer = new Id("arrayInitializer");
            var variableInitializer = new Id("variableInitializer");
            var variableInitializerList = new Id("variableInitializerList");
            var simpleType = new Id("simpleType");
            var exceptionClassType = new Id("exceptionClassType");
            //var arrayType = new Id("arrayType");
            var nonArrayType = new Id("nonArrayType");
            var rankSpecifier = new Id("rankSpecifier");
            var numericType = new Id("numericType");
            var integralType = new Id("integralType");
            var floatingPointType = new Id("floatingPointType");


            r_compilationUnit = new Rule("compilationUnit",
                new Many(externAliasDirective) - new Many(usingDirective)
                    //new Many(globalAttribute),
                    - new Many(namespaceMemberDeclaration) - EOF
                )
            { semantics = SemanticFlags.CompilationUnitScope };

            parser = new Parser(r_compilationUnit, this);


            //NAME = new Id("NAME");
            //parser.Add(new Rule("NAME",
            //    IDENTIFIER
            //    ));


            parser.Add(new Rule("externAliasDirective",
                new Seq("extern", "alias", IDENTIFIER, ";")
                )
            { semantics = SemanticFlags.ExternAlias });

            var usingNamespaceDirective = new Id("usingNamespaceDirective");
            var usingAliasDirective = new Id("usingAliasDirective");
            var globalNamespace = new Id("globalNamespace");
            var namespaceName = new Id("namespaceName");
            var namespaceOrTypeName = new Id("namespaceOrTypeName");
            var PARTIAL = new Id("PARTIAL");

            parser.Add(new Rule("usingDirective",
                "using" - (new If(IDENTIFIER - "=", usingAliasDirective) | usingNamespaceDirective) - ";"
                ));

            parser.Add(new Rule("PARTIAL",
                new If(s => s.Current.text == "partial", IDENTIFIER) | "partial"
            )
            { contextualKeyword = true });

            parser.Add(new Rule("namespaceMemberDeclaration",
                namespaceDeclaration
                | (attributes - modifiers - new Alt(
                    new Seq(
                        new Opt(PARTIAL),
                        new Id("classDeclaration") | new Id("structDeclaration") | new Id("interfaceDeclaration")),
                    new Id("enumDeclaration"),
                    new Id("delegateDeclaration"))
                )
                | ".NAMESPACEBODY"
                ));

            parser.Add(new Rule("namespaceDeclaration",
                "namespace" - qualifiedIdentifier - namespaceBody - new Opt(";")
                )
            { semantics = SemanticFlags.NamespaceDeclaration });

            parser.Add(new Rule("qualifiedIdentifier",
                NAME - new Many(new Lit(".") - NAME)
                ));

            parser.Add(new Rule("namespaceBody",
                "{" - new Many(externAliasDirective) - new Many(usingDirective) - new Many(namespaceMemberDeclaration) - "}"
                )
            { semantics = SemanticFlags.NamespaceBodyScope });

            //parser.Add(new Rule("typeDeclaration",
            //   new Alt(
            //       new Seq(
            //           new Opt("partial"),
            //           new Id("classDeclaration") | new Id("structDeclaration") | new Id("interfaceDeclaration")),
            //       new Id("enumDeclaration"),
            //       new Id("delegateDeclaration"))
            //   ));


            //	parser = new Parser(r_compilationUnit, this);


            parser.Add(new Rule("usingNamespaceDirective",
                namespaceName
                )
            { semantics = SemanticFlags.UsingNamespace });

            parser.Add(new Rule("namespaceName",
                namespaceOrTypeName
                ));

            parser.Add(new Rule("usingAliasDirective",
                IDENTIFIER - "=" - namespaceOrTypeName
                )
            { semantics = SemanticFlags.UsingAlias });

            parser.Add(new Rule("classDeclaration",
                new Seq(
                    "class",
                    NAME,
                    new Opt(typeParameterList),
                    new Opt(classBase),
                    new Opt(typeParameterConstraintsClauses),
                    classBody,
                    new Opt(";"))
                )
            { semantics = SemanticFlags.ClassDeclaration | SemanticFlags.TypeDeclarationScope });

            parser.Add(new Rule("typeParameterList",
                "<" - attributes - typeParameter - new Many("," - attributes - typeParameter) - ">"
                ));

            parser.Add(new Rule("typeParameter",
                NAME
                )
            { semantics = SemanticFlags.TypeParameterDeclaration });

            var interfaceTypeList = new Id("interfaceTypeList");

            parser.Add(new Rule("classBase",
                ":" - interfaceTypeList
                )
            { semantics = SemanticFlags.ClassBaseScope });

            parser.Add(new Rule("interfaceTypeList",
                (typeName | "object") - new Many("," - typeName)
                )
            { semantics = SemanticFlags.BaseListDeclaration });

            parser.Add(new Rule("classBody",
                "{" - new Many(classMemberDeclaration) - "}"
                )
            { semantics = SemanticFlags.ClassBodyScope });

            var interfaceDeclaration = new Id("interfaceDeclaration");
            var classDeclaration = new Id("classDeclaration");
            var structDeclaration = new Id("structDeclaration");
            var memberName = new Id("memberName");
            var qid = new Id("qid");
            var enumDeclaration = new Id("enumDeclaration");
            //var enumDeclarator = new Id("enumDeclarator");
            var delegateDeclaration = new Id("delegateDeclaration");
            var conversionOperatorDeclaration = new Id("conversionOperatorDeclaration");

            parser.Add(new Rule("memberName",
                qid
                ));

            parser.Add(new Rule("classMemberDeclaration",
                new Seq(
                    attributes,
                    modifiers,
                    constantDeclaration
                    | "void" - methodDeclaration
                    | new If(PARTIAL, PARTIAL - ("void" - methodDeclaration | classDeclaration | structDeclaration | interfaceDeclaration))
                    | new If(IDENTIFIER - "(", constructorDeclaration)
                    | new Seq(
                        type,
                        new If(memberName - "(", methodDeclaration)
                        | new If(memberName - "{", propertyDeclaration)
                        | new If(typeName - "." - "this", typeName - "." - indexerDeclaration)
                        | new If(predefinedType - "." - "this", predefinedType - "." - indexerDeclaration)
                        | indexerDeclaration
                        | fieldDeclaration
                        | operatorDeclaration)
                    | classDeclaration
                    | structDeclaration
                    | interfaceDeclaration
                    | enumDeclaration
                    | delegateDeclaration
                    | eventDeclaration
                    | conversionOperatorDeclaration)
                | destructorDeclaration
                | ".CLASSBODY"
                ) /*{ semantics = SemanticFlags.MemberDeclarationScope }*/);

            var constructorDeclarator = new Id("constructorDeclarator");
            var constructorBody = new Id("constructorBody");
            var constructorInitializer = new Id("constructorInitializer");
            var destructorDeclarator = new Id("destructorDeclarator");
            var destructorBody = new Id("destructorBody");
            var arguments = new Id("arguments");
            var attributeArguments = new Id("attributeArguments");
            var formalParameterList = new Id("formalParameterList");
            var block = new Id("block");
            var statementList = new Id("statementList");

            parser.Add(new Rule("constructorDeclaration",
                constructorDeclarator - constructorBody
                )
            { semantics = SemanticFlags.MethodDeclarationScope | SemanticFlags.ConstructorDeclarator });

            parser.Add(new Rule("constructorDeclarator",
                IDENTIFIER - "(" - new Opt(formalParameterList) - ")" - new Opt(constructorInitializer)
                ));

            parser.Add(new Rule("constructorInitializer",
                ":" - (new Lit("base") | "this") - arguments
                )
            { semantics = SemanticFlags.ConstructorInitializerScope });

            parser.Add(new Rule("constructorBody",
                "{" - statementList - "}"
                | ";"
                )
            { semantics = SemanticFlags.MethodBodyScope });

            parser.Add(new Rule("destructorDeclaration",
                destructorDeclarator - destructorBody
                )
            { semantics = SemanticFlags.MethodDeclarationScope | SemanticFlags.DestructorDeclarator });

            parser.Add(new Rule("destructorDeclarator",
                "~" - new Opt("extern") - IDENTIFIER - "(" - ")"
                ));

            parser.Add(new Rule("destructorBody",
                "{" - statementList - "}"
                | ";"
                )
            { semantics = SemanticFlags.MethodBodyScope });

            parser.Add(new Rule("constantDeclaration",
                "const" - type - constantDeclarators - ";"
                ));

            parser.Add(new Rule("constantDeclarators",
                constantDeclarator - new Many("," - constantDeclarator)
                ));

            parser.Add(new Rule("constantDeclarator",
                NAME - "=" - constantExpression
                )
            { semantics = SemanticFlags.ConstantDeclarator });

            parser.Add(new Rule("constantExpression",
                expression
                | ".EXPECTEDTYPE"
                ));

            var methodHeader = new Id("methodHeader");
            var methodBody = new Id("methodBody");
            var formalParameter = new Id("formalParameter");
            var fixedParameter = new Id("fixedParameter");
            var parameterModifier = new Id("parameterModifier");
            var defaultArgument = new Id("defaultArgument");
            var parameterArray = new Id("parameterArray");
            var statement = new Id("statement");
            var typeVariableName = new Id("typeVariableName");
            var typeParameterConstraintList = new Id("typeParameterConstraintList");
            var secondaryConstraintList = new Id("secondaryConstraintList");
            var secondaryConstraint = new Id("secondaryConstraint");
            var constructorConstraint = new Id("constructorConstraint");
            var WHERE = new Id("WHERE");

            parser.Add(new Rule("methodDeclaration",
                methodHeader - methodBody
                )
            { semantics = SemanticFlags.MethodDeclarationScope | SemanticFlags.MethodDeclarator });

            parser.Add(new Rule("methodHeader",
                memberName - "(" - new Opt(formalParameterList) - ")" - new Opt(typeParameterConstraintsClauses)
                ));

            parser.Add(new Rule("typeParameterConstraintsClauses",
                typeParameterConstraintsClause - new Many(typeParameterConstraintsClause)
                ));//{ semantics = SemanticFlags.TypeParameterConstraintsScope });

            parser.Add(new Rule("WHERE",
                new If(s => s.Current.text == "where", IDENTIFIER) | "where"
                )
            { contextualKeyword = true });

            parser.Add(new Rule("typeParameterConstraintsClause",
                WHERE - typeVariableName - ":" - typeParameterConstraintList
                ));//{ semantics = SemanticFlags.TypeParameterConstraint });

            parser.Add(new Rule("typeParameterConstraintList",
                (new Lit("class") | "struct") - new Opt("," - secondaryConstraintList)
                | secondaryConstraintList
                ));

            parser.Add(new Rule("secondaryConstraintList",
                secondaryConstraint - new Opt("," - new Id("secondaryConstraintList"))
                | constructorConstraint
                ));

            parser.Add(new Rule("secondaryConstraint",
                typeName // | typeVariableName
                ));

            parser.Add(new Rule("typeVariableName",
                IDENTIFIER
                ));

            parser.Add(new Rule("constructorConstraint",
                new Lit("new") - "(" - ")"
                ));

            //primary_constraint:
            //	class_type
            //	| 'class'
            //	| 'struct' ;

            parser.Add(new Rule("methodBody",
                "{" - statementList - "}"
                | ";"
                )
            { semantics = SemanticFlags.MethodBodyScope });

            parser.Add(new Rule("block",
                "{" - statementList - "}"
                | ";"
                )
            { semantics = SemanticFlags.CodeBlockScope });

            parser.Add(new Rule("statementList",
                new Many(new IfNot(new Seq("default", ":"), statement))
                ));

            //	var declarationStatement = new Id("declarationStatement");
            var labeledStatement = new Id("labeledStatement");
            var embeddedStatement = new Id("embeddedStatement");
            var selectionStatement = new Id("selectionStatement");
            var iterationStatement = new Id("iterationStatement");
            var jumpStatement = new Id("jumpStatement");
            var tryStatement = new Id("tryStatement");
            var lockStatement = new Id("lockStatement");
            var usingStatement = new Id("usingStatement");
            var yieldStatement = new Id("yieldStatement");
            var expressionStatement = new Id("expressionStatement");
            var breakStatement = new Id("breakStatement");
            var continueStatement = new Id("continueStatement");
            var gotoStatement = new Id("gotoStatement");
            var returnStatement = new Id("returnStatement");
            var throwStatement = new Id("throwStatement");
            var checkedStatement = new Id("checkedStatement");
            var uncheckedStatement = new Id("uncheckedStatement");
            var localVariableDeclaration = new Id("localVariableDeclaration");
            var localVariableType = new Id("localVariableType");
            var localVariableDeclarators = new Id("localVariableDeclarators");
            var localVariableDeclarator = new Id("localVariableDeclarator");
            var localVariableInitializer = new Id("localVariableInitializer");
            //var stackallocInitializer = new Id("stackallocInitializer");
            var localConstantDeclaration = new Id("localConstantDeclaration");
            //var unmanagedType = new Id("unmanagedType");
            var resourceAcquisition = new Id("resourceAcquisition");

            var VAR = new Id("VAR");

            parser.Add(new Rule("VAR",
                //new If(s => s.Current.text == "var", IDENTIFIER)
                IDENTIFIER | "var"
                )
            { contextualKeyword = true });

            parser.Add(new Rule("statement",
                new If((type | "var") - IDENTIFIER - (new Lit(";") | "=" | "[" | ","), localVariableDeclaration - ";")
                | new If(IDENTIFIER - ":", labeledStatement)
                | localConstantDeclaration
                | embeddedStatement
                ));

            //	parser.Add(new Rule("declarationStatement",
            //		new Seq( localVariableDeclaration | localConstantDeclaration, ";" )
            //		));

            parser.Add(new Rule("localVariableDeclaration",
                localVariableType - localVariableDeclarators
                ));

            parser.Add(new Rule("localVariableType",
                new If(s => s.Current.text == "var", VAR)
                | type
                ));

            parser.Add(new Rule("localVariableDeclarators",
                localVariableDeclarator - new Many("," - localVariableDeclarator)
                ));

            parser.Add(new Rule("localVariableDeclarator",
                NAME - new Opt("=" - localVariableInitializer)
                )
            { semantics = SemanticFlags.LocalVariableDeclarator });

            parser.Add(new Rule("localVariableInitializer",
                expression | arrayInitializer //| stackallocInitializer
                | ".EXPECTEDTYPE"
                )
            { semantics = SemanticFlags.LocalVariableInitializerScope });

            //parser.Add(new Rule("stackallocInitializer",
            //    new Seq( "stackalloc", unmanagedType, "[", expression, "]" )
            //    ));

            parser.Add(new Rule("localConstantDeclaration",
                "const" - type - constantDeclarators - ";"
                ));

            //parser.Add(new Rule("unmanagedType",
            //    type
            //    ));

            parser.Add(new Rule("labeledStatement",
                IDENTIFIER - ":" - statement
                )
            { semantics = SemanticFlags.LabeledStatement });

            var YIELD = new Id("YIELD");

            parser.Add(new Rule("YIELD",
                    //new If(s => s.Current.text == "yield", IDENTIFIER)
                    IDENTIFIER | "yield"
                    )
            { contextualKeyword = true });

            parser.Add(new Rule("embeddedStatement",
                block
                | selectionStatement    // if, switch
                | iterationStatement    // while, do, for, foreach
                | jumpStatement         // break, continue, goto, return, throw
                | tryStatement
                | lockStatement
                | usingStatement
                | new If(
                    s => {
                        if (s.Current.text != "yield")
                            return false;
                        string next = s.Lookahead(1).text;
                        return next == "return" || next == "break";
                    }, yieldStatement)
                | "yield return" | "yield break;" // auto-completion hint only, while the actual code will be handled by the previous line
                | new If(new Lit("checked") - "{", checkedStatement)
                | new If(new Lit("unchecked") - "{", uncheckedStatement)
                | expressionStatement  // expression!
                | ".STATEMENT"
                )
            { semantics = SemanticFlags.CodeBlockScope });

            parser.Add(new Rule("lockStatement",
                new Seq("lock", "(", expression, ")", embeddedStatement)
                ));

            parser.Add(new Rule("checkedStatement",
                new Seq("checked", block)
            ));

            parser.Add(new Rule("uncheckedStatement",
                new Seq("unchecked", block)
            ));

            parser.Add(new Rule("usingStatement",
                new Seq("using", "(", resourceAcquisition, ")", embeddedStatement)
                )
            { semantics = SemanticFlags.UsingStatementScope });

            parser.Add(new Rule("resourceAcquisition",
                new If(localVariableType - IDENTIFIER, localVariableDeclaration)
                | expression
                ));

            parser.Add(new Rule("yieldStatement",
                YIELD -
                    (("return" - expression - ";")
                    | (new Lit("break") - ";"))
                ));

            var ifStatement = new Id("ifStatement");
            var elseStatement = new Id("elseStatement");
            var switchStatement = new Id("switchStatement");
            var booleanExpression = new Id("booleanExpression");
            var switchBlock = new Id("switchBlock");
            var switchSection = new Id("switchSection");
            var switchLabel = new Id("switchLabel");
            var statementExpression = new Id("statementExpression");

            parser.Add(new Rule("selectionStatement",
                ifStatement | switchStatement
                ));

            parser.Add(new Rule("ifStatement",
                new Lit("if") - "(" - booleanExpression - ")" - embeddedStatement - new Opt(elseStatement)
                ));

            parser.Add(new Rule("elseStatement",
                "else" - embeddedStatement
                ));

            parser.Add(new Rule("switchStatement",
                new Lit("switch") - "(" - expression - ")" - switchBlock
                ));

            parser.Add(new Rule("switchBlock",
                "{" - new Many(switchSection) - "}"
                )
            { semantics = SemanticFlags.SwitchBlockScope });

            parser.Add(new Rule("switchSection",
                new Many(switchLabel) - statement - statementList
                ));

            parser.Add(new Rule("switchLabel",
                "case" - constantExpression - ":"
                | new Seq("default", ":")
                ));

            parser.Add(new Rule("expressionStatement",
                statementExpression - ";"
                ));

            parser.Add(new Rule("jumpStatement",
                breakStatement
                | continueStatement
                | gotoStatement
                | returnStatement
                | throwStatement
                ));

            parser.Add(new Rule("breakStatement",
                new Seq("break", ";")
                ));

            parser.Add(new Rule("continueStatement",
                new Seq("continue", ";")
                ));

            parser.Add(new Rule("gotoStatement",
                "goto" - (
                    IDENTIFIER
                    | "case" - constantExpression
                    | "default")
                - ";"
                ));

            parser.Add(new Rule("returnStatement",
                "return" - new Opt(expression) - ";"
                ));

            parser.Add(new Rule("throwStatement",
                "throw" - new Opt(expression) - ";"
                ));

            var catchClauses = new Id("catchClauses");
            var finallyClause = new Id("finallyClause");
            var specificCatchClauses = new Id("specificCatchClauses");
            var specificCatchClause = new Id("specificCatchClause");
            var catchExceptionIdentifier = new Id("catchExceptionIdentifier");
            var generalCatchClause = new Id("generalCatchClause");

            parser.Add(new Rule("tryStatement",
                "try" - block - (catchClauses - new Opt(finallyClause) | finallyClause)
                ));

            parser.Add(new Rule("catchClauses",
                new If(new Seq("catch", "("), specificCatchClauses - new Opt(generalCatchClause))
                | generalCatchClause
                ));

            parser.Add(new Rule("specificCatchClauses",
                specificCatchClause - new Many(new If(new Seq("catch", "("), specificCatchClause))
                ));

            parser.Add(new Rule("specificCatchClause",
                new Lit("catch") - "(" - exceptionClassType - new Opt(catchExceptionIdentifier) - ")" - block
                )
            { semantics = SemanticFlags.SpecificCatchScope });

            parser.Add(new Rule("catchExceptionIdentifier",
                NAME
                )
            { semantics = SemanticFlags.CatchExceptionParameterDeclaration });

            parser.Add(new Rule("generalCatchClause",
                "catch" - block
                ));

            parser.Add(new Rule("finallyClause",
                "finally" - block
                ));

            parser.Add(new Rule("formalParameterList",
                formalParameter - new Many("," - formalParameter)
                )
            { semantics = SemanticFlags.FormalParameterListScope });

            parser.Add(new Rule("formalParameter",
                attributes - (fixedParameter | parameterArray)
                //	| "__arglist"
                ));

            parser.Add(new Rule("fixedParameter",
                new Opt(parameterModifier) - type - NAME - new Opt(defaultArgument)
                )
            { semantics = SemanticFlags.FixedParameterDeclaration });

            parser.Add(new Rule("parameterModifier",
                new Lit("ref") | "out" | "this"
                ));

            parser.Add(new Rule("defaultArgument",
                "=" - (expression | ".EXPECTEDTYPE")
                ));

            parser.Add(new Rule("parameterArray",
                "params" - type - NAME
                )
            { semantics = SemanticFlags.ParameterArrayDeclaration });

            var whileStatement = new Id("whileStatement");
            var doStatement = new Id("doStatement");
            var forStatement = new Id("forStatement");
            var foreachStatement = new Id("foreachStatement");
            var forInitializer = new Id("forInitializer");
            //var forCondition = new Id("forCondition");
            var forIterator = new Id("forIterator");
            var statementExpressionList = new Id("statementExpressionList");

            parser.Add(new Rule("iterationStatement",
                whileStatement | doStatement | forStatement | foreachStatement
                ));

            parser.Add(new Rule("whileStatement",
                new Seq("while", "(", booleanExpression, ")", embeddedStatement)
                ));

            parser.Add(new Rule("doStatement",
                "do" - embeddedStatement - "while" - "(" - booleanExpression - ")" - ";"
                ));

            parser.Add(new Rule("forStatement",
                new Seq("for", "(", new Opt(forInitializer), ";", new Opt(booleanExpression), ";",
                    new Opt(forIterator), ")", embeddedStatement)
                )
            { semantics = SemanticFlags.ForStatementScope });

            parser.Add(new Rule("forInitializer",
                new If(localVariableType - IDENTIFIER, localVariableDeclaration)
                | statementExpressionList
                ));

            parser.Add(new Rule("forIterator",
                statementExpressionList
                ));

            parser.Add(new Rule("foreachStatement",
                new Lit("foreach") - "(" - localVariableType - NAME - "in" - expression - ")" - embeddedStatement
                )
            { semantics = SemanticFlags.ForStatementScope | SemanticFlags.ForEachVariableDeclaration });

            parser.Add(new Rule("statementExpressionList",
                new Seq(statementExpression, new Many(new Seq(",", statementExpression)))
                ));

            // TODO: should be assignment, call, increment, decrement, and new object expressions
            parser.Add(new Rule("statementExpression",
                expression
                ));

            var accessorDeclarations = new Id("accessorDeclarations");
            var getAccessorDeclaration = new Id("getAccessorDeclaration");
            var setAccessorDeclaration = new Id("setAccessorDeclaration");
            var accessorModifiers = new Id("accessorModifiers");
            var accessorBody = new Id("accessorBody");

            parser.Add(new Rule("indexerDeclaration",
                new Lit("this") - "[" - formalParameterList - "]" - "{" - accessorDeclarations - "}"
                )
            { semantics = SemanticFlags.IndexerDeclaration | SemanticFlags.AccessorsListScope });

            parser.Add(new Rule("propertyDeclaration",
                memberName - "{" - accessorDeclarations - "}"
                )
            { semantics = SemanticFlags.PropertyDeclaration | SemanticFlags.AccessorsListScope });

            parser.Add(new Rule("accessorModifiers",
                "internal" - new Opt("protected")
                | "protected" - new Opt("internal")
                | "public"
                | "private"
                ));

            var GET = new Id("GET");
            var SET = new Id("SET");

            parser.Add(new Rule("GET",
                IDENTIFIER | "get"
                )
            { contextualKeyword = true });

            parser.Add(new Rule("SET",
                IDENTIFIER | "set"
                )
            { contextualKeyword = true });

            parser.Add(new Rule("accessorDeclarations",
                attributes - new Opt(accessorModifiers)
                    - (new If(s => s.Current.text == "get", getAccessorDeclaration - new Opt(attributes - new Opt(accessorModifiers) - setAccessorDeclaration))
                    | setAccessorDeclaration - new Opt(attributes - new Opt(accessorModifiers) - getAccessorDeclaration))
                ));

            parser.Add(new Rule("getAccessorDeclaration",
                GET - accessorBody
                )
            { semantics = SemanticFlags.GetAccessorDeclaration });

            parser.Add(new Rule("setAccessorDeclaration",
                SET - accessorBody
                )
            { semantics = SemanticFlags.SetAccessorDeclaration });

            parser.Add(new Rule("accessorBody",
                "{" - statementList - "}"
                | ";"
                )
            { semantics = SemanticFlags.AccessorBodyScope });


            var eventDeclarators = new Id("eventDeclarators");
            var eventDeclarator = new Id("eventDeclarator");
            var eventWithAccessorsDeclaration = new Id("eventWithAccessorsDeclaration");
            var eventAccessorDeclarations = new Id("eventAccessorDeclarations");
            var addAccessorDeclaration = new Id("addAccessorDeclaration");
            var removeAccessorDeclaration = new Id("removeAccessorDeclaration");

            parser.Add(new Rule("eventDeclaration",
                "event" - typeName - (
                    new If(memberName - "{",
                        eventWithAccessorsDeclaration)
                    | eventDeclarators - ";")
                ));

            parser.Add(new Rule("eventWithAccessorsDeclaration",
                memberName - "{" - eventAccessorDeclarations - "}"
                )
            { semantics = SemanticFlags.EventWithAccessorsDeclaration | SemanticFlags.AccessorsListScope });

            parser.Add(new Rule("eventDeclarators",
                eventDeclarator - new Many("," - eventDeclarator)
                ));

            parser.Add(new Rule("eventDeclarator",
                NAME - new Opt("=" - variableInitializer)
                )
            { semantics = SemanticFlags.EventDeclarator });

            parser.Add(new Rule("eventAccessorDeclarations",
                attributes - (
                    new If(s => s.Current.text == "add", addAccessorDeclaration - attributes - removeAccessorDeclaration)
                    | removeAccessorDeclaration - attributes - addAccessorDeclaration)
                ));

            var ADD = new Id("ADD");
            var REMOVE = new Id("REMOVE");

            parser.Add(new Rule("ADD",
                IDENTIFIER | "add"
                )
            { contextualKeyword = true });

            parser.Add(new Rule("REMOVE",
                IDENTIFIER | "remove"
                )
            { contextualKeyword = true });

            parser.Add(new Rule("addAccessorDeclaration",
                ADD - accessorBody
                )
            { semantics = SemanticFlags.AddAccessorDeclaration });

            parser.Add(new Rule("removeAccessorDeclaration",
                REMOVE - accessorBody
                )
            { semantics = SemanticFlags.RemoveAccessorDeclaration });


            parser.Add(new Rule("fieldDeclaration",
                variableDeclarators - ";"
                ));

            parser.Add(new Rule("variableDeclarators",
                new Seq(variableDeclarator, new Many(new Seq(",", variableDeclarator)))
                ));

            parser.Add(new Rule("variableDeclarator",
                NAME - new Opt("=" - variableInitializer)
                )
            { semantics = SemanticFlags.VariableDeclarator });

            parser.Add(new Rule("variableInitializer",
                expression | arrayInitializer
                | ".EXPECTEDTYPE"
                ));

            //parser.Add(new Rule("arrayInitializer",
            //    new Seq( "{", new Opt(variableInitializerList), "}" )
            //    ));

            parser.Add(new Rule("modifiers",
                new Many(
                    new Lit("new") | "public" | "protected" | "internal" | "private" | "abstract"
                    | "sealed" | "static" | "readonly" | "volatile" | "virtual" | "override" | "extern"
                    )
                ));

            var operatorDeclarator = new Id("operatorDeclarator");
            var operatorBody = new Id("operatorBody");
            var operatorParameter = new Id("operatorParameter");
            var binaryOperatorPart = new Id("binaryOperatorPart");
            var unaryOperatorPart = new Id("unaryOperatorPart");
            var overloadableBinaryOperator = new Id("overloadableBinaryOperator");
            var overloadableUnaryOperator = new Id("overloadableUnaryOperator");
            var conversionOperatorDeclarator = new Id("conversionOperatorDeclarator");

            parser.Add(new Rule("operatorDeclaration",
                operatorDeclarator - operatorBody
                ));

            parser.Add(new Rule("operatorDeclarator",
                new Seq("operator",
                new Seq(new Lit("+") | "-", "(", operatorParameter, binaryOperatorPart | unaryOperatorPart)
                    | overloadableUnaryOperator - "(" - operatorParameter - unaryOperatorPart
                    | overloadableBinaryOperator - "(" - operatorParameter - binaryOperatorPart)
                )
            { semantics = SemanticFlags.OperatorDeclarator });

            parser.Add(new Rule("operatorParameter",
                type - NAME
                )
            { semantics = SemanticFlags.FixedParameterDeclaration });

            parser.Add(new Rule("unaryOperatorPart",
                ")"
                ));

            parser.Add(new Rule("binaryOperatorPart",
                "," - operatorParameter - ")"
                ));

            parser.Add(new Rule("overloadableUnaryOperator",
                /*'+' |  '-' | */ new Lit("!") | "~" | "++" | "--" | "true" | "false"
                ));

            parser.Add(new Rule("overloadableBinaryOperator",
                /*'+' | '-' | */
                // >> check needed
                new Lit("*") | "/" | "%" | "&" | "|" | "^" | "<<" | new If(new Seq(">", ">"), new Seq(">", ">")) | "==" | "!=" | ">" | "<" | ">=" | "<="
                ));

            parser.Add(new Rule("conversionOperatorDeclaration",
                conversionOperatorDeclarator - operatorBody
                ));

            parser.Add(new Rule("conversionOperatorDeclarator",
                (new Lit("implicit") | "explicit") - "operator" - type - "(" - operatorParameter - ")"
                )
            { semantics = SemanticFlags.ConversionOperatorDeclarator });

            parser.Add(new Rule("operatorBody",
                "{" - statementList - "}"
                | ";"
                )
            { semantics = SemanticFlags.MethodBodyScope });

            var typeOrGeneric = new Id("typeOrGeneric");
            var typeArgumentList = new Id("typeArgumentList");
            var unboundTypeRank = new Id("unboundTypeRank");
            var structInterfaces = new Id("structInterfaces");
            var structBody = new Id("structBody");
            var structMemberDeclaration = new Id("structMemberDeclaration");

            parser.Add(new Rule("structDeclaration",
                "struct" - NAME - new Opt(typeParameterList) - new Opt(structInterfaces)
                - new Opt(typeParameterConstraintsClauses) - structBody - new Opt(";")
                )
            { semantics = SemanticFlags.StructDeclaration | SemanticFlags.TypeDeclarationScope });

            //struct_modifier:
            //    'new' | 'public' | 'protected' | 'internal' | 'private' | 'unsafe' ;

            parser.Add(new Rule("structInterfaces",
                ":" - interfaceTypeList
                )
            { semantics = SemanticFlags.StructInterfacesScope });

            parser.Add(new Rule("structBody",
                "{" - new Many(structMemberDeclaration) - "}"
                )
            { semantics = SemanticFlags.StructBodyScope });

            parser.Add(new Rule("structMemberDeclaration",
                new Seq(
                    attributes,
                    modifiers,
                    constantDeclaration
                    | "void" - methodDeclaration
                    | new If(PARTIAL, PARTIAL - ("void" - methodDeclaration | classDeclaration | structDeclaration | interfaceDeclaration))
                    | new If(IDENTIFIER - "(", constructorDeclaration)
                    | new Seq(
                        type,
                        new If(memberName - "(", methodDeclaration)
                        | new If(memberName - "{", propertyDeclaration)
                        | new If(memberName - "." - "this", typeName - "." - indexerDeclaration)
                        | new If(predefinedType - "." - "this", predefinedType - "." - indexerDeclaration)
                        | indexerDeclaration
                        | fieldDeclaration
                        | operatorDeclaration)
                    | classDeclaration
                    | structDeclaration
                    | interfaceDeclaration
                    | enumDeclaration
                    | delegateDeclaration
                    | eventDeclaration
                    | conversionOperatorDeclaration
                    | ".STRUCTBODY")
                ));

            var interfaceBase = new Id("interfaceBase");
            var interfaceBody = new Id("interfaceBody");
            var interfaceMemberDeclaration = new Id("interfaceMemberDeclaration");
            var interfaceMethodDeclaration = new Id("interfaceMethodDeclaration");
            var interfaceEventDeclaration = new Id("interfaceEventDeclaration");
            var interfacePropertyDeclaration = new Id("interfacePropertyDeclaration");
            var interfaceIndexerDeclaration = new Id("interfaceIndexerDeclaration");
            var interfaceAccessorDeclarations = new Id("interfaceAccessorDeclarations");
            var interfaceGetAccessorDeclaration = new Id("interfaceGetAccessorDeclaration");
            var interfaceSetAccessorDeclaration = new Id("interfaceSetAccessorDeclaration");

            parser.Add(new Rule("interfaceDeclaration",
                "interface" - NAME - new Opt(typeParameterList) - new Opt(interfaceBase) -
                    new Opt(typeParameterConstraintsClauses) - interfaceBody - new Opt(";")
                )
            { semantics = SemanticFlags.InterfaceDeclaration | SemanticFlags.TypeDeclarationScope });

            parser.Add(new Rule("interfaceBase",
                ":" - interfaceTypeList
                )
            { semantics = SemanticFlags.InterfaceBaseScope });

            parser.Add(new Rule("interfaceBody",
                "{" - new Many(interfaceMemberDeclaration) - "}"
                )
            { semantics = SemanticFlags.InterfaceBodyScope });

            parser.Add(new Rule("interfaceMemberDeclaration",
                new Seq(attributes, modifiers,
                    "void" - interfaceMethodDeclaration
                    | interfaceEventDeclaration
                    | new Seq(type,
                        new If(memberName - "(", interfaceMethodDeclaration)
                        | new If(memberName - "{", interfacePropertyDeclaration)
                        | interfaceIndexerDeclaration)
                    | ".INTERFACEBODY")
                ));

            parser.Add(new Rule("interfacePropertyDeclaration",
                NAME - "{" - interfaceAccessorDeclarations - "}"
                )
            { semantics = SemanticFlags.InterfacePropertyDeclaration });

            parser.Add(new Rule("interfaceMethodDeclaration",
                NAME - new Opt(typeParameterList) - "(" - new Opt(formalParameterList) - ")"
                    - new Opt(typeParameterConstraintsClauses) - ";"
                )
            { semantics = SemanticFlags.MethodDeclarationScope | SemanticFlags.InterfaceMethodDeclaration });

            parser.Add(new Rule("interfaceEventDeclaration",
                new Seq("event", typeName, NAME, ";")
                )
            { semantics = SemanticFlags.InterfaceEventDeclaration });

            parser.Add(new Rule("interfaceIndexerDeclaration",
                new Seq("this", "[", formalParameterList, "]", "{", interfaceAccessorDeclarations, "}")
                )
            { semantics = SemanticFlags.InterfaceIndexerDeclaration });

            parser.Add(new Rule("interfaceAccessorDeclarations",
                new Seq(attributes,
                    new If(s => s.Current.text == "get", interfaceGetAccessorDeclaration - new Opt(attributes - interfaceSetAccessorDeclaration))
                    | interfaceSetAccessorDeclaration - new Opt(attributes - interfaceGetAccessorDeclaration))
                ));

            parser.Add(new Rule("interfaceGetAccessorDeclaration",
                GET - ";"
                )
            { semantics = SemanticFlags.InterfaceGetAccessorDeclaration });

            parser.Add(new Rule("interfaceSetAccessorDeclaration",
                SET - ";"
                )
            { semantics = SemanticFlags.InterfaceSetAccessorDeclaration });

            var enumBase = new Id("enumBase");
            var enumBody = new Id("enumBody");
            var enumMemberDeclarations = new Id("enumMemberDeclarations");
            var enumMemberDeclaration = new Id("enumMemberDeclaration");

            parser.Add(new Rule("enumDeclaration",
                "enum" - NAME - new Opt(enumBase) - enumBody - new Opt(";")
                )
            { semantics = SemanticFlags.EnumDeclaration | SemanticFlags.TypeDeclarationScope });

            parser.Add(new Rule("enumBase",
                new Seq(":", integralType)
                )
            { semantics = SemanticFlags.EnumBaseScope });

            parser.Add(new Rule("enumBody",
                "{" - new Opt(enumMemberDeclarations) - "}"
                )
            { semantics = SemanticFlags.EnumBodyScope });

            parser.Add(new Rule("enumMemberDeclarations",
                new Seq(
                    enumMemberDeclaration,
                    new Many(new IfNot(new Seq(",", "}") | "}", "," - enumMemberDeclaration)), new Opt(","))
                ));

            parser.Add(new Rule("enumMemberDeclaration",
                attributes - NAME - new Opt("=" - constantExpression)
                )
            { semantics = SemanticFlags.EnumMemberDeclaration });

            parser.Add(new Rule("delegateDeclaration",
                "delegate" - ("void" | type) - NAME - new Opt(typeParameterList)
                    - "(" - new Opt(formalParameterList) - ")" - new Opt(typeParameterConstraintsClauses) - ";"
                )
            { semantics = SemanticFlags.DelegateDeclaration | SemanticFlags.TypeDeclarationScope });

            var attributeTargetSpecifier = new Id("attributeTargetSpecifier");
            var ATTRIBUTETARGET = new Id("ATTRIBUTETARGET");
            parser.Add(new Rule("ATTRIBUTETARGET",
                (IDENTIFIER | "event:" | "return:" | "field:" | "method:" | "param:" | "property:" | "type:" | "assembly:" | "module:")
                )
            { contextualKeyword = true });

            parser.Add(new Rule("attributeTargetSpecifier",
                (new If(s => s.Current.text == "field"
                    || s.Current.text == "method"
                    || s.Current.text == "param"
                    || s.Current.text == "property"
                    || s.Current.text == "type"
                    || s.Current.text == "assembly"
                    || s.Current.text == "module",
                        ATTRIBUTETARGET) | "event" | "return") - ":"
                ));

            parser.Add(new Rule("attributes",
                new Many(
                    "[" - new If(attributeTargetSpecifier, attributeTargetSpecifier) - attribute -
                        new Many("," - attribute) - "]"
                )
                )
            { semantics = SemanticFlags.AttributesScope });

            parser.Add(new Rule("attribute",
                typeName - new Opt(attributeArguments)
                | ".ATTRIBUTE"
                ));

            var rankSpecifiers = new Id("rankSpecifiers");

            parser.Add(new Rule("type",
                predefinedType - new Opt("?") - new Opt(rankSpecifiers)
                | typeName - new Opt("?") - new Opt(rankSpecifiers)
                ));

            parser.Add(new Rule("type2",
                predefinedType - new Opt(rankSpecifiers)
                | typeName - new Opt(rankSpecifiers)
                ));

            //parser.Add(new Rule("typeSpecifier",
            //    predefinedType - new Opt("?") - new Opt(rankSpecifiers)
            //    | typeName - new Opt("?") - new Opt(rankSpecifiers)
            //    ));

            parser.Add(new Rule("exceptionClassType",
                typeName | "object" | "string"
                ));

            parser.Add(new Rule("nonArrayType",
                (typeName | simpleType) - new Opt("?")
                | "object"
                | "string"
                ));

            //	parser.Add(new Rule("arrayType",
            //		nonArrayType - rankSpecifiers
            //		));

            parser.Add(new Rule("simpleType",
                numericType | "bool"
                ));

            parser.Add(new Rule("numericType",
                integralType | floatingPointType | "decimal"
                ));

            parser.Add(new Rule("integralType",
                new Lit("sbyte") | "byte" | "short" | "ushort" | "int" | "uint" | "long" | "ulong" | "char"
                ));

            parser.Add(new Rule("floatingPointType",
                new Lit("float") | "double"
                ));

            parser.Add(new Rule("typeName",
                namespaceOrTypeName
                ));

            parser.Add(new Rule("globalNamespace",
                IDENTIFIER | "global"
                ));

            parser.Add(new Rule("namespaceOrTypeName",
                new If(IDENTIFIER - "::", globalNamespace - "::") -
                typeOrGeneric - new Many("." - typeOrGeneric)
                ));

            parser.Add(new Rule("typeOrGeneric",
                IDENTIFIER - new If("<" - type, typeArgumentList) - new If("<" - (new Lit(",") | ">"), unboundTypeRank)
                //				new If(new Seq(IDENTIFIER, typeArgumentList), new Seq(IDENTIFIER, typeArgumentList))
                //				| IDENTIFIER
                ));

            parser.Add(new Rule("typeArgumentList",
                "<" - type - new Many("," - type) - ">"
                ));

            parser.Add(new Rule("unboundTypeRank",
                "<" - new Many(",") - ">"
                ));

            //parser.Add(new Rule("typeArguments",
            //    type - new Many("," - type)
            //    ));

            parser.Add(new Rule("rankSpecifier",
                "[" - new Many(",") - "]"
                ));

            var unaryExpression = new Id("unaryExpression");
            var assignmentOperator = new Id("assignmentOperator");
            var assignment = new Id("assignment");
            var nonAssignmentExpression = new Id("nonAssignmentExpression");
            var castExpression = new Id("castExpression");

            parser.Add(new Rule("expression",
                new If(unaryExpression - assignmentOperator, assignment)
                | nonAssignmentExpression
                ));

            parser.Add(new Rule("assignment",
                unaryExpression - assignmentOperator - (expression | ".EXPECTEDTYPE")
                ));

            var preIncrementExpression = new Id("preIncrementExpression");
            var preDecrementExpression = new Id("preDecrementExpression");

            parser.Add(new Rule("unaryExpression",
                new If("(" - type - ")" - IDENTIFIER, castExpression)
                | new If(castExpression, castExpression)
                | primaryExpression - new Many(new Lit("++") | "--") // TODO: Fix this! Post increment operators should be primaryExpressionPart
                | new Seq(new Lit("+") | "-" | "!", unaryExpression)
                | new Seq("~", unaryExpression | ".EXPECTEDTYPE")
                | preIncrementExpression
                | preDecrementExpression
                ));

            parser.Add(new Rule("castExpression",
                "(" - type - ")" - unaryExpression
                ));

            parser.Add(new Rule("assignmentOperator",
                new Lit("=") | "+=" | "-=" | "*=" | "/=" | "%=" | "&=" | "|=" | "^=" | "<<=" | new Seq(">", ">=")
                ));

            parser.Add(new Rule("preIncrementExpression",
                "++" - unaryExpression
                ));

            parser.Add(new Rule("preDecrementExpression",
                "--" - unaryExpression
                ));

            var brackets = new Id("brackets");
            var primaryExpressionStart = new Id("primaryExpressionStart");
            var primaryExpressionPart = new Id("primaryExpressionPart");
            var objectCreationExpression = new Id("objectCreationExpression");
            //var delegateCreationExpression = new Id("delegateCreationExpression");
            var anonymousObjectCreationExpression = new Id("anonymousObjectCreationExpression");
            var sizeofExpression = new Id("sizeofExpression");
            var checkedExpression = new Id("checkedExpression");
            var uncheckedExpression = new Id("uncheckedExpression");
            var defaultValueExpression = new Id("defaultValueExpression");
            var anonymousMethodExpression = new Id("anonymousMethodExpression");

            //*
            parser.Add(new Rule("primaryExpression",
                primaryExpressionStart - new Many(primaryExpressionPart)
                | new Seq("new",
                    ((nonArrayType | ".EXPECTEDTYPE") - (objectCreationExpression | arrayCreationExpression))
                    | implicitArrayCreationExpression
                    | anonymousObjectCreationExpression,
                    new Many(primaryExpressionPart))
                | anonymousMethodExpression
                ));

            var parenExpression = new Id("parenExpression");
            var typeofExpression = new Id("typeofExpression");
            var qidStart = new Id("qidStart");
            var qidPart = new Id("qidPart");
            var accessIdentifier = new Id("accessIdentifier");

            parser.Add(new Rule("typeofExpression",
                new Lit("typeof") - "(" - (type | "void") - ")"
                ));

            parser.Add(new Rule("predefinedType",
                new Lit("bool") | "byte" | "char" | "decimal" | "double" | "float" | "int" | "long"
                | "object" | "sbyte" | "short" | "string" | "uint" | "ulong" | "ushort"
                ));

            parser.Add(new Rule("qid",
                qidStart - new Many(qidPart)
                ));

            parser.Add(new Rule("qidStart",
                predefinedType
                | new If(IDENTIFIER - "<", NAME - typeParameterList)
                | IDENTIFIER - new Opt("::" - IDENTIFIER)
                ));

            parser.Add(new Rule("qidPart",
                new If("." - IDENTIFIER, accessIdentifier) | "."
                ));

            parser.Add(new Rule("primaryExpressionStart",
                predefinedType
                | LITERAL | "true" | "false"
                | new If(IDENTIFIER - typeArgumentList /*- (new Lit("(") | ")" | ":" | ";" | "," | "." | "?" | "==" | "!="*/, IDENTIFIER - typeArgumentList)
                | new If(IDENTIFIER - "::", globalNamespace - "::") - IDENTIFIER
                | parenExpression
                | "this"
                | "base"
                | typeofExpression
                | sizeofExpression
                | checkedExpression
                | uncheckedExpression
                | defaultValueExpression
                ));

            //var bracketsOrArguments = new Id("bracketsOrArguments");
            var argument = new Id("argument");
            var attributeArgument = new Id("attributeArgument");
            var expressionList = new Id("expressionList");
            var argumentValue = new Id("argumentValue");
            var argumentName = new Id("argumentName");
            var attributeMemberName = new Id("attributeMemberName");
            var variableReference = new Id("variableReference");

            parser.Add(new Rule("primaryExpressionPart",
                accessIdentifier
                | brackets
                | arguments
                ));

            parser.Add(new Rule("accessIdentifier",
                "." - IDENTIFIER - new If(typeArgumentList, typeArgumentList)
                ));

            //parser.Add(new Rule("bracketsOrArguments",
            //    brackets | arguments
            //    ));

            parser.Add(new Rule("brackets",
                "[" - new Opt(expressionList) - "]"
                ));

            parser.Add(new Rule("expressionList",
                expression - new Many("," - expression)
                ));

            parser.Add(new Rule("parenExpression",
                "(" - expression - ")"
                ));

            parser.Add(new Rule("arguments",
                "(" - argumentList - ")"
                ));

            parser.Add(new Rule("attributeArguments",
                "(" - attributeArgumentList - ")"
            ));

            parser.Add(new Rule("argumentList",
                new Opt(argument - new Many("," - argument))
                )
            { semantics = SemanticFlags.ArgumentListScope });

            parser.Add(new Rule("attributeArgumentList",
                new Opt(attributeArgument - new Many("," - attributeArgument))
                )
            { semantics = SemanticFlags.AttributeArgumentsScope });

            parser.Add(new Rule("argument",
                new If(IDENTIFIER - ":", argumentName - argumentValue)
                | argumentValue
                ));

            parser.Add(new Rule("attributeArgument",
                new If(IDENTIFIER - "=", attributeMemberName - argumentValue)
                | new If(IDENTIFIER - ":", argumentName - argumentValue)
                | argumentValue
                ));

            parser.Add(new Rule("argumentName",
                IDENTIFIER - ":"
                ));

            parser.Add(new Rule("attributeMemberName",
                IDENTIFIER - "="
                ));

            parser.Add(new Rule("argumentValue",
                expression
                | new Seq(new Lit("out") | "ref", variableReference)
                | ".EXPECTEDTYPE"
                ));

            parser.Add(new Rule("variableReference",
                expression
                ));

            parser.Add(new Rule("rankSpecifiers",
                "[" - new Many(",") - "]" - new Many("[" - new Many(",") - "]")
                ));

            //parser.Add(new Rule("delegateCreationExpression",
            //    new Seq( typeName, "(", typeName, ")" )
            //    ));

            var anonymousObjectInitializer = new Id("anonymousObjectInitializer");
            var memberDeclaratorList = new Id("memberDeclaratorList");
            var memberDeclarator = new Id("memberDeclarator");
            var memberAccessExpression = new Id("memberAccessExpression");

            parser.Add(new Rule("anonymousObjectCreationExpression",
                anonymousObjectInitializer
                )
            { semantics = SemanticFlags.AnonymousObjectCreation });

            parser.Add(new Rule("anonymousObjectInitializer",
                "{" - new Opt(memberDeclaratorList) - new Opt(",") - "}"
                ));

            parser.Add(new Rule("memberDeclaratorList",
                memberDeclarator - new Many(new If("," - IDENTIFIER, "," - memberDeclarator))
                ));

            parser.Add(new Rule("memberDeclarator",
                new If(IDENTIFIER - "=", IDENTIFIER - "=" - expression)
                | memberAccessExpression
                )
            { semantics = SemanticFlags.MemberDeclarator });

            parser.Add(new Rule("memberAccessExpression",
                expression
                ));

            //parser.Add(new Rule("primaryOrArrayCreationExpression",
            //    new If( /*arrayCreationExpression*/ "new" - new Opt(nonArrayType) - "[", arrayCreationExpression )
            //    | primaryExpression
            //    ));

            var objectOrCollectionInitializer = new Id("objectOrCollectionInitializer");
            var objectInitializer = new Id("objectInitializer");
            var collectionInitializer = new Id("collectionInitializer");
            var elementInitializerList = new Id("elementInitializerList");
            var elementInitializer = new Id("elementInitializer");
            var memberInitializerList = new Id("memberInitializerList");
            var memberInitializer = new Id("memberInitializer");

            parser.Add(new Rule("objectCreationExpression",
                arguments - new Opt(objectOrCollectionInitializer)
                | objectOrCollectionInitializer
                ));

            parser.Add(new Rule("objectOrCollectionInitializer",
                "{" - ((new If(IDENTIFIER - "=", objectInitializer) | "}" | collectionInitializer))
                ));

            parser.Add(new Rule("collectionInitializer",
                elementInitializerList - new Opt(",") - "}"
                ));

            parser.Add(new Rule("elementInitializerList",
                elementInitializer - new Many(new IfNot(new Opt(",") - "}", "," - elementInitializer))
                ));

            parser.Add(new Rule("elementInitializer",
                nonAssignmentExpression
                | "{" - expressionList - "}"
                | ".EXPECTEDTYPE"
                ));

            parser.Add(new Rule("objectInitializer",
                new Opt(memberInitializerList) - new Opt(",") - (new Lit("}") | ".MEMBERINITIALIZER")
                ));

            parser.Add(new Rule("memberInitializerList",
                memberInitializer - new Many(new IfNot(new Opt(",") - "}", "," - memberInitializer))
                )
            { semantics = SemanticFlags.MemberInitializerScope });

            parser.Add(new Rule("memberInitializer",
                (IDENTIFIER | ".MEMBERINITIALIZER") - "=" - (expression | objectOrCollectionInitializer | ".EXPECTEDTYPE")
                ));

            //var invocationPart = new Id("invocationPart");
            var explicitAnonymousFunctionParameterList = new Id("explicitAnonymousFunctionParameterList");
            var explicitAnonymousFunctionParameter = new Id("explicitAnonymousFunctionParameter");
            var anonymousFunctionParameterModifier = new Id("anonymousFunctionParameterModifier");
            var explicitAnonymousFunctionSignature = new Id("explicitAnonymousFunctionSignature");

            parser.Add(new Rule("arrayCreationExpression",
                new If(new Seq("[", new Lit(",") | "]"), rankSpecifiers - arrayInitializer)
                | "[" - expressionList - "]" - new Opt(rankSpecifiers) - new Opt(arrayInitializer)
                ));

            parser.Add(new Rule("implicitArrayCreationExpression",
                rankSpecifier - arrayInitializer
                ));

            //parser.Add(new Rule("invocationPart",
            //    accessIdentifier | brackets
            //    ));

            parser.Add(new Rule("arrayInitializer",
                "{" - new Opt(variableInitializerList) - "}"
                ));

            parser.Add(new Rule("variableInitializerList",
                variableInitializer - new Many(new IfNot(new Seq(",", "}"), "," - new Opt(variableInitializer))) - new Opt(",")
                ));

            parser.Add(new Rule("sizeofExpression",
                new Lit("sizeof") - "(" - simpleType - ")"
                ));

            parser.Add(new Rule("checkedExpression",
                new Lit("checked") - "(" - expression - ")"
            ));

            parser.Add(new Rule("uncheckedExpression",
                new Lit("unchecked") - "(" - expression - ")"
            ));

            parser.Add(new Rule("defaultValueExpression",
                new Seq("default", "(", type, ")")
                ));

            var anonymousFunctionSignature = new Id("anonymousFunctionSignature");
            var lambdaExpression = new Id("lambdaExpression");
            var queryExpression = new Id("queryExpression");
            var conditionalExpression = new Id("conditionalExpression");
            var lambdaExpressionBody = new Id("lambdaExpressionBody");
            var anonymousMethodBody = new Id("anonymousMethodBody");
            var implicitAnonymousFunctionParameterList = new Id("implicitAnonymousFunctionParameterList");
            var implicitAnonymousFunctionParameter = new Id("implicitAnonymousFunctionParameter");

            var FROM = new Id("FROM"); parser.Add(new Rule("FROM", IDENTIFIER | "from") { contextualKeyword = true });
            var SELECT = new Id("SELECT"); parser.Add(new Rule("SELECT", IDENTIFIER) { contextualKeyword = true });
            var GROUP = new Id("GROUP"); parser.Add(new Rule("GROUP", IDENTIFIER | "group") { contextualKeyword = true });
            var INTO = new Id("INTO"); parser.Add(new Rule("INTO", new If(s => s.Current.text == "into", IDENTIFIER | "into")) { contextualKeyword = true });
            var ORDERBY = new Id("ORDERBY"); parser.Add(new Rule("ORDERBY", new If(s => s.Current.text == "orderby", IDENTIFIER | "orderby")) { contextualKeyword = true });
            var JOIN = new Id("JOIN"); parser.Add(new Rule("JOIN", new If(s => s.Current.text == "join", IDENTIFIER | "join")) { contextualKeyword = true });
            var LET = new Id("LET"); parser.Add(new Rule("LET", new If(s => s.Current.text == "let", IDENTIFIER | "let")) { contextualKeyword = true });
            var ON = new Id("ON"); parser.Add(new Rule("ON", new If(s => s.Current.text == "on", IDENTIFIER | "on")) { contextualKeyword = true });
            var EQUALS = new Id("EQUALS"); parser.Add(new Rule("EQUALS", new If(s => s.Current.text == "equals", IDENTIFIER | "equals")) { contextualKeyword = true });
            var BY = new Id("BY"); parser.Add(new Rule("BY", new If(s => s.Current.text == "by", IDENTIFIER | "by")) { contextualKeyword = true });
            var ASCENDING = new Id("ASCENDING"); parser.Add(new Rule("ASCENDING", IDENTIFIER | "ascending") { contextualKeyword = true });
            var DESCENDING = new Id("DESCENDING"); parser.Add(new Rule("DESCENDING", IDENTIFIER | "descending") { contextualKeyword = true });

            var fromClause = new Id("fromClause");

            parser.Add(new Rule("nonAssignmentExpression",
                new If(anonymousFunctionSignature - "=>", lambdaExpression)
                | new If(IDENTIFIER - IDENTIFIER - "in", queryExpression)
                | new If(IDENTIFIER - type - IDENTIFIER - "in", queryExpression)
                | conditionalExpression
                ));

            parser.Add(new Rule("lambdaExpression",
                anonymousFunctionSignature - "=>" - lambdaExpressionBody
                )
            { semantics = SemanticFlags.LambdaExpressionScope | SemanticFlags.LambdaExpressionDeclaration });

            parser.Add(new Rule("anonymousFunctionSignature",
                new Seq(
                    "(",
                    new Opt(
                        new If(new Seq(IDENTIFIER, new Lit(",") | ")"), implicitAnonymousFunctionParameterList)
                        | explicitAnonymousFunctionParameterList),
                    ")")
                | implicitAnonymousFunctionParameter
                ));// { semantics = SemanticFlags.LambdaExpressionDeclaration });

            parser.Add(new Rule("implicitAnonymousFunctionParameterList",
                implicitAnonymousFunctionParameter - new Many("," - implicitAnonymousFunctionParameter)
                ));

            parser.Add(new Rule("implicitAnonymousFunctionParameter",
                IDENTIFIER
                )
            { semantics = SemanticFlags.ImplicitParameterDeclaration });

            parser.Add(new Rule("lambdaExpressionBody",
                expression
                | "{" - statementList - "}"
                )
            { semantics = SemanticFlags.LambdaExpressionBodyScope });

            parser.Add(new Rule("anonymousMethodExpression",
                "delegate" - new Opt(explicitAnonymousFunctionSignature) - anonymousMethodBody
                )
            { semantics = SemanticFlags.AnonymousMethodDeclaration | SemanticFlags.AnonymousMethodScope });

            parser.Add(new Rule("anonymousMethodBody",
                "{" - statementList - "}"
                )
            { semantics = SemanticFlags.AnonymousMethodBodyScope });

            parser.Add(new Rule("explicitAnonymousFunctionSignature",
                "(" - new Opt(explicitAnonymousFunctionParameterList) - ")"
                )
            { semantics = SemanticFlags.FormalParameterListScope });

            parser.Add(new Rule("explicitAnonymousFunctionParameterList",
                explicitAnonymousFunctionParameter - new Many("," - explicitAnonymousFunctionParameter)
                ));

            parser.Add(new Rule("explicitAnonymousFunctionParameter",
                new Opt(anonymousFunctionParameterModifier) - type - NAME
                )
            { semantics = SemanticFlags.ExplicitParameterDeclaration });

            parser.Add(new Rule("anonymousFunctionParameterModifier",
                new Lit("ref") | "out"
                ));

            var queryBody = new Id("queryBody");
            var queryBodyClause = new Id("queryBodyClause");
            var queryContinuation = new Id("queryContinuation");
            var letClause = new Id("letClause");
            var whereClause = new Id("whereClause");
            var joinClause = new Id("joinClause");
            var orderbyClause = new Id("orderbyClause");
            var orderingList = new Id("orderingList");
            var ordering = new Id("ordering");
            var selectClause = new Id("selectClause");
            var groupClause = new Id("groupClause");

            parser.Add(new Rule("queryExpression",
                fromClause - queryBody
                )
            { semantics = SemanticFlags.QueryExpressionScope });

            parser.Add(new Rule("fromClause",
                FROM - (new If(NAME - "in", NAME) | type - NAME) - "in" - expression
                )
            { semantics = SemanticFlags.FromClauseVariableDeclaration });

            parser.Add(new Rule("queryBody",
                new Many(
                    new If(s => s.Current.text == "from"
                        | s.Current.text == "let"
                        | s.Current.text == "join"
                        | s.Current.text == "orderby"
                        | s.Current.text == "where", queryBodyClause)
                ) - (new If(s => s.Current.text == "select", selectClause) | "select" | groupClause) - new Opt(queryContinuation)
                )
            { semantics = SemanticFlags.QueryBodyScope });

            parser.Add(new Rule("queryContinuation",
                INTO - IDENTIFIER - queryBody
                ));

            parser.Add(new Rule("queryBodyClause",
                new If(s => s.Current.text == "from", fromClause)
                | new If(s => s.Current.text == "let", letClause)
                | new If(s => s.Current.text == "join", joinClause)
                | new If(s => s.Current.text == "orderby", orderbyClause)
                | whereClause
                ));

            parser.Add(new Rule("joinClause",
                JOIN - new Opt(type) - IDENTIFIER - "in" - expression - ON - expression - EQUALS - expression - new Opt(INTO - IDENTIFIER)
                ));

            parser.Add(new Rule("letClause",
                LET - IDENTIFIER - "=" - expression
                ));

            parser.Add(new Rule("orderbyClause",
                ORDERBY - orderingList
                ));

            parser.Add(new Rule("orderingList",
                ordering - new Many("," - ordering)
                ));

            parser.Add(new Rule("ordering",
                expression - new Opt(new If(s => s.Current.text == "ascending", ASCENDING) | DESCENDING)
                ));

            parser.Add(new Rule("selectClause",
                SELECT - expression
                ));

            parser.Add(new Rule("groupClause",
                GROUP - expression - BY - expression
                ));

            parser.Add(new Rule("whereClause",
                WHERE - booleanExpression
                ));

            parser.Add(new Rule("booleanExpression",
                expression
                ));

            var nullCoalescingExpression = new Id("nullCoalescingExpression");
            var conditionalOrExpression = new Id("conditionalOrExpression");
            var conditionalAndExpression = new Id("conditionalAndExpression");
            var inclusiveOrExpression = new Id("inclusiveOrExpression");
            var exclusiveOrExpression = new Id("exclusiveOrExpression");
            var andExpression = new Id("andExpression");
            var equalityExpression = new Id("equalityExpression");
            var relationalExpression = new Id("relationalExpression");
            var shiftExpression = new Id("shiftExpression");
            var additiveExpression = new Id("additiveExpression");
            var multiplicativeExpression = new Id("multiplicativeExpression");

            parser.Add(new Rule("conditionalExpression",
                nullCoalescingExpression - new Opt("?" - expression - ":" - expression)
                )
            { autoExclude = true });

            parser.Add(new Rule("nullCoalescingExpression",
                conditionalOrExpression - new Many("??" - conditionalOrExpression)
                )
            { autoExclude = true });

            parser.Add(new Rule("conditionalOrExpression",
                conditionalAndExpression - new Many("||" - conditionalAndExpression)
                )
            { autoExclude = true });

            parser.Add(new Rule("conditionalAndExpression",
                inclusiveOrExpression - new Many("&&" - inclusiveOrExpression)
                )
            { autoExclude = true });

            parser.Add(new Rule("inclusiveOrExpression",
                exclusiveOrExpression - new Many("|" - (exclusiveOrExpression | ".EXPECTEDTYPE"))
                )
            { autoExclude = true });

            parser.Add(new Rule("exclusiveOrExpression",
                andExpression - new Many("^" - andExpression)
                )
            { autoExclude = true });

            parser.Add(new Rule("andExpression",
                equalityExpression - new Many("&" - (equalityExpression | ".EXPECTEDTYPE"))
                )
            { autoExclude = true });

            parser.Add(new Rule("equalityExpression",
                relationalExpression - new Many((new Lit("==") | "!=") - (relationalExpression | ".EXPECTEDTYPE"))
                )
            { autoExclude = true });

            parser.Add(new Rule("relationalExpression",
                new Seq(shiftExpression, new Many(
                    new Seq(new Lit("<") | ">" | "<=" | ">=", (shiftExpression | ".EXPECTEDTYPE"))
                    | new Seq(new Lit("is") | "as", new If(type2 - "?" - expression - ":", type2) | type)))
                )
            { autoExclude = true });

            parser.Add(new Rule("shiftExpression",
                new Seq(additiveExpression,
                    new Many(new If(new Seq(">", ">") | "<<", new Seq(new Seq(">", ">") | "<<", additiveExpression))))
                )
            { autoExclude = true });

            parser.Add(new Rule("additiveExpression",
                new Seq(multiplicativeExpression, new Many(new Seq(new Lit("+") | "-", multiplicativeExpression)))
                )
            { autoExclude = true });

            parser.Add(new Rule("multiplicativeExpression",
                new Seq(unaryExpression, new Many(new Seq(new Lit("*") | "/" | "%", unaryExpression)))
                )
            { autoExclude = true });

            Rule.debug = false;

            parser.InitializeGrammar();
            InitializeTokenCategories();

            //Debug.Log(parser);
        }

        public class Scanner : IScanner
		{
			public void MoveAfterLeaf(ParseTree.Leaf leaf)
			{
			}


			public IScanner Clone()
			{
				return null;
			}
			
			public bool Lookahead(Node node, int maxDistance = int.MaxValue)
			{
				return false;
			}
			public SyntaxToken Lookahead(int offset, bool skipWhitespace = true)
			{
				return null;
			}
			
			public SyntaxToken CurrentToken()
			{
				return null;
			}
			public int CurrentLine()
			{
				return 0;
			}
			public int CurrentTokenIndex()
			{
				return 0;
			}
			
			public CsGrammar.Node CurrentGrammarNode { get; set; }
			public ParseTree.Node CurrentParseTreeNode { get; set; }
			
			public ParseTree.Leaf ErrorToken { get; set; }
			public string ErrorMessage { get; set; }
			public CsGrammar.Node ErrorGrammarNode { get; set; }
			public ParseTree.Node ErrorParseTreeNode { get; set; }
			
			public bool KeepScanning { get; set; }
			
			public bool Seeking { get; set; }
			
			public void InsertMissingToken(string errorMessage)
			{
			}
			
			public bool CollectCompletions(TokenSet tokenSet)
			{
				return false;
			}
			
			public void OnReduceSemanticNode(ParseTree.Node node)
			{
			}
			
			public void SyntaxErrorExpected(TokenSet lookahead)
			{
			}

			public SyntaxToken Current 
			{
				get;
				set;
			}

			object System.Collections.IEnumerator.Current
			{
				get { return Current; }
			}
			public bool MoveNext()
			{
				return false;
			}

			public void Reset()
			{
			}

			public void Dispose()
			{
			}
		}
		public abstract Parser GetParser { get; }
		
		public abstract IdentifierCompletionsType GetCompletionTypes(ParseTree.BaseNode afterNode);
		
		public interface IScanner : IEnumerator<SyntaxToken>
		{
			IScanner Clone();
			
			bool Lookahead(Node node, int maxDistance = int.MaxValue);
			SyntaxToken Lookahead(int offset, bool skipWhitespace = true);
			
			SyntaxToken CurrentToken();
			int CurrentLine();
			int CurrentTokenIndex();
			
			CsGrammar.Node CurrentGrammarNode { get; set; }
			ParseTree.Node CurrentParseTreeNode { get; set; }
			
			ParseTree.Leaf ErrorToken { get; set; }
			string ErrorMessage { get; set; }
			CsGrammar.Node ErrorGrammarNode { get; set; }
			ParseTree.Node ErrorParseTreeNode { get; set; }
			
			bool KeepScanning { get; }
			
			bool Seeking { get; }
			
			void InsertMissingToken(string errorMessage);
			
			bool CollectCompletions(TokenSet tokenSet);
			
			void OnReduceSemanticNode(ParseTree.Node node);
			
			void SyntaxErrorExpected(TokenSet lookahead);
		}
		
		public abstract class Node
		{
			public Node parent;
			public int childIndex;
			
			public TokenSet lookahead;
			public TokenSet follow;
			
			public static implicit operator Node(string s)
			{
				return new Lit(s);
			}
			
			public static Node operator | (Node a, Node b)
			{
				return new Alt(a, b);
			}
			
			public static Node operator | (Alt a, Node b)
			{
				a.Add(b);
				return a;
			}
			
			public static Node operator - (Node a, Node b)
			{
				return new Seq(a, b);
			}
			
			public static Node operator - (Seq a, Node b)
			{
				a.Add(b);
				return a;
			}
			
			//public static implicit operator Predicate<IScanner> (Node node)
			//{
			//    return (IScanner scanner) =>
			//        {
			//            try
			//            {
			//                node.Parse(scanner.Clone(), new GoalAdapter());
			//            }
			//            catch
			//            {
			//                return false;
			//            }
			//            return true;
			//        };
			//}
			
			public virtual Node GetNode()
			{
				return this;
			}
			
			public virtual void Add(Node node)
			{
				throw new Exception(GetType() + ": cannot Add()");
			}
			
			//public virtual TokenSet GetLookahead()
			//{
			//    return lookahead;
			//}
			
			public virtual bool Matches(IScanner scanner)
			{
				return lookahead.Matches(scanner.Current);
			}
			
			public abstract TokenSet SetLookahead(Parser parser);
			
			public abstract TokenSet SetFollow(Parser parser, TokenSet succ);
			
			public virtual void CheckLL1(Parser parser)
			{
				if (follow == null)
					throw new Exception(this + ": follow not set");
				if (lookahead.MatchesEmpty() && lookahead.Accepts(follow))
					throw new Exception(this + ": ambiguous\n"
					                    + "  lookahead " + lookahead.ToString(parser) + "\n"
					                    + "  follow " + follow.ToString(parser));
			}
			
			public abstract bool Scan(IScanner scanner);
			
			public void SyntaxError(IScanner scanner, string errorMessage)
			{
				if (scanner.ErrorMessage != null)
					return;
				//Debug.LogError(errorMessage);
				scanner.ErrorMessage = errorMessage;
			}
			
			public virtual IEnumerable<Lit> EnumerateLitNodes() { yield break; }
			
			public virtual IEnumerable<Id> EnumerateIdNodes() { yield break; }
			
			public virtual IEnumerable<T> EnumerateNodesOfType<T>() where T : Node
			{
				if (this is T)
					yield return (T)this;
			}
			
			public abstract Node Parse(IScanner scanner);
			
			public Node Recover(IScanner scanner, out int numMissing)
			{
				numMissing = 0;
				
				var current = this;
				while (current.parent != null)
				{
					var next = current.parent.NextAfterChild(current, scanner);
					if (next == null)
						break;
					
					var nextId = next as Id;
					if (nextId != null && nextId.GetName() == "attribute")
						return nextId;
					
					var nextMatchesScanner = next.Matches(scanner);
					while (next != null && !nextMatchesScanner && next.lookahead.MatchesEmpty())
					{
						next = next.parent.NextAfterChild(next, scanner);
						nextMatchesScanner = next != null && next.Matches(scanner);
					}
					
					if (nextMatchesScanner && scanner.Current.text == ";" && next is Opt)
					{
						return null;
					}
					
					//if (next is Many)
					//{
					//    var currentToken = scanner.Current;
					//    var n = 0;
					//    var recoverOnMany = Recover(scanner, out n);
					//    if (recoverOnMany != null && currentToken != scanner.Current)
					//    {
					//        next = recoverOnMany;
					//    }
					//    else
					//    {
					//        next = next.parent.NextAfterChild(next, scanner);
					//    }
					//}
					//if (next == null)
					//    break;
					
					++numMissing;
					if (nextMatchesScanner)
					{
						//if (scanner.Current.text == ";" && next is Opt)
						//{
						//	Debug.Log(next);
						//	return null;
						//}
						
						//var clone = scanner.Clone();
						if (scanner.Current.text == "{" ||
						    scanner.Current.text == "}" ||
						    scanner.Lookahead(next, 3))//next.Scan(clone))
						{
							return next;
						}
						//					else
						//					{
						//						nextMatchesScanner = false;
						//					}
					}
					
					if (numMissing <= 1 && scanner.Current.text != "{" && scanner.Current.text != "}")
						using (var laScanner = scanner.Clone())
							if (//laScanner.CurrentLine() == scanner.CurrentLine() &&
							    laScanner.MoveNext() && next.Matches(laScanner))
					{
						if (laScanner.Lookahead(next, 3))//.Scan(laScanner))
							return null;
					}
					
					current = next;
				}
				return null;
			}
			
			public virtual Node NextAfterChild(Node child, IScanner scanner)
			{
				return parent != null ? parent.NextAfterChild(this, scanner) : null;
			}
			
			public bool CollectCompletions(TokenSet tokenSet, IScanner scanner, int identifierId)
			{
				var clone = scanner.Clone();
				
				//	string debug = null;
				
				//	var idTypes = IdentifierCompletionsType.None;
				var hasName = false;
				var current = this;
				while (current != null && current.parent != null)
				{
					//if (clone.CurrentParseTreeNode.RuleName != debug)
					//{
					//    debug = clone.CurrentParseTreeNode.RuleName;
					//    BitArray set;
					//    current.lookahead.GetDataSet(out set);
					////	UnityEngine.Debug.Log(debug + " LA:" + (set != null ? set.Count : 1));
					//}
					
					tokenSet.Add(current.lookahead);
					
					if (current.lookahead.Matches(identifierId))
					{
						Rule currentRule = null;
						var currentId = current as Id;
						if (currentId == null)
						{
							var rule = current.parent;
							while (rule != null && !(rule is Rule))
								rule = rule.parent;
							currentId = rule != null ? rule.parent as Id : null;
							currentRule = rule as Rule;
						}
						
						//	UnityEngine.Debug.Log("IDENTIFIER in " + (currentId ?? current));
						
						if (currentId != null)
						{
							var peerAsRule = currentId.peer as Rule;
							if (peerAsRule != null && peerAsRule.contextualKeyword)
							{
								Debug.Log(currentId.GetName());
							}
							else if (currentRule != null && currentRule.contextualKeyword)
							{
								Debug.Log("currentRule " + currentRule.GetNt());
							}
							else
							{
								var id = currentId.GetName();
								if (Array.IndexOf(new []
								                  {
									"constantDeclarators",
									"constantDeclarator",
								}, id) >= 0)
								{
									hasName = true;
								}
							}
						}
						
						//    idTypes |= clone.GetCompletionIdentifierType();
					}
					
					if (!current.lookahead.MatchesEmpty())
						break;
					
					current = current.parent.NextAfterChild(current, clone);
				}
				tokenSet.RemoveEmpty();
				
				//Debug.Log(tokenSet.ToString(FlipbookGames.ScriptInspector2.CsGrammar.Instance.GetParser));
				
				//	UnityEngine.Debug.Log("Current node " + current.ToString());
				return hasName;
			}
		}
		
		public class NameId : Id
		{
			public NameId()
				: base("NAME")
			{}
			
			public override TokenSet SetLookahead(Parser parser)
			{
				if (lookahead == null)
				{
					base.SetLookahead(parser);
					lookahead.Add(new TokenSet(parser.TokenToId("IDENTIFIER")));
				}
				return lookahead;
			}
		}
		
		public class Id : Node
		{
			protected string name;
			
			// Token or Rule.
			public Node peer { get; protected set; }
			
			public CsGrammar.Rule Rule { get { return peer as Rule; } }
			
			public Id(string name)
			{
				this.name = name;
			}
			
			public Id Clone()
			{
				var clone = new Id(name) { peer = peer, lookahead = lookahead, follow = follow };
				var token = peer as Token;
				if (token != null)
				{
					clone.peer = token.Clone();
					clone.peer.parent = this;
				}
				return clone;
			}
			
			public override TokenSet SetLookahead(Parser parser)
			{
				if (lookahead == null)
				{
					peer = parser.GetPeer(name);
					if (peer == null)
						Debug.LogError("Parser rule \"" + name + "\" not found!!!");
					else
					{
						peer.parent = this;
						peer.childIndex = 0;
						lookahead = peer.SetLookahead(parser);
					}
				}
				return lookahead;
			}
			
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				SetLookahead(parser);
				if (peer is Rule)
					peer.SetFollow(parser, succ);
				
				return lookahead;
			}
			
			public override void CheckLL1(Parser parser) {}
			
			public override bool Scan(IScanner scanner)
			{
				return !scanner.KeepScanning || peer.Scan(scanner);
			}
			
			public override Node Parse(IScanner scanner)
			{
				peer.parent = this;
				var rule = peer as Rule;
				if (rule != null)
				{
					bool skip;
					scanner.CurrentParseTreeNode = scanner.CurrentParseTreeNode.AddNode(this, scanner, out skip);
					if (skip)
						return scanner.CurrentGrammarNode;
				}
				var result2 = peer.Parse(scanner);
				peer.parent = this;
				return result2;
			}
			
			public override Node NextAfterChild(Node child, IScanner scanner)
			{
				if (peer is Rule)
					scanner.CurrentParseTreeNode = scanner.CurrentParseTreeNode.parent;
				return base.NextAfterChild(this, scanner);
			}
			
			public override string ToString()
			{
				return name;
			}
			
			public string GetName()
			{
				return name;
			}
			
			public sealed override IEnumerable<Id> EnumerateIdNodes()
			{
				yield return this;
			}
		}
		
		public class Lit : Node
		{
			public readonly string body;
			public string pretty;
			
			public Lit(string body)
			{
				pretty = body;
				this.body = body.Trim();
			}
			
			public override TokenSet SetLookahead(Parser parser)
			{
				return lookahead ?? (lookahead = new TokenSet(parser.TokenToId(body)));
			}
			
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				return SetLookahead(parser);
			}
			
			public override void CheckLL1(Parser parser)
			{
			}
			
			public override bool Scan(IScanner scanner)
			{
				if (!scanner.KeepScanning)
					return true;
				
				if (!lookahead.Matches(scanner.Current.tokenId))
					return false;
				
				scanner.MoveNext();
				return true;
			}
			
			public override Node Parse(IScanner scanner)
			{
				if (!lookahead.Matches(scanner.Current.tokenId))
				{
					scanner.SyntaxErrorExpected(lookahead);
					return this;
				}
				
				scanner.CurrentParseTreeNode.AddToken(scanner).grammarNode = this;
				scanner.MoveNext();
				if (scanner.ErrorMessage == null)
				{
					scanner.ErrorParseTreeNode = scanner.CurrentParseTreeNode;
					scanner.ErrorGrammarNode = scanner.CurrentGrammarNode;
				}
				
				return parent.NextAfterChild(this, scanner);
			}
			
			public override string ToString()
			{
				return "\"" + body + "\"";
			}
			
			public sealed override IEnumerable<Lit> EnumerateLitNodes()
			{
				yield return this;
			}
		}
		
		// represents alternatives: node { "|" node }.
		
		public class Alt : Node
		{
			protected List<Node> nodes = new List<Node>();
			
			public Alt(params Node[] nodes)
			{
				foreach (var node in nodes)
					Add(node);
			}
			
			public override sealed void Add(Node node)
			{
				var idNode = node as Id;
				if (idNode != null)
					node = idNode.Clone();
				
				var altNode = node as Alt;
				if (altNode != null)
				{
					foreach (var n in altNode.nodes)
					{
						n.parent = this;
						nodes.Add(n);
					}
				}
				else
				{
					node.parent = this;
					nodes.Add(node);
				}
			}
			
			public override Node GetNode()
			{
				return nodes.Count == 1 ? nodes[0] : this;
			}
			
			// lookahead of each alternative must be
			// different, but more than one alternative
			// with empty input is allowed.
			// returns lookahead - union of alternatives.
			
			public override TokenSet SetLookahead(Parser parser)
			{
				if (lookahead == null)
				{
					lookahead = new TokenSet();
					foreach (var t in nodes)
					{
						if (t is If)
							continue;
						var set = t.SetLookahead(parser);
						if (lookahead.Accepts(set))
						{
							Debug.LogError(this + ": ambiguous alternatives");
							Debug.LogWarning(lookahead.Intersecton(set).ToString(parser));
						}
						lookahead.Add(set);
					}
					foreach (var t in nodes)
					{
						if (t is If)
						{
							var set = t.SetLookahead(parser);
							lookahead.Add(set);
						}
					}
				}
				return lookahead;
			}
			
			// each alternative gets same succ.
			
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				SetLookahead(parser);
				follow = succ;
				foreach (var node in nodes)
					node.SetFollow(parser, succ);
				return lookahead;
			}
			
			public override void CheckLL1(Parser parser)
			{
				base.CheckLL1(parser);
				foreach (var node in nodes)
					node.CheckLL1(parser);
			}
			
			public override bool Scan(IScanner scanner)
			{
				if (!scanner.KeepScanning)
					return true;
				
				foreach (var node in nodes)
					if (node.Matches(scanner))
						return node.Scan(scanner);
				
				if (!lookahead.MatchesEmpty())
					return false; // throw new Exception(scanner + ": syntax error in Alt");
				return true;
			}
			
			public override Node Parse(IScanner scanner)
			{
				foreach (var node in nodes)
				{
					if (node.Matches(scanner))
					{
						return node.Parse(scanner);
					}
				}
				if (lookahead.MatchesEmpty())
					return NextAfterChild(this, scanner);
				
				scanner.SyntaxErrorExpected(lookahead);
				return this;
			}
			
			public override string ToString()
			{
				var s = new StringBuilder("( " + nodes[0]);
				for (var n = 1; n < nodes.Count; ++n)
					s.Append(" | " + nodes[n]);
				s.Append(" )");
				return s.ToString();
			}
			
			public sealed override IEnumerable<Lit> EnumerateLitNodes()
			{
				foreach (var node in nodes)
					foreach (var subnode in node.EnumerateLitNodes())
						yield return subnode;
			}
			
			public sealed override IEnumerable<Id> EnumerateIdNodes()
			{
				foreach (var node in nodes)
					foreach (var subnode in node.EnumerateIdNodes())
						yield return subnode;
			}
			
			public override IEnumerable<T> EnumerateNodesOfType<T>()
			{
				foreach (var node in nodes)
					foreach (var subnode in node.EnumerateNodesOfType<T>())
						yield return subnode;
				base.EnumerateNodesOfType<T>();
			}
		}
		
		public class Many : Node
		{
			protected readonly Node node;
			
			public Many(Node node)
			{
				var idNode = node as Id;
				if (idNode != null)
					node = idNode.Clone();
				
				node.parent = this;
				this.node = node;
			}
			
			public override Node GetNode()
			{
				if (node is Opt)	// [{ [ n ] }] -> [{ n }]
					return new Many(node.GetNode());
				
				if (node is Many)	// [{ [{ n }] }] -> [{ n }]
					return node;
				
				return this;
			}
			
			// lookahead includes empty.
			public override TokenSet SetLookahead(Parser parser)
			{
				if (lookahead == null)
				{
					lookahead = new TokenSet(node.SetLookahead(parser));
					lookahead.AddEmpty();
				}
				return lookahead;
			}
			
			// subtree gets succ.
			
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				SetLookahead(parser);
				follow = succ;
				node.SetFollow(parser, succ);
				return lookahead;
			}
			
			// subtree is checked.
			public override void CheckLL1(Parser parser)
			{
				// trust the predicate!
				//base.CheckLL1(parser);
				if (follow == null)
					throw new Exception(this + ": follow not set");
				node.CheckLL1(parser);
			}
			
			public override bool Matches(IScanner scanner)
			{
				return node.Matches(scanner);
			}
			
			public override bool Scan(IScanner scanner)
			{
				if (!scanner.KeepScanning)
					return true;
				
				var ifNode = node as If ?? node as IfNot;
				if (ifNode != null)
				{
					int tokenIndex, line;
					do
					{
						tokenIndex = scanner.CurrentTokenIndex();
						line = scanner.CurrentLine();
						if (!node.Scan(scanner))
							return false;
						if (!scanner.KeepScanning)
							return true;	
					} while (scanner.CurrentTokenIndex() != tokenIndex || scanner.CurrentLine() != line);
				}
				else
				{
					while (lookahead.Matches(scanner.Current.tokenId))
					{
						int tokenIndex = scanner.CurrentTokenIndex();
						int line = scanner.CurrentLine();
						if (!node.Scan(scanner))
							return false;
						if (!scanner.KeepScanning)
							return true;
						if (scanner.CurrentTokenIndex() == tokenIndex && scanner.CurrentLine() == line)
							throw new Exception("Infinite loop!!!");
					}
				}
				return true;
			}
			
			public override Node NextAfterChild(Node child, IScanner scanner)
			{
				//	if (scanner.ErrorMessage == null || Parse(scanner.Clone()))
				return this;
				
				//	return base.NextAfterChild(child, scanner);
			}
			
			public override Node Parse(IScanner scanner)
			{
				var ifNode = node as If;
				if (ifNode != null)
				{
					if (!ifNode.Matches(scanner))
						return parent.NextAfterChild(this, scanner);
					
					var tokenIndex = scanner.CurrentTokenIndex();
					var line = scanner.CurrentLine();
					var nextNode = node.Parse(scanner);
					if (nextNode != this || scanner.CurrentTokenIndex() != tokenIndex || scanner.CurrentLine() != line)
						return nextNode;
					//Debug.Log("Exiting Many " + this + " in goal: " + scanner.CurrentParseTreeNode);
				}
				else
				{
					if (!lookahead.Matches(scanner.Current.tokenId))
						return parent.NextAfterChild(this, scanner);
					
					var tokenIndex = scanner.CurrentTokenIndex();
					var line = scanner.CurrentLine();
					var nextNode = node.Parse(scanner);
					if (!(nextNode == this && scanner.CurrentTokenIndex() == tokenIndex && scanner.CurrentLine() == line))
						// throw new Exception("Infinite loop!!! while parsing " + scanner.Current + " on line " + scanner.CurrentLine());
						return nextNode;
				}
				return parent.NextAfterChild(this, scanner);
			}
			
			public override String ToString()
			{
				return "[{ "+node+" }]";
			}
			
			public sealed override IEnumerable<Lit> EnumerateLitNodes()
			{
				foreach (var n in node.EnumerateLitNodes())
					yield return n;
			}
			
			public sealed override IEnumerable<Id> EnumerateIdNodes()
			{
				foreach (var n in node.EnumerateIdNodes())
					yield return n;
			}
			
			public override IEnumerable<T> EnumerateNodesOfType<T>()
			{
				foreach (var n in node.EnumerateNodesOfType<T>())
					yield return n;
				base.EnumerateNodesOfType<T>();
			}
		}
		
		//protected class Some : Many
		//{
		//    public Some(Node node)
		//        : base(node)
		//    {
		//    }
		
		//    public override Node GetNode()
		//    {
		//        if (node is Some)			// { { n } } -> { n }
		//            return node;
		
		//        if (node is Opt)		// { [ n ] } -> [{ n }]
		//            return new Many(node.GetNode());
		
		//        if (node is Many)		// { [{ n }] } -> [{ n }]
		//            return node;
		
		//        return this;
		//    }
		
		//    // lookahead results from subtree.
		//    public override TokenSet SetLookahead(Parser parser)
		//    {
		//        if (lookahead == null)
		//            lookahead = node.SetLookahead(parser);
		
		//        return lookahead;
		//    }
		
		//    // lookahead != follow; check subtree.
		//    public override void CheckLL1(Parser parser)
		//    {
		//        if (follow == null)
		//            throw new Exception(this + ": follow not set");
		//        if (lookahead.Accepts(follow))
		//            throw new Exception(this + ": ambiguous\n"
		//                + "  lookahead " + lookahead.ToString(parser) + "\n"
		//                + "  follow " + follow.ToString(parser));
		//        node.CheckLL1(parser);
		//    }
		
		//    public override void Parse(IScanner scanner, Goal goal)
		//    {
		//        if (lookahead.Matches(scanner.Current.tokenId))
		//            do
		//                node.Parse(scanner, goal);
		//            while (lookahead.Matches(scanner.Current.tokenId));
		//        else if (!lookahead.MatchesEmpty())
		//            throw new Exception(scanner + ": syntax error in Some");
		//    }
		
		//    public override string ToString()
		//    {
		//        return "{ " + node + " }";
		//    }
		//}
		
		protected class Opt : Many
		{
			public Opt(Node node)
				: base(node)
			{
			}
			
			public override Node GetNode()
			{
				//	if (node is Some)	// [ { n } ] -> [{ n }]
				//		return new Many(node.GetNode());
				
				if (node is Opt)	// [ [ n ] ] -> [ n ]
					return node;
				
				if (node is Many)	// [ [{ n }] ] -> [{ n }]
					return node;
				
				return this;
			}
			
			public override bool Scan(IScanner scanner)
			{
				if (!scanner.KeepScanning)
					return true;
				
				if (lookahead.Matches(scanner.Current.tokenId))
					return node.Scan(scanner);
				return true;
			}
			
			public override Node Parse(IScanner scanner)
			{
				if (lookahead.Matches(scanner.Current.tokenId))
					return node.Parse(scanner);
				return parent.NextAfterChild(this, scanner);
			}
			
			public override Node NextAfterChild(Node child, IScanner scanner)
			{
				return parent != null ? parent.NextAfterChild(this, scanner) : null;
			}
			
			public override string ToString()
			{
				return "[ " + node + " ]";
			}
		}
		
		protected class If : Opt
		{
			protected readonly Predicate<IScanner> predicate;
			protected readonly Node nodePredicate;
			protected readonly bool debug;
			
			public If(Predicate<IScanner> pred, Node node, bool debug = false)
				: base(node)
			{
				predicate = pred;
				this.debug = debug;
			}
			
			public If(Node pred, Node node, bool debug = false)
				: base(node)
			{
				nodePredicate = pred;
				this.debug = debug;
			}
			
			public override Node GetNode()
			{
				//    if (node is Some)	// [ { n } ] -> [{ n }]
				//        return new Many(node.GetNode());
				
				//    if (node is Opt)	// [ [ n ] ] -> [ n ]
				//        return node;
				
				//    if (node is Many)	// [ [{ n }] ] -> [{ n }]
				//        return node;
				
				return this;
			}
			
			public virtual bool CheckPredicate(IScanner scanner)
			{
				//if (debug)
				//{
				//    var s = scanner.Clone();
				//    Debug.Log(s.Current.tokenKind + " " + s.Current.text);
				//    s.MoveNext();
				//    Debug.Log(s.Current.tokenKind + " " + s.Current.text);
				//}
				if (predicate != null)
					return predicate(scanner);
				else if (nodePredicate != null)
				{
					return scanner.Lookahead(nodePredicate);//, new GoalAdapter());
				}
				return false;
			}
			
			public override bool Matches(IScanner scanner)
			{
				return lookahead.Matches(scanner.Current) && CheckPredicate(scanner);
			}
			
			public override bool Scan(IScanner scanner)
			{
				if (!scanner.KeepScanning)
					return true;
				
				if (lookahead.Matches(scanner.Current.tokenId) && CheckPredicate(scanner))
					return node.Scan(scanner);
				return true;
			}
			
			public override Node Parse(IScanner scanner)
			{
				if (lookahead.Matches(scanner.Current.tokenId) && CheckPredicate(scanner))
					return node.Parse(scanner);
				else
					return parent.NextAfterChild(this, scanner); // .Parse2(scanner, goal);
			}
			
			// lookahead doesn't include empty.
			
			public override TokenSet SetLookahead(Parser parser)
			{
				if (lookahead == null)
					lookahead = new TokenSet(node.SetLookahead(parser));
				return lookahead;
			}
			
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				if (nodePredicate != null && nodePredicate.follow == null)
					nodePredicate.SetFollow(parser, new TokenSet());
				return base.SetFollow(parser, succ);
			}
			
			public override string ToString()
			{
				return "[ ?(" + predicate + ") " + node + " ]";
			}
		}
		
		protected class IfNot : If
		{
			//public IfNot(Predicate<IScanner> pred, Node node)
			//    : base(pred, node)
			//{
			//}
			
			public IfNot(Node pred, Node node)
				: base(pred, node)
			{
			}
			
			public override bool CheckPredicate(IScanner scanner)
			{
				return !base.CheckPredicate(scanner);
			}
		}
		
		public class Seq : Node
		{
			// sequence of subtrees.
			// @serial nodes sequence of subtrees.
			
			private readonly List<Node> nodes = new List<Node>();
			//private readonly int debugLine = -1;
			
			public Seq(params Node[] nodes)
			{
				foreach (var t in nodes)
					Add(t);
			}
			
			//public Seq(int debugLine, params Node[] nodes)
			//{
			//	//this.debugLine = debugLine;
			//	foreach (var t in nodes)
			//		Add(t);
			//}
			
			public override sealed void Add(Node node)
			{
				var idNode = node as Id;
				if (idNode != null)
					node = idNode.Clone();
				
				var seqNode = node as Seq;
				if (seqNode != null)
					foreach (var n in seqNode.nodes)
						Add(n);
				else
				{
					node.parent = this;
					node.childIndex = nodes.Count;
					nodes.Add(node);
				}
			}
			
			// @return nodes[0] if only one.
			public override Node GetNode()
			{
				return nodes.Count == 1 ? nodes[0] : this;
			}
			
			// lookahead is union including first element
			// that does not accept empty input; it
			// includes empty input only if there is
			// no such element.
			public override TokenSet SetLookahead(Parser parser)
			{
				if (lookahead == null)
				{
					lookahead = new TokenSet();
					if (nodes.Count == 0)
						lookahead.AddEmpty();
					else
						for (int i = 0; i < nodes.Count; ++i)
					{
						var t = nodes[i];
						var set = t.SetLookahead(parser);
						lookahead.Add(set);
						if (!set.MatchesEmpty())
						{
							lookahead.RemoveEmpty();
							//for (int j = i + 1; j < nodes.Count; ++j)
							//	nodes[j].SetLookahead(parser);
							break;
						}
					}
				}
				return lookahead;
			}
			
			// each element gets successor's lookahead.
			
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				SetLookahead(parser);
				follow = succ;
				for (var n = nodes.Count; n-- > 0; )
				{
					var prev = nodes[n].SetFollow(parser, succ);
					if (prev.MatchesEmpty())
					{
						prev = new TokenSet(prev);
						prev.RemoveEmpty();
						prev.Add(succ);
					}
					succ = prev;
				}
				return lookahead;
			}
			
			public override void CheckLL1(Parser parser)
			{
				base.CheckLL1(parser);
				foreach (var t in nodes)
					t.CheckLL1(parser);
			}
			
			public override bool Scan(IScanner scanner)
			{
				foreach (var t in nodes)
				{
					if (!scanner.KeepScanning)
						return true;
					if (!t.Scan(scanner))
						return false;
				}
				return true;
			}
			
			public override Node NextAfterChild(Node child, IScanner scanner)
			{
				var index = child.childIndex;
				if (++index < nodes.Count)
					return nodes[index];
				return base.NextAfterChild(this, scanner);
			}
			
			public override Node Parse(IScanner scanner)
			{
				return nodes[0].Parse(scanner);
			}
			
			public override string ToString()
			{
				var s = new StringBuilder("( ");
				foreach (var t in nodes)
					s.Append(" " + t);
				s.Append(" )");
				return s.ToString();
			}
			
			public sealed override IEnumerable<Lit> EnumerateLitNodes()
			{
				foreach (var node in nodes)
					foreach (var subnode in node.EnumerateLitNodes())
						yield return subnode;
			}
			
			public sealed override IEnumerable<Id> EnumerateIdNodes()
			{
				foreach (var node in nodes)
					foreach (var subnode in node.EnumerateIdNodes())
						yield return subnode;
			}
			
			public override IEnumerable<T> EnumerateNodesOfType<T>()
			{
				foreach (var node in nodes)
					foreach (var subnode in node.EnumerateNodesOfType<T>())
						yield return subnode;
			}
		}
		
		public class Token : Node
		{
			protected string name;
			
			public Token(string name, TokenSet lookahead)
			{
				this.name = name;
				this.lookahead = lookahead;
			}
			
			public Token Clone()
			{
				var clone = new Token(name, lookahead);
				return clone;
			}
			
			// returns a one-element set,
			// initialized by the parser.
			public override TokenSet SetLookahead(Parser parser)
			{
				return lookahead;
			}
			
			// follow doesn't need to be set.
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				return lookahead;
			}
			
			// follow is not set; nothing to check.
			public override void CheckLL1(Parser parser)
			{
			}
			
			public override bool Scan(IScanner scanner)
			{
				if (!scanner.KeepScanning)
					return true;
				
				if (!lookahead.Matches(scanner.Current.tokenId))
					return false;
				scanner.MoveNext();
				return true;
			}
			
			public override Node Parse(IScanner scanner)
			{
				if (!lookahead.Matches(scanner.Current.tokenId))
				{
					scanner.SyntaxErrorExpected(lookahead);
					return this;
				}
				
				scanner.CurrentParseTreeNode.AddToken(scanner).grammarNode = this;
				scanner.MoveNext();
				if (scanner.ErrorMessage == null)
				{
					scanner.ErrorParseTreeNode = scanner.CurrentParseTreeNode;
					scanner.ErrorGrammarNode = scanner.CurrentGrammarNode;
				}
				
				return parent.NextAfterChild(this, scanner);
			}
			
			public override string ToString()
			{
				return name;
			}
		}
		
		public class Parser : Node
		{
			private readonly List<Rule> rules = new List<Rule>();
			public Rule Start { get { return rules[0]; } }
			
			// maps Rule.nt to rule.
			private readonly Dictionary<string, Rule> nts = new Dictionary<string, Rule>();
			
			// maps Id.name to Token or Rule; kept for scanner.
			protected Dictionary<string, Node> ids = new Dictionary<string, Node>();
			
			// maps token number to token name;
			// kept to symbolically displaying tokens in sets.
			protected string[] tokens;
			
			public Parser(Rule start, CsGrammar grammar)
			{
				//	Grammar = grammar;
				rules.Add(start);
			}
			
			// adds each rule.
			public void Add(Rule rule)
			{
				var nt = rule.GetNt();
				if (nts.ContainsKey(nt))
					throw new Exception(nt + ": duplicate");
				nts.Add(nt, rule);
				rules.Add(rule);
			}
			
			public void InitializeGrammar()
			{
				// maps Lit.body to set containing token
				var lits = new HashSet<string>();
				var t = new List<String>();
				
				foreach (Lit lit in EnumerateLitNodes())
				{
					if (lits.Contains(lit.body))
						continue;
					
					lits.Add(lit.body);
					t.Add(lit.body);
				}
				foreach (var id in EnumerateIdNodes())
				{
					var idName = id.GetName();
					if (ids.ContainsKey(idName))
						continue;
					
					ids.Add(idName, nts.ContainsKey(idName) ? nts[idName] : null);
					t.Add(idName);
				}
				
				t.Sort();
				tokens = t.ToArray();
				for (var i = 0; i < tokens.Length; ++i)
				{
					var name = tokens[i];
					if (!lits.Contains(name) && ids[name] == null)
					{
						ids[name] = new Token(name, new TokenSet(i));
						
						if (name == "NAME")
						{
							ids[name].lookahead.Add(ids["IDENTIFIER"].lookahead);
						}
					}
				}
				
				SetLookahead(this);
				SetFollow(this, null);
				CheckLL1(this);
				
				//var sb = new StringBuilder();
				//foreach (var rule in rules)
				//    if (rule.lookahead.Matches(FlipbookGames.ScriptInspector2.CsGrammar.Instance.tokenIdentifier))
				//        sb.AppendLine("Lookahead(" + rule.GetNt() + "): " + rule.lookahead.ToString(this));
				//UnityEngine.Debug.Log(sb.ToString());
			}
			
			public override TokenSet SetLookahead(Parser parser)
			{
				foreach (var rule in rules)
				{
					rule.SetLookahead(this);
				}
				return null;
			}
			
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				Start.SetFollow();
				bool followChanged;
				do
				{
					foreach (var rule in rules)
						rule.SetFollow(this);
					followChanged = false;
					foreach (var rule in rules)
						followChanged |= rule.FollowChanged();
				} while (followChanged);
				return null;
			}
			
			public override void CheckLL1(Parser parser)
			{
				foreach (var rule in rules)
					rule.CheckLL1(this);
			}
			
			public override bool Scan(IScanner scanner)
			{
				throw new InvalidOperationException();
			}
			
			public override Node Parse(IScanner scanner)
			{
				throw new InvalidOperationException();
			}
			
			public ParseTree ParseAll(IScanner scanner)
			{
				if (!scanner.MoveNext())
					return null;
				
				var parseTree = new ParseTree();
				var rootId = new Id(Start.GetNt());
				ids[Start.GetNt()] = Start;
				rootId.SetLookahead(this);
				Start.parent = rootId;
				scanner.CurrentParseTreeNode = parseTree.root = new ParseTree.Node(rootId);
				scanner.CurrentGrammarNode = Start.Parse(scanner);
				
				scanner.ErrorParseTreeNode = scanner.CurrentParseTreeNode;
				scanner.ErrorGrammarNode = scanner.CurrentGrammarNode;
				
				while (scanner.CurrentGrammarNode != null)
					if (!ParseStep(scanner))
						break;
				
				//if (scanner.MoveNext())
				//	Debug.LogWarning(scanner + ": trash at end");
				return parseTree;
			}
			
			public bool ParseStep(IScanner scanner)
			{
				//			if (scanner.ErrorMessage == null && scanner.ErrorParseTreeNode == null)
				//			{
				//				scanner.ErrorParseTreeNode = scanner.CurrentParseTreeNode;
				//				scanner.ErrorGrammarNode = scanner.CurrentGrammarNode;
				//			}
				
				//scanner.CurrentParseTreeNode.AddToken(scanner).grammarNode = this;
				//scanner.MoveNext();
				
				//return parent.NextAfterChild(this, scanner);
				
				if (scanner.CurrentGrammarNode == null)
					return false;
				
				//var errorGrammarNode = scanner.CurrentGrammarNode;
				//var errorParseTreeNode = scanner.CurrentParseTreeNode;
				
				//var numValidNodes = scanner.CurrentParseTreeNode.numValidNodes;
				
				var token = scanner.Current;
				if (scanner.ErrorMessage == null)
				{
					while (scanner.CurrentGrammarNode != null)
					{
						scanner.CurrentGrammarNode = scanner.CurrentGrammarNode.Parse(scanner);
						if (scanner.ErrorMessage != null || token != scanner.Current)
							break;
					}
					
					//if (scanner.CurrentGrammarNode == null)
					//{
					//	Debug.LogError("scanner.CurrentGrammarNode == null");
					//	return false;
					//}
					
					//if (scanner.ErrorMessage != null)
					//{
					//    Debug.Log("ErrorGrammarNode: " + scanner.ErrorGrammarNode +
					//        "\nErrorParseTreeNode: " + scanner.ErrorParseTreeNode);
					//}
					
					if (scanner.ErrorMessage == null && token != scanner.Current)
					{
						scanner.ErrorParseTreeNode = scanner.CurrentParseTreeNode;
						scanner.ErrorGrammarNode = scanner.CurrentGrammarNode;
					}
				}
				if (scanner.ErrorMessage != null)
				{
					if (token.tokenKind == SyntaxToken.Kind.EOF)
					{
						//	Debug.LogError("Unexpected end of file in ParseStep");
						return false;
					}
					
					var missingParseTreeNode = scanner.CurrentParseTreeNode;
					var missingGrammarNode = scanner.CurrentGrammarNode;
					
					// Rolling back all recent parser state changes
					scanner.CurrentParseTreeNode = scanner.ErrorParseTreeNode;
					scanner.CurrentGrammarNode = scanner.ErrorGrammarNode;
					if (scanner.CurrentParseTreeNode != null)
					{
						var cpt = scanner.CurrentParseTreeNode;
						for (var i = cpt.numValidNodes; i > 0 && !cpt.ChildAt(--i).HasLeafs(); )
							cpt.InvalidateFrom(i);
					}
					
					if (scanner.CurrentGrammarNode != null)
					{
						int numSkipped;
						scanner.CurrentGrammarNode = scanner.CurrentGrammarNode.Recover(scanner, out numSkipped);
					}
					if (scanner.CurrentGrammarNode == null)
					{
						if (token.parent != null)
							token.parent.ReparseToken();
						//					if (scanner.ErrorToken == null)
						//						scanner.ErrorToken = scanner.ErrorParseTreeNode.AddToken(scanner);
						//					else
						//						scanner.ErrorParseTreeNode.AddToken(scanner);
						new ParseTree.Leaf(scanner);
						
						if (cachedErrorGrammarNode == scanner.ErrorGrammarNode)
						{
							token.parent.syntaxError = cachedErrorMessage;
						}
						else
						{
							token.parent.syntaxError = "Unexpected token! Expected " + scanner.ErrorGrammarNode.lookahead.ToString(this);
							cachedErrorMessage = token.parent.syntaxError;
							cachedErrorGrammarNode = scanner.ErrorGrammarNode;
						}
						//	scanner.ErrorMessage = cachedErrorMessage;
						//	Debug.LogError("Skipped " + token + "added to " + token.parent + "\nparent: " + token.parent.parent);
						
						
						scanner.CurrentGrammarNode = scanner.ErrorGrammarNode;
						scanner.CurrentParseTreeNode = scanner.ErrorParseTreeNode;
						
						//	token = scanner.Current;
						//	token.parent = errorParseTreeNode;
						
						//Debug.Log("Skipping " + scanner.Current.tokenKind + " \"" + scanner.Current.text + "\"");
						if (!scanner.MoveNext())
						{
							//	Debug.LogError("Unexpected end of file");
							return false;
						}
						scanner.ErrorMessage = null;
					}
					else
					{
						//var sb = new StringBuilder();
						//scanner.CurrentParseTreeNode.Dump(sb, 0);
						//Debug.Log("Recovered on " + scanner.CurrentGrammarNode + " (current token: " + scanner.Current +
						//	" at line " + scanner.CurrentLine() + ":" + scanner.CurrentTokenIndex() +
						//	")" + //"\nnumSkipped: " + numSkipped +
						//	"\nin parent: " + scanner.CurrentGrammarNode.parent +
						//	"\nCurrentParseTreeNode is:\n" + sb);
						
						//if (scanner.ErrorToken == null)
						{
							//var n = scanner.ErrorGrammarNode;
							//while (n != null && !(n is Id))
							//    n = n.parent;
							//scanner.ErrorParseTreeNode.errors = scanner.ErrorParseTreeNode.errors ?? new List<string>();
							//scanner.ErrorParseTreeNode.errors.Add("Not a valid " + n + "! Expected " + scanner.ErrorGrammarNode.lookahead.ToString(this));
							
							if (missingGrammarNode != null && missingParseTreeNode != null)
							{
								scanner.CurrentParseTreeNode = missingParseTreeNode;
								scanner.CurrentGrammarNode = missingGrammarNode;
							}
							
							scanner.InsertMissingToken(scanner.ErrorMessage
							                           ?? ("Expected " + missingGrammarNode.lookahead.ToString(this)));
							
							if (missingGrammarNode != null && missingParseTreeNode != null)
							{
								scanner.ErrorMessage = null;
								scanner.ErrorToken = null;
								scanner.CurrentParseTreeNode = missingParseTreeNode;
								scanner.CurrentGrammarNode = missingGrammarNode;
								scanner.CurrentGrammarNode = missingGrammarNode.parent.NextAfterChild(missingGrammarNode, scanner);
							}
						}
						scanner.ErrorMessage = null;
						scanner.ErrorToken = null;
					}
				}
				
				return true;
			}
			
			private static string cachedErrorMessage;
			private static Node cachedErrorGrammarNode;
			
			public override string ToString()
			{
				var s = new StringBuilder(GetType().Name + " {\n");
				foreach (var rule in rules)
					s.AppendLine(rule.ToString(this));
				s.Append("}");
				return s.ToString();
			}
			
			public int TokenToId(string s)
			{
				return Array.BinarySearch<string>(tokens, s);
			}
			
			public string GetToken(int tokenId)
			{
				return tokenId >= 0 && tokenId < tokens.Length ? tokens[tokenId] : tokenId + "?";
			}
			
			// returns Token or Rule for Id.
			public Node GetPeer(string name)
			{
				var peer = ids[name];
				var token = peer as Token;
				if (token != null)
					peer = token.Clone();
				return peer;
			}
			
			public sealed override IEnumerable<Lit> EnumerateLitNodes()
			{
				foreach (var rule in rules)
					foreach (var node in rule.EnumerateLitNodes())
						yield return node;
			}
			
			public sealed override IEnumerable<Id> EnumerateIdNodes()
			{
				foreach (var rule in rules)
					foreach (var node in rule.EnumerateIdNodes())
						yield return node;
			}
			
			public override IEnumerable<T> EnumerateNodesOfType<T>()
			{
				foreach (var rule in rules)
					foreach (var node in rule.EnumerateNodesOfType<T>())
						yield return node;
				base.EnumerateNodesOfType<T>();
			}
		}
		
		public class Rule : Node
		{
			public static bool debug;
			public SemanticFlags semantics;
			public bool autoExclude;
			public bool contextualKeyword;
			
			// nonterminal name.
			protected string nt;
			
			// right hand side subtree.
			protected Node rhs;
			
			public Rule(string nt, Node rhs)
			{
				var idNode = rhs as Id;
				if (idNode != null)
					rhs = idNode.Clone();
				
				this.nt = nt;
				
				rhs.parent = this;
				this.rhs = rhs;
			}
			
			// used to detect left recursion and to
			// flag if follow needs to be recomputed.
			protected bool inProgress;
			
			// gets lookahead from rhs, sets inProgress.
			public override TokenSet SetLookahead(Parser parser)
			{
				if (lookahead == null)
				{
					if (inProgress)
						throw new Exception(nt + ": recursive lookahead");
					inProgress = true;
					lookahead = rhs.SetLookahead(parser);
					
				}
				return lookahead;
			}
			
			// set if follow has changed.
			protected bool followChanged;
			
			// resets before recomputing follow.
			public bool FollowChanged()
			{
				inProgress = followChanged;
				followChanged = false;
				return inProgress;
			}
			
			// initializes follow with an empty set.
			// used only once, only for the start rule.
			public void SetFollow()
			{
				follow = new TokenSet();
			}
			
			// traverses rhs;
			// should reach all rules from start rule.
			public void SetFollow(Parser parser)
			{
				if (lookahead == null)
					throw new Exception(nt + ": lookahead not set");
				if (follow == null)
					return;// throw new Exception(nt + ": not connected");
				if (inProgress)
					rhs.SetFollow(parser, follow);
			}  
			
			// sets or adds to (new) follow set
			// and reports changes to parser.
			// returns lookahead.
			public override TokenSet SetFollow(Parser parser, TokenSet succ)
			{
				if (follow == null)
				{
					followChanged = true;
					follow = new TokenSet(succ);
					
				}
				else if (follow.Add(succ))
				{
					followChanged = true;
				}
				return lookahead;
			}
			
			public override void CheckLL1(Parser parser)
			{
				if (!contextualKeyword)
				{
					base.CheckLL1(parser);
					rhs.CheckLL1(parser);
				}
			}
			
			public override bool Scan(IScanner scanner)
			{
				if (!scanner.KeepScanning)
					return true;
				
				if (lookahead.Matches(scanner.Current))
					return rhs.Scan(scanner);
				else if (!lookahead.MatchesEmpty())
					return false; 
				
				return true;
			}
			
			public override Node Parse(IScanner scanner)
			{
				return RhsParse2(scanner);
			}
			
			public override Node NextAfterChild(Node child, IScanner scanner)
			{
				var temp = scanner.CurrentParseTreeNode;
				if (temp == null)
					return null;
				var res = temp.grammarNode != null ? temp.grammarNode.NextAfterChild(this, scanner) : null;
				
				if (scanner.Seeking)
					return res;
				
				if (contextualKeyword && temp.numValidNodes == 1)
				{
					var token = temp.LeafAt(0).token;
					token.tokenKind = SyntaxToken.Kind.ContextualKeyword;
				}
				
				/*if (autoExclude && temp.numValidNodes == 1)
				{
					temp.Exclude();
				}
				else*/ if (temp.semantics != SemanticFlags.None)
				{
					scanner.OnReduceSemanticNode(temp);
				}
				else
				{
					temp.Reduce();
				}
				
				return res;
			}
			
			private Node RhsParse2(IScanner scanner)
			{
				bool wasError = scanner.ErrorMessage != null;
				Node res = null;
				if (lookahead.Matches(scanner.Current))
				{
					//try
					//{
					res = rhs.Parse(scanner);
					//}
					//catch (Exception e)
					//{
					//	throw new Exception(e.Message + "<=" + this.nt, e);
					//}
				}
				if ((res == null || !wasError && scanner.ErrorMessage != null) && !lookahead.MatchesEmpty())
				{
					scanner.SyntaxErrorExpected(lookahead);
					return res ?? this;
				}
				if (res != null)
					return res;
				
				return NextAfterChild(rhs, scanner); // ready to be reduced
			}
			
			public override string ToString()
			{
				return nt + " : " + rhs + " .";
			}
			
			public string ToString(Parser parser)
			{
				var result = new StringBuilder(nt + " : " + rhs + " .");
				if (lookahead != null)
					result.Append("\n  lookahead " + lookahead.ToString(parser));
				if (follow != null)
					result.Append("\n  follow " + follow.ToString(parser));
				return result.ToString();
			}
			
			public string GetNt()
			{
				return nt;
			}
			
			public sealed override IEnumerable<Lit> EnumerateLitNodes()
			{
				foreach (var node in rhs.EnumerateLitNodes())
					yield return node;
			}
			
			public sealed override IEnumerable<Id> EnumerateIdNodes()
			{
				foreach (var node in rhs.EnumerateIdNodes())
					yield return node;
			}
			
			public override IEnumerable<T> EnumerateNodesOfType<T>()
			{
				foreach (var node in rhs.EnumerateNodesOfType<T>())
					yield return node;
				base.EnumerateNodesOfType<T>();
			}
		}
		
		public class TokenSet
		{
			// true if empty input is acceptable.
			protected bool empty;
			
			// if != null: many elements.
			private BitArray set;
			
			// else if >= 0: single element.
			private int tokenId = -1;
			
			public int GetDataSet(out BitArray bitArray)
			{
				bitArray = set;
				return tokenId;
			}
			
			// empty set, doesn't accept even empty input.
			public TokenSet() {}
			
			public TokenSet(int tokenId)
			{
				this.tokenId = tokenId;
			}
			
			public TokenSet(TokenSet s)
			{
				empty = s.empty;
				if (s.set != null)
					set = new BitArray(s.set);
				else
					tokenId = s.tokenId;
			}
			
			public void AddEmpty()
			{
				empty = true;
			}
			
			public void RemoveEmpty()
			{
				empty = false;
			}
			
			public bool Remove(int token)
			{
				if (set == null)
				{
					if (token != tokenId)
						return false;
					tokenId = -1;
					return true;
				}
				if (token >= set.Count)
					Debug.LogError("Unknown token " + token);
				bool result = set[token];
				set[token] = false;
				return result;
			}
			
			// set to accept additional set of tokens.
			// returns true if set changed.
			public bool Add(TokenSet s)
			{
				var result = false;
				if (s.empty && !empty)
				{
					empty = true;
					result = true;
				}
				if (s.set != null)
				{
					if (set != null)
					{
						for (var n = 0; n < s.set.Count; ++n)
						{
							if (s.set[n] && !set[n])
							{
								set.Set(n, true);
								result = true;
							}
						}
					}
					else
					{
						set = new BitArray(s.set);
						if (tokenId >= 0)
						{
							set.Set(tokenId, true);
							tokenId = -1;
						}
						result = true;	// s.set cannot just contain one token
					}
				}
				else if (s.tokenId >= 0)
				{
					if (set != null)
					{
						if (!set.Get(s.tokenId))
						{
							set.Set(s.tokenId, true);
							result = true;
						}
					}
					else if (tokenId >= 0)
					{
						if (tokenId != s.tokenId)
						{
							set = new BitArray(700, false);
							set.Set(s.tokenId, true);
							set.Set(tokenId, true);
							tokenId = -1;
							result = true;
						}
					}
					else
					{
						tokenId = s.tokenId;
						result = true;
					}
				}
				return result;
			}
			
			// checks if lookahead accepts empty input.
			public bool MatchesEmpty()
			{
				return empty;
			}
			
			// checks if lookahead accepts input symbol.
			public bool Matches(TokenSet tokenSet)
			{
				if (tokenSet == null)
					return false;
				if (tokenSet.tokenId >= 0)
					return set != null ? set[tokenSet.tokenId] : tokenId == tokenSet.tokenId;
				throw new Exception("matches() botched");
			}
			
			// checks if lookahead accepts input symbol.
			public bool Matches(SyntaxToken token)
			{
				return set != null ? set[token.tokenId] : token.tokenId == tokenId;
			}
			
			// checks if lookahead accepts input symbol.
			public bool Matches(int token)
			{
				if (set == null)
					return token == tokenId;
				if (token >= set.Count)
					Debug.LogError("Unknown token " + token);
				return set[token];
			}
			
			// checks for ambiguous lookahead.
			public bool Accepts(TokenSet s)
			{
				if (s.set != null)
				{
					if (set != null)
					{
						var intersection = new BitArray(set);
						intersection = intersection.And(s.set);
						for (var n = 0; n < intersection.Count; ++n)
							if (intersection[n])
								return true;
					}
					else if (tokenId >= 0)
						return s.set[tokenId];
				}
				else if (s.tokenId >= 0)
				{
					if (set != null)
						return set[s.tokenId];
					if (tokenId >= 0)
						return tokenId == s.tokenId;
				}
				return false;
			}
			
			public TokenSet Intersecton(TokenSet s)
			{
				if (s.set != null)
				{
					if (set != null)
					{
						var intersection = new BitArray(set);
						intersection = intersection.And(s.set);
						var ts = new TokenSet();
						for (var i = 0; i < intersection.Length; ++i)
							if (intersection[i])
								ts.Add(new TokenSet(i));
						return ts;
					}
					else if (tokenId >= 0 && s.set[tokenId])
						return this;
				}
				else if (s.tokenId >= 0)
				{
					if (set != null && set[s.tokenId])
						return s;
					if (tokenId >= 0 && tokenId == s.tokenId)
						return this;
				}
				return new TokenSet();
			}
			
			public override string ToString()
			{
				var result = new StringBuilder();
				var delim = "";
				if (empty)
				{
					result.Append("empty");
					delim = ", ";
				}
				if (set != null)
					result.Append(delim + "set " + set);
				else if (tokenId >= 0)
					result.Append(delim + "token " + tokenId);
				return "{" + result + "}";
			}
			
			private string cached;
			public string ToString(Parser parser)
			{
				if (cached != null)
					return cached;
				
				var result = new StringBuilder();
				var delim = string.Empty;
				if (empty)
				{
					result.Append("[empty]");
					delim = ", ";
				}
				if (set != null)
				{
					for (var n = 0; n < set.Count; ++n)
					{
						if (set.Get(n))
						{
							result.Append(delim + parser.GetToken(n));
							delim = n == set.Count - 2 ? ", or " : ", ";
						}	
					}
				}
				else if (tokenId >= 0)
				{
					result.Append(delim + parser.GetToken(tokenId));
				}
				return cached = result.ToString();
			}
		}
		
		public abstract int TokenToId(string s);
		
		public abstract string GetToken(int tokenId);
	}

	public static class CsGrammarExtensions
	{
		public static CsGrammar.Lit ToLit(this string s)
		{
			return new CsGrammar.Lit(s);
		}
	}

}