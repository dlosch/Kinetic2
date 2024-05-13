using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

internal readonly struct ParseState {
    public ParseState(ParseContextProxy proxy) {
        CancellationToken = proxy.CancellationToken;
        Node = proxy.Node;
        SemanticModel = proxy.SemanticModel;
        Options = proxy.Options;
    }
    public ParseState(in GeneratorSyntaxContext context, CancellationToken cancellationToken) {
        CancellationToken = cancellationToken;
        Node = context.Node;
        SemanticModel = context.SemanticModel;
        Options = null;
    }
    public ParseState(in OperationAnalysisContext context) {
        CancellationToken = context.CancellationToken;
        Node = context.Operation.Syntax;
        SemanticModel = context.Operation.SemanticModel!;
        Options = context.Options;
    }
    public readonly CancellationToken CancellationToken;
    public readonly SyntaxNode Node;
    public readonly SemanticModel SemanticModel;
    public readonly AnalyzerOptions? Options;
}
