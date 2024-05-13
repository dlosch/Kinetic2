using Microsoft.CodeAnalysis;
using System.Diagnostics;

namespace Kinetic2.Analyzers;

internal class TypeMappings {

    internal sealed class EqComparer : IEqualityComparer<INamedTypeSymbol?> {
        internal static readonly EqComparer Instance = new EqComparer();
        public bool Equals(INamedTypeSymbol? x, INamedTypeSymbol? y) => x is not null && y is not null && 0 == string.Compare(x.QualifiedTypeName(), y.QualifiedTypeName(), StringComparison.Ordinal);
        public int GetHashCode(INamedTypeSymbol? obj) => obj is null ? 0 : obj.QualifiedTypeName().GetHashCode();
    }

    private Dictionary<INamedTypeSymbol, string> _dictDirectMappings = new Dictionary<INamedTypeSymbol, string>(EqComparer.Instance);
    private Dictionary<INamedTypeSymbol, Dictionary<INamedTypeSymbol?, string>> _dict = new Dictionary<INamedTypeSymbol, Dictionary<INamedTypeSymbol?, string>>(EqComparer.Instance);
    private HashSet<INamedTypeSymbol> _interfaces = new(EqComparer.Instance);

    internal bool Add(INamedTypeSymbol originalSymbol, string newSymbol) {
        if (_dictDirectMappings.TryGetValue(originalSymbol, out var mapping)) {
            Debug.Assert(string.Equals(mapping, newSymbol));
            return true;
        }
        else {
            _dictDirectMappings.Add(originalSymbol, newSymbol);
        }
        return true;
    }

    internal bool Add(INamedTypeSymbol interfaceType, INamedTypeSymbol originalSymbol, string newSymbol) {
        _interfaces.Add(interfaceType);

        if (_dict.TryGetValue(originalSymbol, out var mappings)) {
            if (mappings.TryGetValue(interfaceType, out var remapping)) return string.Equals(remapping, newSymbol);
            else mappings.Add(interfaceType, newSymbol);
        }
        else {
            _dict.Add(originalSymbol, new Dictionary<INamedTypeSymbol?, string>(EqComparer.Instance) { { interfaceType, newSymbol } });
        }
        return true;
    }

    internal bool ContainsImplementation(INamedTypeSymbol implementation) {
        var first = _dict.ContainsKey(implementation);
        var second = _dictDirectMappings.ContainsKey(implementation);

        return first || second;
    }

    internal bool ContainsInterface(INamedTypeSymbol @interface) {
        return _interfaces.Contains(@interface);
    }

    internal string? Get(INamedTypeSymbol? @interface, INamedTypeSymbol? implementation) {
        if (implementation is null) throw new ArgumentNullException();

        if (@interface is null) return _dictDirectMappings[implementation];

        if (_dict.TryGetValue(implementation, out var mappings)) {
            if (mappings.TryGetValue(@interface, out var newType)) return newType;
        }
        else {
            var v2 = implementation;

            do {
                v2 = v2.BaseType;
                if (v2 is null) break;

                if (_dict.TryGetValue(v2, out var mappings2)) {
                    if (mappings2.TryGetValue(@interface, out var newType)) return newType;
                }

            } while (true);
        }

        return null;
    }

    internal bool ContainsInterface(INamedTypeSymbol? implementation, INamedTypeSymbol @interface) {
        return _interfaces.Contains(@interface);
    }
}
