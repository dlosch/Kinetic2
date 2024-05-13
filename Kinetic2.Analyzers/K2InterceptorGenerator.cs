using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Kinetic2.Analyzers;



[Generator(LanguageNames.CSharp), DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class K2InterceptorGenerator : InterceptorGeneratorBase {
    public override void Initialize(IncrementalGeneratorInitializationContext context) {
        var nodes = context.SyntaxProvider.CreateSyntaxProvider(PreFilter, Parse)
                        .Where(x => x is not null)
                        .Select((x, _) => x!);
        var combined = context.CompilationProvider.Combine(nodes.Collect());

        context.RegisterImplementationSourceOutput(combined, Generate);
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray<DiagnosticDescriptor>.Empty;

    internal bool PreFilter(SyntaxNode node, CancellationToken cancellationToken) {
        // RegisterKinetic2
        //Debugger.Launch();

        if (node is InvocationExpressionSyntax ie && ie.ChildNodes().FirstOrDefault() is MemberAccessExpressionSyntax ma) {
            bool IsCandidate(MemberAccessExpressionSyntax memberAccessExpressionSyntax) {
                return memberAccessExpressionSyntax.Name.Identifier.Text == "RegisterKinetic2";
            }

            return IsCandidate(ma);
        }

        return false;
    }

    private SourceState? Parse(GeneratorSyntaxContext ctx, CancellationToken cancellationToken) {
        try {
            return Parse(new(ctx, cancellationToken));
        }
        catch (Exception ex) {
            Debug.Fail(ex.Message);
            return null;
        }

    }

    internal static SourceState? Parse(ParseState ctx) {
        if (ctx.Node is InvocationExpressionSyntax ie
           && ctx.SemanticModel.GetOperation(ie) is IInvocationOperation op) {

            if (op.TargetMethod.Name == "RegisterKinetic2"
                && op.TargetMethod.ContainingType is {
                    Name: "ServiceCollectionExtensions",
                    ContainingNamespace: {
                        Name: nameof(Kinetic2), // "Kinetic2",
                        ContainingNamespace.IsGlobalNamespace: true
                    }
                }) {
                var callLocation = op.GetMemberLocation();
                var loc = Location.Create(ctx.Node.SyntaxTree, ctx.Node.Span);
                return new InterceptSourceState(loc, callLocation, op); ;
            }
        }
        return null;
    }



    [DebuggerStepThrough, DebuggerHidden]
    private void Generate(SourceProductionContext ctx, (Compilation Compilation, ImmutableArray<SourceState> Nodes) state) {
        try {
            var isCsharp = state.Compilation.Options is CSharpCompilationOptions cSharp;

            var codeWriter = new CodeWriter().Append("#nullable enable").NewLine()
           .Append("namespace ")
            .Append("Kinetic2.Interceptor")
            //.Append("Microsoft.AspNetCore.Http.Generated")
           .Append(" // interceptors must be in a known namespace").Indent().NewLine()
           .Append("[System.CodeDom.Compiler.GeneratedCode(\"Kinetic2\", \"1.0.0\")]").NewLine()
           .Append("[System.Diagnostics.DebuggerStepThrough]").NewLine()
           .Append("file static class GeneratedInterceptors").Indent().NewLine();

            foreach (InterceptSourceState node in state.Nodes) {
                var state2 = node.State;
                var loc = node.Location;
                var callLoc = node.CallLoc!;

                if (callLoc.Kind == LocationKind.SourceFile) {
                    codeWriter.Append("[System.Diagnostics.DebuggerHidden]").NewLine();
                    codeWriter.Append("[System.Diagnostics.DebuggerStepThrough]").NewLine();
                    codeWriter.Append("[System.Runtime.CompilerServices.InterceptsLocation(\"" + callLoc.SourceTree!.FilePath.Replace("\\", "\\\\") + "\", " + (callLoc.GetLineSpan().StartLinePosition.Line + 1) + ", " + (callLoc.GetLineSpan().StartLinePosition.Character + 1) + ")]").NewLine();
                    codeWriter.Append("internal static async void InterceptsMarkerMethod(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection unused) { /* this is an empty marker method */ }").NewLine();
                }
                //else
                //    codeWriter.Append("[System.Runtime.CompilerServices.InterceptsLocation(\"" + "D:\\\\MyTools\\\\Kinetic2\\\\Kinetic2\\\\Program.cs" + "\", 89, 21)] " +
                //        "internal static async ValueTask IntercerptsSendEmailAsync(this EmailService sd, string to, string subject, string body) { " +
                //        "await sd.SendEmailAsync(to, subject, body);" +
                //        "System.Console.WriteLine(\"Email sent\");" +
                //        "" +
                //        "").Append("}").NewLine();
            }
            codeWriter.Outdent().NewLine();
            codeWriter.Outdent().NewLine();

            var interceptsLocationWriter = new InterceptorsLocationAttributeWriter(codeWriter);
            interceptsLocationWriter.Write(state.Compilation);

            ctx.AddSource((state.Compilation.AssemblyName ?? "package") + ".generated.cs", codeWriter.ToString());
        }
        catch /*(Exception ex)*/ {
            //ctx.ReportDiagnostic(Diagnostic.Create(DiagnosticsBase., null, ex.Message, ex.StackTrace));
        }
    }
}


internal class InterceptSourceState(Location loc, Location? _callLocation, IInvocationOperation _state) : SourceState(loc) {

    public IInvocationOperation? State => _state;
    public Location? CallLoc => _callLocation;

}