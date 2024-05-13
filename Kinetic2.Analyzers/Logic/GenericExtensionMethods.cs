using Microsoft.CodeAnalysis;

namespace Kinetic2.Analyzers.Logic;

internal static class GenericExtensionMethods {

    internal static bool IsAsyncEnumerable(ITypeSymbol typeSymbol) {
        var genericTypeSymbol = "System.Collections.Generic.IAsyncEnumerable<T>";

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol
            && ((namedTypeSymbol.IsGenericType && (
                                                    StringComparer.Ordinal.Equals(namedTypeSymbol.OriginalDefinition.QualifiedTypeName(), genericTypeSymbol)
                                                    )
                )
            )) {
            return true;
        }

        //Debugger.Launch();
        return false;
    }

    internal static bool IsTaskOfStringTypeStr(ITypeSymbol typeSymbol) {
        var taskGenericTypeSymbol = "System.Threading.Tasks.Task<TResult>";
        var taskTypeSymbol = "System.Threading.Tasks.Task";
        var valueTaskTypeSymbol = "System.Threading.Tasks.ValueTask";
        var valueTaskGenericTypeSymbol = "System.Threading.Tasks.ValueTask<TResult>";

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol
            && (
            (!namedTypeSymbol.IsGenericType && (
                                                    StringComparer.Ordinal.Equals(namedTypeSymbol.QualifiedTypeName(), taskTypeSymbol)
                                                    || StringComparer.Ordinal.Equals(namedTypeSymbol.QualifiedTypeName(), valueTaskTypeSymbol)
                                                    )
                )
            || (namedTypeSymbol.IsGenericType && (
                                                    StringComparer.Ordinal.Equals(namedTypeSymbol.OriginalDefinition.QualifiedTypeName(), taskGenericTypeSymbol)
                                                    || StringComparer.Ordinal.Equals(namedTypeSymbol.OriginalDefinition.QualifiedTypeName(), valueTaskGenericTypeSymbol)
                                                    )
                )
            )) {
            return true;
        }

        //Debugger.Launch();
        return false;
    }

    internal static INamedTypeSymbol? GetReturnType(this ITypeSymbol typeSymbol) {
        var taskGenericTypeSymbol = "System.Threading.Tasks.Task<TResult>";
        var valueTaskGenericTypeSymbol = "System.Threading.Tasks.ValueTask<TResult>";

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol
&& (namedTypeSymbol.IsGenericType && (
                                                    StringComparer.Ordinal.Equals(namedTypeSymbol.OriginalDefinition.QualifiedTypeName(), taskGenericTypeSymbol)
                                                    || StringComparer.Ordinal.Equals(namedTypeSymbol.OriginalDefinition.QualifiedTypeName(), valueTaskGenericTypeSymbol)
                                                    )
                )
            ) {
            return namedTypeSymbol.TypeArguments[0] as INamedTypeSymbol;
        }

        return default;
    }

    internal static bool IsAsyncVoid(this ITypeSymbol typeSymbol) {
        var taskTypeSymbol = "System.Threading.Tasks.Task";
        var valueTaskTypeSymbol = "System.Threading.Tasks.ValueTask";

        if (typeSymbol is INamedTypeSymbol namedTypeSymbol
            && (
            (!namedTypeSymbol.IsGenericType && (
                                                    StringComparer.Ordinal.Equals(namedTypeSymbol.QualifiedTypeName(), taskTypeSymbol)
                                                    || StringComparer.Ordinal.Equals(namedTypeSymbol.QualifiedTypeName(), valueTaskTypeSymbol)
                                                    )
                )
            )) {
            return true;
        }

        //Debugger.Launch();
        return false;
    }

}

internal static class DependencyInjectionExtensionMethods {
    internal static bool HasInstanceFactoryParameter(IMethodSymbol targetMethod, Compilation compilation, /*[NotNullWhen(true)]*/ out ITypeSymbol? instance) {
        // todo does this need bulletproofing?
        INamedTypeSymbol funcTypeSymbol = compilation.GetTypeByMetadataName("System.Func`2")!;
        INamedTypeSymbol serviceProviderTypeSymbol = compilation.GetTypeByMetadataName("System.IServiceProvider")!;

        instance = default;

        foreach (var parameterSymbol in targetMethod.Parameters) {
            if (parameterSymbol.Type is INamedTypeSymbol parameterTypeSymbol &&
                parameterTypeSymbol.TypeArguments.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(parameterTypeSymbol.OriginalDefinition, funcTypeSymbol) &&
                SymbolEqualityComparer.Default.Equals(parameterTypeSymbol.TypeArguments[0], serviceProviderTypeSymbol)) {
                instance = parameterTypeSymbol.TypeArguments[1];
                return true;
            }
        }

        return false;
    }

}


internal static class CollExtensions {
    internal static bool Exists(this Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> interfaceTypes, INamedTypeSymbol ifaceIn, INamedTypeSymbol impl) {
        if (interfaceTypes.TryGetValue(ifaceIn, out var mappedTypes)) {
            if (mappedTypes.Contains(impl)) {
                // impl may be a subclass of a mapped type

                return true;
            }

            if (mappedTypes.Any(mappedType => IsDerivedFrom(mappedType, impl))) {
                return true;
            }
        }


        // interface may be a base type
        var iface = ifaceIn;
        do {
            iface = iface.BaseType;

            if (iface == null) {
                return false;
            }

            if (interfaceTypes.TryGetValue(iface, out var mappedTypes2)) {
                if (mappedTypes.Contains(impl)) {
                    return true;
                }

                if (mappedTypes.Any(mappedType => IsDerivedFrom(mappedType, impl))) {
                    return true;
                }
            }
        } while (true);
    }

    private static bool IsDerivedFrom(INamedTypeSymbol derivedType, INamedTypeSymbol baseType) {
        var currentBaseType = derivedType.BaseType;

        while (currentBaseType != null) {
            if (SymbolEqualityComparer.Default.Equals(currentBaseType, baseType)) {
                return true;
            }

            currentBaseType = currentBaseType.BaseType;
        }

        return false;
    }


    internal static bool Ensure(this Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol?>> interfaceTypes, INamedTypeSymbol iface, INamedTypeSymbol? impl, bool requireImpl = false) {
        if (interfaceTypes.TryGetValue(iface, out var mappedImplementations)) {
            return mappedImplementations.Add(impl);
        }
        else {
            var map = new HashSet<INamedTypeSymbol?>(SymbolEqualityComparer.Default);
            map.Add(impl /*as INamedTypeSymbol*/);
            interfaceTypes.Add(iface/*as INamedTypeSymbol*/, map);
            return true;
        }
    }
}
