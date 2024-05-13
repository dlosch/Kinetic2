#define ALLOW_DEFAULT_IFACE_WITH_ATTR
using Kinetic2.Analyzers.Logic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Diagnostics;
using static Kinetic2.Analyzers.Logic.DependencyInjectionExtensionMethods;
using static Kinetic2.Analyzers.Logic.GenericExtensionMethods;

namespace Kinetic2.Analyzers;
/*
 * 
 */
[Generator(LanguageNames.CSharp), DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class K2PollyGenerator : InterceptorGeneratorBase {
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticsBase.All<Diagnostics>();

    public override void Initialize(IncrementalGeneratorInitializationContext context) {
        var nodes = context.SyntaxProvider.CreateSyntaxProvider(PreFilter, Parse)
                        .Where(x => x is not null)
                        .Select((x, _) => x!);

        var combined = context.CompilationProvider.Combine(nodes.Collect());

        context.RegisterImplementationSourceOutput(combined, Generate);
    }

    static string[] methodNames = ["AddTransient", "AddSingleton", "AddScoped", "AddKeyedTransient", "AddKeyedScoped", "AddKeyedSingleton", "Add", "Insert"];
    static string[] methodNamesPolly = ["AddResiliencePipeline"];

    static HashSet<string> _affectedMethodNamesDiContainer = new HashSet<string>(methodNames);
    static HashSet<string> _affectedMethodNamesPolly = new HashSet<string>(methodNamesPolly);

    internal bool PreFilter(SyntaxNode node, CancellationToken cancellationToken) {
        if (node is InvocationExpressionSyntax ie && ie.ChildNodes().FirstOrDefault() is MemberAccessExpressionSyntax ma) {
            bool IsCandidate(MemberAccessExpressionSyntax memberAccessExpressionSyntax) {
                if (memberAccessExpressionSyntax?.Name?.Identifier.Text is null) return false;

                return
                    _affectedMethodNamesDiContainer.Contains(memberAccessExpressionSyntax.Name.Identifier.Text)
                    || _affectedMethodNamesPolly.Contains(memberAccessExpressionSyntax.Name.Identifier.Text)
                    ;
            }

            return IsCandidate(ma);
        }
        else if (node is MethodDeclarationSyntax mds) {
            // todo improve check
            return mds.AttributeLists.Any(als => als.Attributes.Any(IMethodSymbolExtensions.FilterByResiliencePipelineAttribute));
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
        try {
            // methods with attribute in class or interface
            if (ctx.Node is MethodDeclarationSyntax mds
                && ctx.SemanticModel.GetOperation(mds) is IMethodBodyOperation mi
                && mds.AttributeLists.Any(als => als.Attributes.Any())
                ) {
                var symbol = ctx.SemanticModel.GetDeclaredSymbol(mds, CancellationToken.None);

                if (symbol is null) {
                    var diagnostic = Diagnostic.Create(Diagnostics.UnableToResolveSymbol, ctx.Node.GetLocation(), mds.ToString());
                    return new DiagnosticsSourceState(ctx.Node, diagnostic);
                }

                var process = symbol.GetAttributes().Any(IMethodSymbolExtensions.IsResiliencePipelineAttribute);
                if (process) {
                    var resiliencePipelineAttribute = symbol.GetResiliencePipelineAttribute()!;

                    /*
                     * DI registration as Type
                     *  - type is sealed -> error (why doesnt delegation work?) bc the injected usages will be OriginalType, not DerivedType. 
                     *       We must derive, all affected methods must be virtual non sealed
                     *  - derive type
                     * 
                     * DI Registration as interface
                     *  replace the implementation part of the registration with the new class
                     *      new class: Implement iface
                     *      can delegate to original class  
                     *      
                     *  no method with the marker may be sealed
                     * */
#if !ALLOW_DEFAULT_IFACE_WITH_ATTR
                    if (symbol.ContainingType.TypeKind == TypeKind.Interface) {
                        var diagnostic = Diagnostic.Create(Diagnostics.DefaultImplementationOnInterfaceNotSupported, ctx.Node.GetLocation(), symbol.Name, symbol.ContainingType.QualifiedTypeName());
                        return new DiagnosticsSourceState(ctx.Node, diagnostic);
                    }
#endif
                    if (symbol.ReturnType is { } && IsAsyncEnumerable(symbol.ReturnType)) {
                        var diagnostic = Diagnostic.Create(Diagnostics.AsyncEnumerableNotSupported, ctx.Node.GetLocation(), symbol.Name, symbol.ContainingType.QualifiedTypeName());
                        return new DiagnosticsSourceState(ctx.Node, diagnostic);
                    }

                    if (symbol.ReturnType is null || !IsTaskOfStringTypeStr(symbol.ReturnType)) {
                        var diagnostic = Diagnostic.Create(Diagnostics.MethodMustBeAwaitableAndReturnValueTaskOrValueTaskOfT, ctx.Node.GetLocation(), symbol.Name, symbol.ContainingType.QualifiedTypeName());
                        return new DiagnosticsSourceState(ctx.Node, diagnostic);
                    }

                    // for interfaces, we create a new type which implements the interface and delegates all calls to an instance of the original class.
                    if (IsInterfaceMethod(symbol, out var iface)) {
                        return new MethodDefinitionSourceState(symbol, symbol.ContainingType, iface, resiliencePipelineAttribute, ctx.Node.GetLocation());
                    }

                    // if the method is not an interface method, a) class must not be sealed, b) method must not be sealed c) method must be virtual
                    if (symbol.IsSealed) {
                        var diagnostic = Diagnostic.Create(Diagnostics.MethodMayNotBeSealed, ctx.Node.GetLocation(), symbol.Name, symbol.ContainingType.QualifiedTypeName());
                        return new DiagnosticsSourceState(ctx.Node, diagnostic);
                    }

                    // only true if derived not if delegation is used
                    if (!symbol.IsVirtual) {
                        // I am not sure whether it must be virtual
                        var diagnostic = Diagnostic.Create(Diagnostics.MethodMustBeVirtual, ctx.Node.GetLocation(), symbol.Name, symbol.ContainingType.QualifiedTypeName());
                        return new DiagnosticsSourceState(ctx.Node, diagnostic);
                    }

                    if (symbol.ContainingType.IsSealed) {
                        var diagnostic = Diagnostic.Create(Diagnostics.DeclaringTypeMayNotBeSealedOfNoInterface, ctx.Node.GetLocation(), symbol.Name, symbol.ContainingType.QualifiedTypeName());
                        return new DiagnosticsSourceState(ctx.Node, diagnostic);
                    }

                    return new MethodDefinitionSourceState(symbol, symbol.ContainingType, null, resiliencePipelineAttribute, ctx.Node.GetLocation());
                }
            }

            // DI registrations
            if (ctx.Node is InvocationExpressionSyntax ie && ctx.SemanticModel.GetOperation(ie) is IInvocationOperation op) {
                // Insert 
                if (op.TargetMethod.Name == "Add"
                    && (op.TargetMethod.ContainingType.QualifiedTypeName() == "Microsoft.Extensions.DependencyInjection.ServiceCollection")
                    || (op.TargetMethod.ContainingType.QualifiedTypeName() == "System.Collections.Generic.ICollection<Microsoft.Extensions.DependencyInjection.ServiceDescriptor>")
                    ) {
                    var diagnostic = Diagnostic.Create(Diagnostics.UnsupportedDiRegistrationMethodCalled, ctx.Node.GetLocation(), op.TargetMethod.Name, op.TargetMethod.ContainingType.QualifiedTypeName());
                    return new DiagnosticsSourceState(ctx.Node, diagnostic);
                }

                var methodName = op.TargetMethod.Name;
                if (_affectedMethodNamesDiContainer.Contains(op.TargetMethod.Name)) {

                    var hasImplementationFactory = HasInstanceFactoryParameter(op.TargetMethod, ctx.SemanticModel.Compilation, out var instanceType);
                    var hasInstanceParameter = false;
                    hasInstanceParameter = (!hasImplementationFactory && op.TargetMethod.Parameters.Any() && op.TargetMethod.Name.IndexOf("Singleton") > 0);

                    // todo I believe keyed doesnt generate properly
                    var isKeyed = methodName.Contains("Keyed");

                    var containingMethod = GetContainingMethod(ie);
                    var closestNamespace = containingMethod is null ? default(string) : ctx.SemanticModel.GetDeclaredSymbol(containingMethod)?.ContainingType?.Namespace();

                    static MethodDeclarationSyntax? GetContainingMethod(InvocationExpressionSyntax invocation) {
                        var parent = invocation.Parent;

                        while (parent != null) {
                            if (parent is MethodDeclarationSyntax methodDeclaration) {
                                return methodDeclaration;
                            }

                            parent = parent.Parent;
                        }

                        return null;
                    }


                    var dirReg = default(DiRegistrationSourceState);
                    if (op.TargetMethod.IsGenericMethod) {
                        if (op.TargetMethod.TypeArguments.Length == 2) {
                            if ((op.TargetMethod.TypeArguments[0] is not INamedTypeSymbol) || (op.TargetMethod.TypeArguments[1] is not INamedTypeSymbol)) {
                                var diagnostic = Diagnostic.Create(Diagnostics.UnsupportedTypeInDiRegistration2, ctx.Node.GetLocation(), op.TargetMethod.Name, op.TargetMethod.ContainingType.QualifiedTypeName(), op.TargetMethod.TypeArguments[0], op.TargetMethod.TypeArguments[1]);
                                return new DiagnosticsSourceState(ctx.Node, diagnostic);
                            }

                            dirReg = new DiRegistrationSourceState(ie, op, closestNamespace, hasImplementationFactory, hasInstanceParameter, isKeyed, (INamedTypeSymbol)op.TargetMethod.TypeArguments[0], (INamedTypeSymbol)op.TargetMethod.TypeArguments[1]);
                        }
                        else if (op.TargetMethod.TypeArguments.Length == 1) {
                            if (op.TargetMethod.TypeArguments[0] is not INamedTypeSymbol) {
                                var diagnostic = Diagnostic.Create(Diagnostics.UnsupportedTypeInDiRegistration, ctx.Node.GetLocation(), op.TargetMethod.Name, op.TargetMethod.ContainingType.QualifiedTypeName(), op.TargetMethod.TypeArguments[0]);
                                return new DiagnosticsSourceState(ctx.Node, diagnostic);
                            }

                            dirReg = new DiRegistrationSourceState(ie, op, closestNamespace, hasImplementationFactory, hasInstanceParameter, isKeyed, default, (INamedTypeSymbol)op.TargetMethod.TypeArguments[0]);
                        }
                    }
                    else {
                        var diagnostic = Diagnostic.Create(Diagnostics.UnsupportedDiRegistrationMethodCalled, ctx.Node.GetLocation(), op.TargetMethod.Name, op.TargetMethod.ContainingType.QualifiedTypeName());
                        return new DiagnosticsSourceState(ctx.Node, diagnostic);
                    }
                    if (dirReg is { }) return dirReg;
                }
            }
        }
        catch (Exception) {
            Debugger.Launch();
            //throw;
        }
        return null;

    }

    const bool _onlyGenerateTypesWithDiRegistration = false; //true;

    private void Generate(SourceProductionContext ctx, (Compilation Compilation, ImmutableArray<SourceState> Nodes) state) {
        try {
            foreach (DiagnosticsSourceState disg in state.Nodes.Where(node => node is DiagnosticsSourceState diagsrcState)) {
                ctx.ReportDiagnostic(disg.Diagnostic);
            }

            // there could be duplicate DI regs nested within if else ... we cannot check that
            // must be pair
            var interfaceTypes = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol?>>(SymbolEqualityComparer.Default);
            // second one may be empty
            var implementationTypes = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol?>>(SymbolEqualityComparer.Default);
            foreach (var dependencyInjectionRegistration in state.Nodes.OfType<DiRegistrationSourceState>()) {
                Debug.Assert(dependencyInjectionRegistration.Implementation is not null);

                if (dependencyInjectionRegistration.Interface is { }) {
                    interfaceTypes.Ensure(dependencyInjectionRegistration.Interface, dependencyInjectionRegistration.Implementation!, true);
                    implementationTypes.Ensure(dependencyInjectionRegistration.Implementation!, dependencyInjectionRegistration.Interface, false);
                }
                else {
                    // todo HIGH 20240511 second argument nullable or dependencyInjectionRegistration.Implementation??
                    implementationTypes.Ensure(dependencyInjectionRegistration.Implementation!, null, false);
                }
            }

            var isCsharp = state.Compilation.Options is CSharpCompilationOptions cSharp;
            var derivedTypesWriter = new CodeWriter().Append("#nullable enable").NewLine();
            derivedTypesWriter.Append("using Kinetic2;").NewLine();

            //var interfaceTypeRemappings = new Dictionary<(INamedTypeSymbol, INamedTypeSymbol), string>();
            var typeRemappings = new TypeMappings();

            //Debugger.Launch();

            // registrations with Type and interface
            // logic is
            // a) create a new class that derives from the iface 
            // b) this class takes the original class as ctor parameter
            // c) construction: type will be registered with factory method (I could use FromKeyedService("Giud"))
            // d) implement just interface methods and delegate to original class
            //
            var targetNodes = state.Nodes.OfType<MethodDefinitionSourceState>()
                            .Where(md => md.InterfaceType is { })
                            .GroupBy(md => md.DeclaringType
                            , (IEqualityComparer<INamedTypeSymbol>)SymbolEqualityComparer.Default // todo the comparer changes the type FFS
                            );

            foreach (var typeGroup in targetNodes) {
                // original type -> [1..n] Iface -> [1..n] MappedMethods

                var parentType = typeGroup.Key;

                var interfaces = new Dictionary<INamedTypeSymbol, List<MethodMapping>>(SymbolEqualityComparer.Default);

                // we need one class per interface
                foreach (var item in typeGroup) {
#pragma warning disable CS0162 // unreachable code
                    if (_onlyGenerateTypesWithDiRegistration) {
                        // ensure this pair exists in the di regs, otherwise ignore
                        if (!interfaceTypes.Exists(item.InterfaceType, typeGroup.Key)) continue;
                        if (!implementationTypes.Exists(typeGroup.Key, item.InterfaceType)) continue;
                    }
#pragma warning restore
                    // a single interface may be implemented by multiple classes (but only registered by a single
                    // IInterfaceService -> InterfaceService (Attribute) -> InterfaceServiceImmed -> InterfaceService2 (Attr + DI)
                    // check if this interface has already been processed
                    if (interfaces.ContainsKey(item.InterfaceType!)) continue;

                    var methodMappings = MarkedMethodHelper.EnumerateMappings(typeGroup.Key, item.InterfaceType!).ToList();
                    interfaces.Add(item.InterfaceType!, methodMappings);
                }

                foreach (var @interface in interfaces) {
                    var newTypeName = $"{parentType.Name}__{@interface.Key.Name}__K2p";
                    // todo 
                    typeRemappings.Add(@interface.Key, parentType, newTypeName);

                    derivedTypesWriter.Append($"[System.CodeDom.Compiler.GeneratedCode(\"Kinetic2\", \"1.0.0\")]").NewLine();
                    derivedTypesWriter.Append($"internal sealed class {newTypeName} : {@interface.Key.QualifiedTypeName()}").NewLine();
                    derivedTypesWriter.Indent().NewLine();

                    // ctor
                    //derivedTypesWriter.Append($"private readonly static global::System.Diagnostics.ActivitySource _sActivitySource = new global::System.Diagnostics.ActivitySource(nameof({newTypeName}));").NewLine();
                    derivedTypesWriter.Append($"private readonly global::System.IServiceProvider _serviceProvider;").NewLine();
                    derivedTypesWriter.Append($"private readonly {@interface.Key.QualifiedTypeName()} _instance;").NewLine();

                    derivedTypesWriter.Append($"internal {newTypeName}(global::System.IServiceProvider serviceProvider, {parentType.QualifiedTypeName()} instance)").NewLine();
                    derivedTypesWriter.Indent().NewLine();
                    derivedTypesWriter.Append($"_serviceProvider = serviceProvider; _instance = ({@interface.Key.QualifiedTypeName()})instance;").NewLine();

                    derivedTypesWriter.Outdent().NewLine();

                    foreach (var mappedMethod in @interface.Value) {

                        var typeMatches = SymbolEqualityComparer.Default.Equals(
                            mappedMethod.mappedMethod?.ContainingType,
                            typeGroup.Key);

                        //Debug.Assert(mappedMethod.mappedMethod is null || typeMatches);


                        switch (mappedMethod.ImplementationType) {
                            case ImplementationType.Intercept:
                                // todo HIGH 20240511 WTF?
                                var attribute = mappedMethod.AttributeEffective!; 
                                Trace.Assert(attribute is { }); // for intercept, we must have the attribute

                                // this is always a interface - don't seal
                                derivedTypesWriter.Append($"public /*{(mappedMethod.mappedMethod?.IsOverride ?? false ? "sealed" : "")}*/ {mappedMethod.interfaceMethod.Signature()}").NewLine();

                                if (mappedMethod.interfaceMethod.ReturnType.IsAsyncVoid()) derivedTypesWriter.Append($"=>  ResilienceExtensions.ExecuteResiliencePipeline<{newTypeName}>(_serviceProvider, \"{attribute!.PipelineName}\", _ =>  _instance.{mappedMethod.interfaceMethod.Invocation()}, CancellationToken.None);").NewLine();
                                else {
                                    var returnType = mappedMethod.interfaceMethod.ReturnType.GetReturnType();
                                    derivedTypesWriter.Append($"=>  ResilienceExtensions.ExecuteResiliencePipeline<{newTypeName}, {returnType!.QualifiedTypeName()}>(_serviceProvider, \"{attribute!.PipelineName}\", _ =>  _instance.{mappedMethod.interfaceMethod.Invocation()}, CancellationToken.None);").NewLine();
                                }
                                break;
                            case ImplementationType.DelegateToBase:
                                derivedTypesWriter.Append($"public /*{(mappedMethod.mappedMethod?.IsOverride ?? false ? "sealed" : "")}*/ {mappedMethod.interfaceMethod.Signature()} => _instance.{mappedMethod.interfaceMethod.Invocation()};").NewLine();
                                break;

                            case ImplementationType.SkipDefaultImplementationInInterface:
                            default:
                                break;
                        }
                    }

                    derivedTypesWriter.Outdent().NewLine();
                }
            }

#if NO_INTERFACE
            // types which are registered without interface must be 
            // technically we could just use interceptors and inject async local context which has the IServiceProvider ... no no no no cannot do that
            // a) non sealed (we must derive)
            // b) marked methods must be virtual
            // we then register the type AddTransient<OriginalType, DerivedType>()
            // hence DI works
            foreach (var typeGroup in state.Nodes.OfType<MethodDefinitionSourceState>()
            .Where(md => md.InterfaceType is null)
            .GroupBy(md => md.DeclaringType)) {

                var parentType = typeGroup.Key;
                var newTypeName = parentType.Name + "K2p";

                typeRemappings.Add(parentType/*.QualifiedTypeName()*/, newTypeName);

                if (parentType.TypeKind == TypeKind.Interface) {
                    // we would need to derive types here ...
                    derivedTypesWriter.Append($"// not supported currently internal interface {newTypeName} : {parentType.QualifiedTypeName()}").Indent()
                        //.NewLine()
                        ;
                    // todo 
                    derivedTypesWriter.Outdent().NewLine();
                }
                else if (parentType.TypeKind == TypeKind.Class) {
                    derivedTypesWriter.Append($"internal sealed class {newTypeName} : {parentType.QualifiedTypeName()}").Indent().NewLine();

                    derivedTypesWriter.Append($"private readonly System.IServiceProvider @spk2p;").NewLine();

                    foreach (var ctor in parentType.Constructors) {
                        if (ctor.IsStatic) continue;
                        derivedTypesWriter.Append($"// {ctor.ToString()}").NewLine();

                        if (
                            //ctor.DeclaredAccessibility.HasFlag(Accessibility.Protected) || 
                            ctor.DeclaredAccessibility.HasFlag(Accessibility.Public)
                            ) {

                            var idx = 1;
                            var sparams = "";
                            var sbase = "";
                            foreach (var ps in ctor.Parameters) {
                                sparams += $", {ps.Type.QualifiedTypeName()} p{idx}";
                                sbase += idx == 1 ? $"p{idx}" : $", p{idx}";
                                idx++;
                            }

                            derivedTypesWriter.Append($"public {newTypeName}(System.IServiceProvider p0{sparams}) : base({sbase})").Indent().NewLine();
                            derivedTypesWriter.Append($"@spk2p = p0;").Outdent().NewLine();
                        }
                    }

                    foreach (var method in typeGroup) {

                        var attr = method.Method.GetAttributes().FirstOrDefault(at => at.AttributeClass.Name == "ResiliencePipelineAttribute");
                        var pipelineName = attr.ConstructorArguments[0].Value as string;


                        var idx = 1;
                        var sparams = "";
                        var sbase = "";
                        foreach (var ps in method.Method.Parameters) {
                            sparams += $", {ps.Type.QualifiedTypeName()} p{idx}";
                            sbase += idx == 1 ? $"p{idx}" : $", p{idx}";
                            idx++;
                        }

                        derivedTypesWriter.Append($"{method.Method.FullSignature().Replace("virtual", "override async")}").Indent().NewLine();

                        derivedTypesWriter
                            .Append($"var @pip = default(Polly.ResiliencePipeline);").NewLine()
                            .Append($"var @log = default(Microsoft.Extensions.Logging.ILogger<{newTypeName}>);").NewLine()

                            .Append($"if (@spk2p is {{}})").Indent().NewLine()
                            .Append($"var @p = @spk2p.GetRequiredService<Polly.Registry.ResiliencePipelineProvider<string>>();").NewLine()
                            .Append($"@pip = @p.GetPipeline(\"{pipelineName}\");").NewLine()
                            .Append($"@log = @spk2p.GetService<ILogger<{newTypeName}>>();").NewLine()
                            .Outdent().NewLine();

                        // declare return if any

                        // cancellationToken?
                        static bool TryGetCancellationToken(ImmutableArray<IParameterSymbol> parameters, out string? parameterName) {
                            parameterName = null;
                            if (parameters.Length == 0) return false;

                            var last = parameters.Last();
                            if (last.Type.QualifiedTypeName() == "System.Threading.CancellationToken") {
                                parameterName = last.Name;
                                return true;
                            }
                            return false;
                        }

                        var hasReturnValue = method.Method.ReturnsVoid is false && method.Method.ReturnType is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType;

                        var cancellationTokenText = "System.Threading.CancellationToken.None";
                        if (TryGetCancellationToken(method.Method.Parameters, out var name)) {
                            cancellationTokenText = name;
                        }

                        var isFirst = true;
                        var parameterNames = new StringBuilder();
                        foreach (var item in method.Method.Parameters) {
                            derivedTypesWriter.Append($"// p: {item.Kind}: {item.Name} ({item.Type})").NewLine();
                            if (!isFirst) {
                                parameterNames.Append(", ");
                            }
                            isFirst = false;
                            parameterNames.Append(item.Name);
                        }

                        derivedTypesWriter.Append($"try").Indent().NewLine();

                        derivedTypesWriter.Append($"if (@pip is {{}})").Indent().NewLine()
                         .Append($"@log?.LogInformation(\"Executing resilience pipeline {{resiliencePipelineName}}\", \"{pipelineName}\");").NewLine()
                         .Append($"{(hasReturnValue ? "return" : "")} await @pip.ExecuteAsync( async (@tokenName) => await base.{method.Method.Name}({parameterNames.ToString()}), {cancellationTokenText});").NewLine()
                         .Outdent().NewLine()

                         .Append("else").Indent().NewLine()
                          .Append($"@log?.LogWarning(\"Failed to resolve resilience pipeline with name '{{resiliencePipelineName}}'. Executing without resilience.\", \"{pipelineName}\");").NewLine()

                        .Append($"{(hasReturnValue ? "return" : "")} await base.{method.Method.Name}({parameterNames.ToString()});").NewLine()
                        //.Append($"{(hasReturnValue ? "return default;" : "")}").NewLine()
                        .Outdent().NewLine();

                        derivedTypesWriter.Outdent().NewLine();

                        derivedTypesWriter.Append($"catch(System.Exception xcptn)").Indent().NewLine();
                        derivedTypesWriter.Append("@log?.LogError(xcptn, \"An unfortunate error occurred\");").NewLine();
                        derivedTypesWriter.Append($"throw;").Outdent().NewLine();

                        derivedTypesWriter.Append($"if (@pip is {{}})").Indent().NewLine();
                        derivedTypesWriter.Append($"@log?.LogInformation(\"Resilience pipeline {{resiliencePipelineName}} executed without error\", \"{pipelineName}\");").NewLine();
                        derivedTypesWriter.Outdent().NewLine();


                        //foreach (var ps in method.Method.ToDisplayParts(ExtM.FullyQualifiedSymbolFormat)) {
                        //    derivedTypesWriter.Append($"// {ps.Kind}: {ps.Symbol}").NewLine();
                        //}


                        derivedTypesWriter
                            .Append($"//throw new NotImplementedException();").NewLine()
                            .Append($"// base.").NewLine()

                            .Outdent().NewLine();
                    }

                    derivedTypesWriter.Outdent().NewLine();
                }


            }

#endif
            // remap one per
            // methodName_Implementation_Interface?_key?
            #region DI Regs
            var writeDiRegs = true;
            if (writeDiRegs) {
                var classIndex = 0;
                var currentNamespace = default(string);

                var allDiRegs = state.Nodes.OfType<DiRegistrationSourceState>()
                    .OrderBy(diReg => diReg.ContainingNamespace).ToList();

                var targetDiRegs = state.Nodes.OfType<DiRegistrationSourceState>()

                    .Where(dirReg =>
                                    (
                                        (dirReg.Interface is { } && typeRemappings.ContainsInterface(dirReg.Implementation, dirReg.Interface))
                                        || typeRemappings.ContainsImplementation(dirReg.Implementation)
                                    )
                        )
                    .OrderBy(diReg => diReg.ContainingNamespace).ToList();



                foreach (var diReg in targetDiRegs) {
                    derivedTypesWriter.Append($"// remap {diReg.Interface?.QualifiedTypeName()}").NewLine();

                    var mappedTypeName = typeRemappings.Get(diReg.Interface, diReg.Implementation);
                    if (mappedTypeName is null) continue;

                    var nmsp = diReg!.Operation!.Type!.ContainingNamespace;

                    if (currentNamespace != diReg.ContainingNamespace) {
                        if (currentNamespace?.Length > 0) {
                            derivedTypesWriter.Outdent().NewLine();
                        }

                        currentNamespace = diReg.ContainingNamespace;
                        if (currentNamespace?.Length > 0) {
                            derivedTypesWriter.Append(currentNamespace).Indent().NewLine();
                        }
                    }

                    derivedTypesWriter.Append($"[System.CodeDom.Compiler.GeneratedCode(\"Kinetic2\", \"1.0.0\")]").NewLine();
                    derivedTypesWriter.Append($"internal static class @K2p_{classIndex++}").Indent().NewLine();

                    if (diReg.Operation.TargetMethod.IsGenericMethod) {

                        derivedTypesWriter.Append($"internal static void {diReg.Operation.TargetMethod.Name}<");

                        var isFirst = true;
                        foreach (var item in diReg.Operation.TargetMethod.TypeArguments) {
                            if (!isFirst) derivedTypesWriter.Append(", ");
                            isFirst = false;
                            derivedTypesWriter.Append($"T{item.Name}");
                        }

                        if (null == mappedTypeName) Debugger.Break();

                        derivedTypesWriter.Append($">(this Microsoft.Extensions.DependencyInjection.IServiceCollection @services, Func<IServiceProvider, {diReg!.Implementation!.QualifiedTypeName()}> func = default)");

                        foreach (var item in diReg.Operation.TargetMethod.TypeArguments) {
                            derivedTypesWriter.Append($" where T{item.Name} : {item.Name}");
                            break;
                        }

                        derivedTypesWriter.Indent().NewLine();

                        // guard against matching this logic against a wrong type.
                        // cannot use a type constrained because they don't allow sealed types :(
                        derivedTypesWriter.Append($"if (!(typeof(T{diReg.Operation.TargetMethod.TypeArguments.Last().Name}) == typeof({diReg.Operation.TargetMethod.TypeArguments.Last().Name}) || typeof(T{diReg.Operation.TargetMethod.TypeArguments.Last().Name}).IsSubclassOf(typeof({diReg.Operation.TargetMethod.TypeArguments.Last().Name}))))").NewLine();
                        derivedTypesWriter.Indent().NewLine();
                        derivedTypesWriter.Append("if (func is {})").NewLine();
                        derivedTypesWriter.Indent().NewLine();
                        derivedTypesWriter.Append($"Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.{diReg.Operation.TargetMethod.Name}<{string.Join(",", diReg.Operation.TargetMethod.TypeArguments.Select(ta => ta.Name))}>(@services, func);").NewLine();
                        derivedTypesWriter.Outdent().NewLine();
                        derivedTypesWriter.Append("else").NewLine();
                        derivedTypesWriter.Indent().NewLine();
                        derivedTypesWriter.Append($"Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.{diReg.Operation.TargetMethod.Name}<{string.Join(",", diReg.Operation.TargetMethod.TypeArguments.Select(ta => ta.Name))}>(@services);").NewLine();
                        derivedTypesWriter.Outdent().NewLine();
                        derivedTypesWriter.Outdent().NewLine();

                        derivedTypesWriter.Append("if (func is {})").NewLine();
                        derivedTypesWriter.Indent().NewLine();
                        derivedTypesWriter.Append($"Func<IServiceProvider, {mappedTypeName}> func2 = (IServiceProvider isp) => {{").NewLine();
                        derivedTypesWriter.Append("var instance = func(isp);").NewLine();
                        derivedTypesWriter.Append($"return new {mappedTypeName}(isp, instance);").NewLine();
                        derivedTypesWriter.Append("};").NewLine();
                        if (diReg.Interface is { }) {
                            derivedTypesWriter.Append($"Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.{diReg.Operation.TargetMethod.Name}<{(diReg.Interface ?? diReg.Implementation).QualifiedTypeName()}, {mappedTypeName}>(@services, func2);").NewLine();
                        }
                        else {
                            derivedTypesWriter.Append($"Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.{diReg.Operation.TargetMethod.Name}<{(diReg.Interface ?? diReg.Implementation).QualifiedTypeName()}, {mappedTypeName}>(@services, func2);").NewLine();
                        }

                        derivedTypesWriter.Outdent().NewLine();
                        derivedTypesWriter.Append("else").NewLine();
                        derivedTypesWriter.Indent().NewLine();

                        var guid = Guid.NewGuid().ToString();
                        derivedTypesWriter.Append($"ServiceCollectionServiceExtensions.AddKeyedTransient<{diReg.Implementation.QualifiedTypeName()}>(@services, \"{guid}\");").NewLine();
                        derivedTypesWriter.Append($"Func<IServiceProvider, {mappedTypeName}> func2 = (IServiceProvider isp) => {{").NewLine();
                        derivedTypesWriter.Append($"var instance = isp.GetKeyedService<{diReg.Implementation.QualifiedTypeName()}>(\"{guid}\");").NewLine();
                        derivedTypesWriter.Append($"return new {mappedTypeName}(isp, instance);").NewLine();
                        derivedTypesWriter.Append($"}};").NewLine();
                        if (diReg.Interface is { }) {
                            derivedTypesWriter.Append($"Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.{diReg.Operation.TargetMethod.Name}<{(diReg.Interface ?? diReg.Implementation).QualifiedTypeName()}, {mappedTypeName}>(@services, func2);").NewLine();
                        }
                        else {
                            derivedTypesWriter.Append($"Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.{diReg.Operation.TargetMethod.Name}<{(diReg.Interface ?? diReg.Implementation).QualifiedTypeName()}, {mappedTypeName}>(@services, func2);").NewLine();
                        }

                        derivedTypesWriter.Outdent().NewLine();
                        derivedTypesWriter.Outdent().NewLine();
                    }
                    else {
                        derivedTypesWriter.Append($"internal static void {diReg.Operation.TargetMethod.Name}()").Indent().NewLine();
                        derivedTypesWriter.Append($"throw new NotImplementedException();").NewLine();
                        derivedTypesWriter.Outdent().NewLine();
                    }

                    derivedTypesWriter.Outdent().NewLine();
                }

                if (currentNamespace?.Length > 0) {
                    derivedTypesWriter.Outdent().NewLine();
                }
            }
            #endregion // DI Regs

            var sb = new CodeWriter().Append("#nullable enable").NewLine();
            //int methodIndex = 0, callSiteCount = 0;
            var interceptsLocationWriter = new InterceptorsLocationAttributeWriter(sb);
            interceptsLocationWriter.Write(state.Compilation);
#if ADD_FROM_RESOURCE
            var resCodeWriter = new CodeWriter();
            resCodeWriter.Append(LoadResilienceExtensionsFromResource());
            ctx.AddSource((state.Compilation.AssemblyName ?? "package") + ".ResilienceExtensions.generated.cs", resCodeWriter.ToString());
#endif
            ctx.AddSource((state.Compilation.AssemblyName ?? "package") + ".generated.cs", sb.ToString());
            ctx.AddSource((state.Compilation.AssemblyName ?? "package") + ".types.generated.cs", derivedTypesWriter.ToString());
            //ctx.ReportDiagnostic(Diagnostic.Create(Diagnostics.CodeGenerated, null, callSiteCount, state.Nodes.Length, methodIndex, 1, 1));
        }
        catch (Exception ex) {
            ctx.ReportDiagnostic(Diagnostic.Create(Diagnostics.UnknownError, null, ex.Message, ex.StackTrace));
        }
    }

#if ADD_FROM_RESOURCE
    static string LoadResilienceExtensionsFromResource(string resName = "Kinetic2.Analyzers.ResilienceExtensions.cs") {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
#endif

    static bool IsInterfaceMethod(IMethodSymbol methodSymbol, /*[NotNullWhen(true)]*/out INamedTypeSymbol? interfaceType) {
        // Check if the method is an explicit interface implementation
        if (methodSymbol.ExplicitInterfaceImplementations.Length > 0) {
            interfaceType = methodSymbol.ExplicitInterfaceImplementations[0].ContainingType;
            return true;
        }

        //Check if the method is an implicit interface implementation
        INamedTypeSymbol containingType = methodSymbol.ContainingType;
        foreach (INamedTypeSymbol interfaceSymbol in containingType.AllInterfaces) {
            foreach (ISymbol interfaceMember in interfaceSymbol.GetMembers()) {
                if (interfaceMember is IMethodSymbol interfaceMethodSymbol) {
                    // todo 20240511 null implementedMember???
                    ISymbol? implementedMember = containingType.FindImplementationForInterfaceMember(interfaceMember);
                    if (SymbolEqualityComparer.Default.Equals(implementedMember, methodSymbol)) {
                        interfaceType = interfaceSymbol;
                        return true;
                    }
                    // Check if the method is an override of an interface method
                    var curMethodSymbol = methodSymbol;
                    do {
                        if (curMethodSymbol.OverriddenMethod is null) break;
                        curMethodSymbol = curMethodSymbol.OverriddenMethod;

                        if (SymbolEqualityComparer.Default.Equals(curMethodSymbol, implementedMember)) {
                            interfaceType = interfaceSymbol;
                            return true;
                        }
                    } while (true);
                }
            }
        }
  
        interfaceType = default;
        return false;
    }
}
