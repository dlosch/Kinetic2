using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kinetic2.Analyzers.Logic;


internal static class IMethodSymbolExtensions {
    internal const string AttributeTypeName = nameof(ResiliencePipelineAttribute);
    internal static readonly string AttributeName = nameof(ResiliencePipelineAttribute).Substring(0, AttributeTypeName.Length - "Attribute".Length);

    internal static bool FilterByResiliencePipelineAttribute(AttributeSyntax attributeSyntax) => attributeSyntax.Name.ToString().IndexOf(AttributeName, StringComparison.Ordinal) >= 0;

    internal static bool HasMarkerAttribute(this ISymbol symbol) => symbol.GetAttributes().Any(a => a.AttributeClass?.Name == AttributeTypeName);

    internal static bool IsResiliencePipelineAttribute(AttributeData attrib)
       => attrib.AttributeClass is {
           Name: nameof(ResiliencePipelineAttribute), // "ResiliencePipelineAttribute",
           ContainingNamespace: {
               Name: nameof(Kinetic2), // "Kinetic2",
               ContainingNamespace.IsGlobalNamespace: true
           }
       };

    //public static bool IsResiliencePipelineAttribute(this INamedTypeSymbol namedTypeSymbol)
    //   => namedTypeSymbol is {
    //       Name: nameof(ResiliencePipelineAttribute), // "ResiliencePipelineAttribute",
    //       ContainingNamespace: {
    //           Name: nameof(Kinetic2), // "Kinetic2",
    //           ContainingNamespace.IsGlobalNamespace: true
    //       }
    //   };


    internal static ResiliencePipelineAttribute? GetResiliencePipelineAttribute(this IMethodSymbol symbol, string attributeName = nameof(ResiliencePipelineAttribute), string attributeNamespace = nameof(Kinetic2)) {
        var attributeData = symbol.GetAttributes().FirstOrDefault(/*a => a.*/IsResiliencePipelineAttribute/*()*/);
        if (attributeData is null) return default;
        var ad = GetAttribute(attributeData);
        return ad;
    }

    internal static ResiliencePipelineAttribute GetAttribute(AttributeData attributeData) {

        var pipelineName = default(string);
        var addLogStatements = false;
        var activitySpanName = default(string);

        for (int idx = 0; idx < attributeData.ConstructorArguments.Length; idx++) {
            if (idx == 0 && attributeData.ConstructorArguments[idx].Value is string pipelineNameVal) {
                pipelineName = pipelineNameVal;
            }
            else if (idx == 1 && attributeData.ConstructorArguments[idx].Value is bool enabled) {
                addLogStatements = enabled;
            }
            else if (idx == 2 && attributeData.ConstructorArguments[idx].Value is string spanName) {
                activitySpanName = spanName;
            }

        }

        return new ResiliencePipelineAttribute(pipelineName!, addLogStatements, activitySpanName);
    }
}


