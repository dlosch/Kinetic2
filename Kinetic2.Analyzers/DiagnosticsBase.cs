using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Reflection;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

internal abstract class DiagnosticsBase {
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string category, DiagnosticSeverity severity) => new(id, title, messageFormat, category, severity, true/*, helpLinkUri: RulesRoot + id*/);

    protected static DiagnosticDescriptor GenerationWarning(string id, string title, string messageFormat) => Create(id, title, messageFormat, Category.Generation, DiagnosticSeverity.Warning);

    protected static DiagnosticDescriptor GenerationError(string id, string title, string messageFormat) => Create(id, title, messageFormat, Category.Generation, DiagnosticSeverity.Error);
    protected static DiagnosticDescriptor GenerationInfo(string id, string title, string messageFormat) => Create(id, title, messageFormat, Category.Generation, DiagnosticSeverity.Info);

    protected static DiagnosticDescriptor UsageError(string id, string title, string messageFormat) => Create(id, title, messageFormat, Category.Usage, DiagnosticSeverity.Error);

    protected static DiagnosticDescriptor UsageWarning(string id, string title, string messageFormat) => Create(id, title, messageFormat, Category.Usage, DiagnosticSeverity.Warning);

    private static ImmutableDictionary<string, string>? _idsToFieldNames;
    public static bool TryGetFieldName(string id, out string field) {
        return (_idsToFieldNames ??= Build()).TryGetValue(id, out field!);
        static ImmutableDictionary<string, string> Build()
            => GetAllFor<Diagnostics>()
            .Distinct()
            .ToImmutableDictionary(x => x.Value.Id, x => x.Key, StringComparer.Ordinal, StringComparer.Ordinal);
    }

    public static ImmutableArray<DiagnosticDescriptor> All<T>() where T : DiagnosticsBase => Cache<T>.All;

    private static IEnumerable<KeyValuePair<string, DiagnosticDescriptor>> GetAllFor<T>() where T : DiagnosticsBase {
        var fields = typeof(T).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        foreach (var field in fields) {
            if (field.FieldType == typeof(DiagnosticDescriptor) && field.GetValue(null) is DiagnosticDescriptor descriptor) {
                yield return new(field.Name, descriptor);
            }
        }
    }

    private static class Cache<T> where T : DiagnosticsBase {
        public static readonly ImmutableArray<DiagnosticDescriptor> All = GetAllFor<T>().Select(x => x.Value).ToImmutableArray();
    }

    private static class Category {
        public const string Generation = nameof(Generation);
        public const string Usage = nameof(Usage);
        public const string Code = nameof(Code);
        public const string Polly = nameof(Polly);
        public const string Performance = nameof(Performance);
    }
}

