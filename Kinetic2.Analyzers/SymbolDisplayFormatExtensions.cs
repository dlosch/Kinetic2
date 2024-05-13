using Microsoft.CodeAnalysis;

namespace Kinetic2.Analyzers;

internal static class SymbolDisplayFormatExtensions {
    private static SymbolDisplayFormat _fmt = new SymbolDisplayFormat(

        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
        );

    private static SymbolDisplayFormat _fmtMethodDiReg = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeVariance | SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints
        , memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeModifiers | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType
            );

    internal static string QualifiedTypeName(this ITypeSymbol nts) => nts.ToDisplayString(_fmt);

    internal static string FullNameEx(this IMethodSymbol methodSymbol) {
        var namespaceName = methodSymbol.ContainingNamespace.ToDisplayString();
        var typeName = methodSymbol.ContainingType.Name;
        var methodName = methodSymbol.ToDisplayString(_fmtMethodDiReg);

        return $"{namespaceName}.{typeName}::{methodName}";
    }

    internal static string Namespace(this INamedTypeSymbol nts) => nts.ContainingNamespace.ToDisplayString(FullyQualifiedSymbolFormat);

    internal static readonly SymbolDisplayFormat FullyQualifiedSymbolFormat = new(
    typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
    globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
    kindOptions: SymbolDisplayKindOptions.IncludeNamespaceKeyword |
                 SymbolDisplayKindOptions.IncludeTypeKeyword |
                 SymbolDisplayKindOptions.IncludeMemberKeyword,
    genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                     //SymbolDisplayGenericsOptions.IncludeTypeConstraints |
                     SymbolDisplayGenericsOptions.IncludeVariance,
    parameterOptions: SymbolDisplayParameterOptions.IncludeName |
                      SymbolDisplayParameterOptions.IncludeType |
                      SymbolDisplayParameterOptions.IncludeDefaultValue |  // <-- this is a little iffy
                      SymbolDisplayParameterOptions.IncludeExtensionThis |
                      SymbolDisplayParameterOptions.IncludeParamsRefOut,
    memberOptions: SymbolDisplayMemberOptions.IncludeType |
                   SymbolDisplayMemberOptions.IncludeModifiers |
                   SymbolDisplayMemberOptions.IncludeAccessibility |
                   SymbolDisplayMemberOptions.IncludeParameters,
    miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                          SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                          SymbolDisplayMiscellaneousOptions.RemoveAttributeSuffix |
                          SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                          SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral,
    propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
    delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
    localOptions: SymbolDisplayLocalOptions.IncludeType |
                  SymbolDisplayLocalOptions.IncludeConstantValue |
                  SymbolDisplayLocalOptions.IncludeRef);
}
