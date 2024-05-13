using Microsoft.CodeAnalysis;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

internal struct InterceptorsLocationAttributeWriter {
    readonly CodeWriter _codeWriter;

    public InterceptorsLocationAttributeWriter(CodeWriter codeWriter) {
        _codeWriter = codeWriter;
    }

    /// <summary>
    /// Writes the "InterceptsLocationAttribute" to inner <see cref="CodeWriter"/>.
    /// </summary>
    /// <remarks>Does so only when "InterceptsLocationAttribute" is NOT visible by <see cref="Compilation"/>.</remarks>
    public void Write(Compilation compilation) {
        var attrib = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.InterceptsLocationAttribute");
        if (!IsAvailable(attrib, compilation)) {
            _codeWriter.NewLine().Append(""" 
namespace System.Runtime.CompilerServices
{
    // this type is needed by the compiler to implement interceptors - it doesn't need to
    // come from the runtime itself, though

    [global::System.Diagnostics.Conditional("DEBUG")] // not needed post-build, so: evaporate
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
    sealed file class InterceptsLocationAttribute : global::System.Attribute
    {
        public InterceptsLocationAttribute(string path, int lineNumber, int columnNumber)
        {
            _ = path;
            _ = lineNumber;
            _ = columnNumber;
        }
    }
}
""");
        }

        static bool IsAvailable(INamedTypeSymbol? type, Compilation compilation) {
            if (type is null) return false;
            if (type.IsFileLocal) return false; // we're definitely not in that file

            switch (type.DeclaredAccessibility) {
                case Accessibility.Public:
                    // fine, we'll use it
                    return true;
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                    // we can use it if we're in the same project (note we won't check IVTA)
                    return SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly);
                default:
                    return false;
            }
        }
    }
}
