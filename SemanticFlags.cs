﻿// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.1
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------
using System;
namespace CSharpCodeParser
{
	public enum SemanticFlags
	{
		None = 0,
		
		SymbolDeclarationsMask = (1 << 8) - 1,
		ScopesMask = ~SymbolDeclarationsMask,
		
		SymbolDeclarationsBegin = 1,
		
		NamespaceDeclaration,
		UsingNamespace,
		UsingAlias,
		ExternAlias,
		ClassDeclaration,
		TypeParameterDeclaration,
		BaseListDeclaration,
		ConstructorDeclarator,
		DestructorDeclarator,
		ConstantDeclarator,
		MethodDeclarator,
		LocalVariableDeclarator,
		ForEachVariableDeclaration,
		FromClauseVariableDeclaration,
		LabeledStatement,
		CatchExceptionParameterDeclaration,
		FixedParameterDeclaration,
		ParameterArrayDeclaration,
		ImplicitParameterDeclaration,
		ExplicitParameterDeclaration,
		PropertyDeclaration,
		IndexerDeclaration,
		GetAccessorDeclaration,
		SetAccessorDeclaration,
		EventDeclarator,
		EventWithAccessorsDeclaration,
		AddAccessorDeclaration,
		RemoveAccessorDeclaration,
		VariableDeclarator,
		OperatorDeclarator,
		ConversionOperatorDeclarator,
		StructDeclaration,
		InterfaceDeclaration,
		InterfacePropertyDeclaration,
		InterfaceMethodDeclaration,
		InterfaceEventDeclaration,
		InterfaceIndexerDeclaration,
		InterfaceGetAccessorDeclaration,
		InterfaceSetAccessorDeclaration,
		EnumDeclaration,
		EnumMemberDeclaration,
		DelegateDeclaration,
		AnonymousObjectCreation,
		MemberDeclarator,
		LambdaExpressionDeclaration,
		AnonymousMethodDeclaration,
		
		SymbolDeclarationsEnd,
		
		
		ScopesBegin                   = 1 << 8,
		
		CompilationUnitScope          = 1 << 8,
		NamespaceBodyScope            = 2 << 8,
		ClassBaseScope                = 3 << 8,
		TypeParameterConstraintsScope = 4 << 8,
		ClassBodyScope                = 5 << 8,
		StructInterfacesScope         = 6 << 8,
		StructBodyScope               = 7 << 8,
		InterfaceBaseScope            = 8 << 8,
		InterfaceBodyScope            = 9 << 8,
		FormalParameterListScope      = 10 << 8,
		EnumBaseScope                 = 11 << 8,
		EnumBodyScope                 = 12 << 8,
		MethodBodyScope               = 13 << 8,
		ConstructorInitializerScope   = 14 << 8,
		LambdaExpressionScope         = 15 << 8,
		LambdaExpressionBodyScope     = 16 << 8,
		AnonymousMethodScope          = 17 << 8,
		AnonymousMethodBodyScope      = 18 << 8,
		CodeBlockScope                = 19 << 8,
		SwitchBlockScope              = 20 << 8,
		ForStatementScope             = 21 << 8,
		EmbeddedStatementScope        = 22 << 8,
		UsingStatementScope           = 23 << 8,
		LocalVariableInitializerScope = 24 << 8,
		SpecificCatchScope            = 25 << 8,
		ArgumentListScope             = 26 << 8,
		AttributeArgumentsScope       = 27 << 8,
		MemberInitializerScope        = 28 << 8,
		
		TypeDeclarationScope          = 29 << 8,
		MethodDeclarationScope        = 30 << 8,
		AttributesScope               = 31 << 8,
		AccessorBodyScope             = 32 << 8,
		AccessorsListScope            = 33 << 8,
		QueryExpressionScope          = 34 << 8,
		QueryBodyScope                = 35 << 8,
		MemberDeclarationScope        = 36 << 8,
		
		ScopesEnd,
	}

}
