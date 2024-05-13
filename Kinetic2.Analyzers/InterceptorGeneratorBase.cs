using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

public abstract class InterceptorGeneratorBase : DiagnosticAnalyzer, IIncrementalGenerator {
    public override void Initialize(AnalysisContext context) {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // we won't register anything; all we really want here is to report our supported diagnostics
    }

    /// <inheritdoc/>
    public abstract void Initialize(IncrementalGeneratorInitializationContext context);
}

