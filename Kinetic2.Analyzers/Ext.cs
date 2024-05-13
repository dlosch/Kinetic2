using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics;

namespace Kinetic2.Analyzers;
/*
 * parts of this code taken from the most excellent https://github.com/DapperLib/DapperAOT/ 
 * licence: The Dapper library and tools are licenced under Apache 2.0: http://www.apache.org/licenses/LICENSE-2.0
 * owner: Marc Gravell
 */

static class Ext {
    public static Location GetMemberLocation(this IInvocationOperation call)
        => GetMemberSyntax(call).GetLocation();
    public static SyntaxNode GetMemberSyntax(this IInvocationOperation call) {
        var syntax = call?.Syntax;
        if (syntax is null) return null!; // GIGO

        var helper = GetHelper(syntax.Language);
        foreach (var outer in syntax.ChildNodesAndTokens()) {
            var outerNode = outer.AsNode();
            if (outerNode is not null && helper.IsMemberAccess(outerNode)) {
                // if there is an identifier, we want the **last** one - think Foo.Bar.Blap(...)
                SyntaxNode? identifier = null;
                foreach (var inner in outerNode.ChildNodesAndTokens()) {
                    var innerNode = inner.AsNode();
                    if (innerNode is not null && helper.IsName(innerNode))
                        identifier = innerNode;
                }
                // we'd prefer an identifier, but we'll allow the entire member-access
                return identifier ?? outerNode;
            }
        }
        return syntax;
    }

    private static LanguageHelper GetHelper(string? language)
        => language switch {
            LanguageNames.CSharp => LanguageHelper.CSharp,
            //LanguageNames.VisualBasic => LanguageHelper.VisualBasic,
            //_ => LanguageHelper.Null,
            _ => throw new NotImplementedException(language)
        };

    public static Location ComputeLocation(this SyntaxToken token
        , Loc location
        //, scoped in TSqlProcessor.Location location
        ) {
        var origin = token.GetLocation();
        try {
            if (origin.SourceTree is not null) {
                var text = token.Text;
                TextSpan originSpan = token.Span;
                if (GetHelper(token.Language).TryGetStringSpan(token, text, location, out var skip, out var take)) {
                    var finalSpan = new TextSpan(originSpan.Start + skip, take);
                    if (originSpan.Contains(finalSpan)) // make sure we haven't messed up the math!
                    {
                        return Location.Create(origin.SourceTree, finalSpan);
                    }
                }
            }
        }
        catch (Exception ex)// best efforts only
        {
            Debug.WriteLine(ex.Message);
        }
        return origin;
    }


}
