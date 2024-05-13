using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

internal abstract class ParseContextProxy {
    public abstract SyntaxNode Node { get; }
    public abstract CancellationToken CancellationToken { get; }
    public abstract SemanticModel SemanticModel { get; }
    public abstract AnalyzerOptions? Options { get; }
}
