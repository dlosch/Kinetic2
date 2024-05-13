#define ALLOW_DEFAULT_IFACE_WITH_ATTR
using Microsoft.CodeAnalysis;
using System.Diagnostics;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Kinetic2.Tests")]

namespace Kinetic2.Analyzers.Logic;

internal enum ImplementationType {
    //Default = DelegateToBase,
    DelegateToBase = 0x0,

    SkipDefaultImplementationInInterface = 0x2,

    Intercept = 0x1,
}

internal record struct MethodMapping(IMethodSymbol interfaceMethod, IMethodSymbol? mappedMethod, ImplementationType ImplementationType, ResiliencePipelineAttribute? AttributeEffective /*= default*/, int pos = -1);

internal static class MarkedMethodHelper {
    internal static Dictionary<IMethodSymbol, IMethodSymbol?> GetInterfaceMethodMappingsWithBase(INamedTypeSymbol classSymbol, INamedTypeSymbol interfaceSymbol) {
        var mappings = new Dictionary<IMethodSymbol, IMethodSymbol?>(SymbolEqualityComparer.Default);

        // Start with the current class and move up the inheritance chain
        var currentClassSymbol = classSymbol;
        while (currentClassSymbol != null) {
            //foreach (var interfaceSymbol in currentClassSymbol.Interfaces) 
            {
                foreach (var interfaceMethod in interfaceSymbol.GetMembers().OfType<IMethodSymbol>()) {
                    if (mappings.ContainsKey(interfaceMethod)) continue;

                    var classMethod = currentClassSymbol.FindImplementationForInterfaceMember(interfaceMethod) as IMethodSymbol;
                    if (classMethod != null && classMethod.ContainingType.TypeKind != TypeKind.Interface) {
                        // Only add the mapping if it doesn't already exist. This ensures that methods in derived classes
                        // override methods in base classes.
                        if (!mappings.ContainsKey(interfaceMethod)) {
                            mappings[interfaceMethod] = classMethod;
                        }
                    }
#if ALLOW_DEFAULT_IFACE_WITH_ATTR
                    else if (interfaceMethod.HasMarkerAttribute()) {
                        mappings[interfaceMethod] = default;
                    }
#endif
                }
            }

            // Move up to the base class
            currentClassSymbol = currentClassSymbol.BaseType;
        }

        return mappings;
    }


    internal static IEnumerable<MethodMapping> EnumerateMappings(INamedTypeSymbol currentClass, INamedTypeSymbol/*?*/ interfaceType) {
#if !YIELD
        var list = new List<MethodMapping>(interfaceType.GetMembers().OfType<IMethodSymbol>().Count());
#endif

        var mapping = GetInterfaceMethodMappingsWithBase(currentClass, interfaceType);

        static Dictionary<IMethodSymbol, IMethodSymbol?> FindMostDerivedMappings(Dictionary<IMethodSymbol, IMethodSymbol?> implementationMappings, INamedTypeSymbol currentClass) {
            var derivedMappings = new Dictionary<IMethodSymbol, IMethodSymbol?>(implementationMappings, SymbolEqualityComparer.Default);

            foreach (var kvp in implementationMappings) {
                var method = kvp.Value;
                if (method is null) continue; // default imple on iface with no override

                if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, currentClass)) {
                    var exit = false;
                    foreach (var item in currentClass.GetMembers().OfType<IMethodSymbol>().Where(ms => ms.Name == method.Name)) {
                        if (item?.OverriddenMethod is null) continue;

                        var methodToCheck = item;
                        do {
                            if (SymbolEqualityComparer.Default.Equals(methodToCheck, method)) {
                                derivedMappings[kvp.Key] = item;
                                exit = true;
                                break;
                            }

                        } while ((methodToCheck = methodToCheck!.OverriddenMethod) is { });

                        if (exit) break;
                    }
                }
            }

            return derivedMappings;
        }

        var mappingsForCurrentClass = FindMostDerivedMappings(mapping, currentClass);

        mapping = mappingsForCurrentClass;

        //foreach (var mappingItem in mapping) {
        //    var intefaceMethod = mappingItem.Key;
        //    var currentMethod = mappingItem.Value!;

        static ResiliencePipelineAttribute? FindAttributeInHierarchy(IMethodSymbol intefaceMethod, IMethodSymbol currentMethodIn) {
            // this is stupid
            IMethodSymbol? currentMethod = currentMethodIn;

            var currentAttribute = default(ResiliencePipelineAttribute);
            if (currentMethod is { }) {
                do {
                    if (currentMethod.HasMarkerAttribute()) {
                        currentAttribute = currentMethod.GetResiliencePipelineAttribute();
                        if (currentAttribute is { }) break;
                    }

                    if ((currentMethod = currentMethod?.OverriddenMethod) is null) {
                        break;
                    }
                } while (true);

            }

            if (currentAttribute is { }) {
                // must be from interface
            }
            else {
                if (intefaceMethod.HasMarkerAttribute()) {
                    currentAttribute = intefaceMethod.GetResiliencePipelineAttribute();
                }
            }
            return currentAttribute;
        }

        //}

        //        foreach (var interfaceMethodDefinition in interfaceType.GetMembers().OfType<IMethodSymbol>().Where(ifaceMethodSymbol => ifaceMethodSymbol.HasMarkerAttribute())) {
        //#if YIELD
        //                yield return new MethodMapping(interfaceMethodDefinition, interfaceMethodDefinition, ImplementationType.Intercept, 1);
        //#else
        //            list.Add(new MethodMapping(interfaceMethodDefinition, interfaceMethodDefinition, ImplementationType.Intercept, 1));
        //#endif
        //        }

        foreach (var ifaceMethod in interfaceType.GetMembers().OfType<IMethodSymbol>()
            //.Where(ifaceMethodSymbol => !ifaceMethodSymbol.HasMarkerAttribute())
            ) {

            if (mapping.TryGetValue(ifaceMethod, out var mappedMethod) && mappedMethod is { }) {
                var resiliencyAttribute = FindAttributeInHierarchy(ifaceMethod, mappedMethod);

                if (mappedMethod.HasMarkerAttribute()) {
#if YIELD
                        yield return new MethodMapping(ifaceMethod, mappedMethod, ImplementationType.Intercept, 2);
#else
                    Trace.Assert(resiliencyAttribute is { });
                    if (resiliencyAttribute is { })
                        list.Add(new MethodMapping(ifaceMethod, mappedMethod, ImplementationType.Intercept, resiliencyAttribute, 2));
#endif
                    continue;
                }
                /*
                 * Foreach type vs iface
Enumerate iface methods
Check iface for marker, if stop
Map to implementation 
If implementation is in current class, stop
If implementation is not in current class
A) if it has attribute, stop
B) if no attribute
   Cur type, foreach method, check if override method equals implementation 
   If yes, stop
   If not, perform basic check if it could be a match and walk up overidenmethod to traverse base check each member for attribute

Creation
Wir muessen uns nicht um den constructor kuememrn. Wir registrieren den original type einfach keyed mit einem geheimen key. Dann kann man ihn weder injecten noch anders missbrauche 
                 * */

                // I don't need to find derived types, I only need to start from the DI registered type
                if (!SymbolEqualityComparer.Default.Equals(mappedMethod.ContainingType, currentClass)) {
                    var continueNextMethod = false;
                    foreach (var methodInCurrentType in currentClass.GetMembers().OfType<IMethodSymbol>().Where(currentClassMethod => currentClassMethod.Name == ifaceMethod.Name)) {
                        continueNextMethod = false;

                        var callChainHasMarker = methodInCurrentType.HasMarkerAttribute();
                        var overridenMethod = methodInCurrentType.OverriddenMethod;
                        if (overridenMethod is { }) {
                            do {
                                callChainHasMarker |= overridenMethod.HasMarkerAttribute();
                                if (SymbolEqualityComparer.Default.Equals(overridenMethod, mappedMethod)) {
                                    if (callChainHasMarker) {
                                        continueNextMethod = true;
#if YIELD
                                            yield return new MethodMapping(ifaceMethod, overridenMethod, ImplementationType.Intercept, 3);
#else
                                        Trace.Assert(resiliencyAttribute is { });
                                        list.Add(new MethodMapping(ifaceMethod, overridenMethod, ImplementationType.Intercept, resiliencyAttribute!, 3));
#endif
                                    }

                                    // breaks out of the loop
                                    break;
                                }
                                overridenMethod = overridenMethod.OverriddenMethod;
                            } while (overridenMethod is { });

                            if (continueNextMethod) break;
                        }
                    }

                    if (continueNextMethod) continue;
                }

                // has no marker
#if YIELD
                    yield return new MethodMapping(ifaceMethod, mappedMethod, ImplementationType.DelegateToBase, 4);
#else
                //if (resiliencyAttribute is null) Debugger.Launch();
                //Trace.Assert(resiliencyAttribute is { });
                //if (resiliencyAttribute is { })
                list.Add(new MethodMapping(ifaceMethod, mappedMethod, ImplementationType.DelegateToBase, resiliencyAttribute, 4));
#endif
            }
            else {
                // has no marker and no mapping
                if (!ifaceMethod.IsAbstract) {
                    var resiliencyAttribute = ifaceMethod.GetResiliencePipelineAttribute();
                    var action = ImplementationType.SkipDefaultImplementationInInterface;
                    if (resiliencyAttribute is { }) {
                        action = ImplementationType.Intercept;
                    }
#if YIELD
                        yield return new MethodMapping(ifaceMethod, null, ImplementationType.SkipDefaultImplementationInInterface, 5);
#else
                    //if (resiliencyAttribute is null) Debugger.Launch();
                    //Trace.Assert(resiliencyAttribute is { });
                    //if (resiliencyAttribute is { })
                    list.Add(new MethodMapping(ifaceMethod, null, action, resiliencyAttribute, 5));
#endif
                }
            }
        }
#if !YIELD
        return list;
#endif
    }
}

internal static class SignatureFormatter {
    internal static string Signature(this IMethodSymbol method) => method.ToDisplayString(_fmtSignature);
    internal static string Invocation(this IMethodSymbol method) => method.ToDisplayString(_fmtInvocation);

    private static SymbolDisplayFormat _fmtInvocation = new SymbolDisplayFormat(
                               globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                //genericsOptions:
                //SymbolDisplayGenericsOptions.IncludeTypeParameters ,
                //|
                //SymbolDisplayGenericsOptions.IncludeVariance,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeRef
                    //|
                    //SymbolDisplayMemberOptions.IncludeContainingType
                    ,
                kindOptions:
                    SymbolDisplayKindOptions.IncludeMemberKeyword,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeName |
                    SymbolDisplayParameterOptions.IncludeParamsRefOut,
                //localOptions: SymbolDisplayLocalOptions.IncludeType,
                miscellaneousOptions:

                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static SymbolDisplayFormat _fmtSignature = new SymbolDisplayFormat(
                           globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            genericsOptions:
                SymbolDisplayGenericsOptions.IncludeTypeParameters |
                SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions:
                SymbolDisplayMemberOptions.IncludeParameters |
                SymbolDisplayMemberOptions.IncludeType |
                SymbolDisplayMemberOptions.IncludeRef
                //|
                //SymbolDisplayMemberOptions.IncludeContainingType
                ,
            kindOptions:
                SymbolDisplayKindOptions.IncludeMemberKeyword,
            parameterOptions:
                SymbolDisplayParameterOptions.IncludeName |
                SymbolDisplayParameterOptions.IncludeType |
                SymbolDisplayParameterOptions.IncludeParamsRefOut |
                SymbolDisplayParameterOptions.IncludeDefaultValue,
            //localOptions: SymbolDisplayLocalOptions.IncludeType,
            miscellaneousOptions:

                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);


}