/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SonarLint.Runner.CSharp
{
    public static class TokenHelper
    {
        // From Roslyn (http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis.CSharp.Workspaces/LanguageServices/CSharpSyntaxFactsService.cs,1453)
        public static SyntaxNode GetBindableParent(this SyntaxToken token)
        {
            var node = token.Parent;
            while (node != null)
            {
                var parent = node.Parent;

                // If this node is on the left side of a member access expression, don't ascend
                // further or we'll end up binding to something else.
                var memberAccess = parent as MemberAccessExpressionSyntax;
                if (memberAccess != null &&
                    memberAccess.Expression == node)
                {
                    break;
                }

                // If this node is on the left side of a qualified name, don't ascend
                // further or we'll end up binding to something else.
                var qualifiedName = parent as QualifiedNameSyntax;
                if (qualifiedName != null &&
                    qualifiedName.Left == node)
                {
                    break;
                }

                // If this node is on the left side of a alias-qualified name, don't ascend
                // further or we'll end up binding to something else.
                var aliasQualifiedName = parent as AliasQualifiedNameSyntax;
                if (aliasQualifiedName != null &&
                    aliasQualifiedName.Alias == node)
                {
                    break;
                }

                // If this node is the type of an object creation expression, return the
                // object creation expression.
                var objectCreation = parent as ObjectCreationExpressionSyntax;
                if (objectCreation != null &&
                    objectCreation.Type == node)
                {
                        node = parent;
                        break;
                }

                // The inside of an interpolated string is treated as its own token so we
                // need to force navigation to the parent expression syntax.
                if (node is InterpolatedStringTextSyntax && parent is InterpolatedStringExpressionSyntax)
                {
                    node = parent;
                    break;
                }

                // If this node is not parented by a name, we're done.
                var name = parent as NameSyntax;
                if (name == null)
                {
                    break;
                }

                node = parent;
            }

            return node;
        }
    }
}
