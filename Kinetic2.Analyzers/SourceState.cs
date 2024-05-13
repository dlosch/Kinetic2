using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

internal class SourceState {
    public Location? Location { get; }
    protected SourceState(Location? location) => Location = location;
}

internal class DiRegistrationSourceState(
    InvocationExpressionSyntax node /*SyntaxNode node*/,
    IInvocationOperation operation,
    string? containingNamespace,
    bool hasFactoryParameter,
    bool hasInstanceParameter,
    bool isKeyed,
    INamedTypeSymbol? @interface,
    INamedTypeSymbol implementation /*, Type? InterfaceType = default, Type? ImplementationType = default*/)
    : SourceState(node.GetLocation()) {

    public SyntaxNode Node { get; } = node;
    public IInvocationOperation Operation { get; } = operation;
    public string? ContainingNamespace { get; } = containingNamespace;
    public bool HasFactoryParameter { get; } = hasFactoryParameter;
    public bool HasInstanceParameter { get; } = hasInstanceParameter;
    public bool IsKeyed { get; } = isKeyed;
    public INamedTypeSymbol? Interface { get; } = @interface;
    public INamedTypeSymbol Implementation { get; } = implementation;

    public string RegistrationMethodName => Operation.TargetMethod.FullNameEx();

    public override string ToString() => $"{Operation.TargetMethod.Name} {ContainingNamespace} {Interface} {Implementation}";
};


internal class MethodDefinitionSourceState : SourceState {
    public MethodDefinitionSourceState(IMethodSymbol method, INamedTypeSymbol declaringType, INamedTypeSymbol? interfaceType, ResiliencePipelineAttribute attribute, Location? location) : base(location) {
        Method = method;
        DeclaringType = declaringType;
        InterfaceType = interfaceType;
        Attribute = attribute;
    }

    public IMethodSymbol Method { get; }
    public INamedTypeSymbol DeclaringType { get; }
    public INamedTypeSymbol? InterfaceType { get; }
    public ResiliencePipelineAttribute Attribute { get; }
}

internal class DiagnosticsSourceState : SourceState {
    public DiagnosticsSourceState(SyntaxNode node, Diagnostic diagnostic) : base(node.GetLocation()) {
        Diagnostic = diagnostic;
    }

    public Diagnostic Diagnostic { get; }
}
