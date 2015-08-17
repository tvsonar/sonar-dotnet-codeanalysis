/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace SonarLint.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class GenericTypeParameterUnusedCodeFixProvider : CodeFixProvider
    {
        internal const string Title = "Remove unused type parameter";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(GenericTypeParameterUnused.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntaxNode = root.FindNode(diagnosticSpan) as TypeParameterSyntax;

            if (syntaxNode == null)
            {
                return;
            }

            var semanticModel = await context.Document
                .GetSemanticModelAsync(context.CancellationToken)
                .ConfigureAwait(false);

            var symbol = semanticModel.GetDeclaredSymbol(syntaxNode);
            if (symbol == null)
            {
                return;
            }

            var references = symbol.DeclaringSyntaxReferences.Select(
                reference => reference.GetSyntax());

            var data = references.Select(reference =>
                new SyntaxDocumentPair
                {
                    Syntax = reference as TypeParameterSyntax,
                    Document = context.Document.Project.Solution.GetDocument(reference.SyntaxTree)
                });

            //todo: fix all not working, we need a custom batch fixer.

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => RemoveUnusedCode(data, context.Document.Project.Solution, c)),
                diagnostic
            );
        }

        private static async Task<Solution> RemoveUnusedCode(IEnumerable<SyntaxDocumentPair> data, Solution s, CancellationToken cancellationToken)
        {
            var newSolution = s;
            foreach (var item in data)
            {
                var root = await item.Document.GetSyntaxRootAsync(cancellationToken);
                SyntaxNode newRoot;

                var list = (TypeParameterListSyntax)item.Syntax.Parent;

                if (list.Parameters.Count == 1)
                {
                    newRoot = root.RemoveNode(list, SyntaxRemoveOptions.KeepExteriorTrivia | SyntaxRemoveOptions.KeepEndOfLine);
                }
                else
                {
                    newRoot = root.RemoveNode(item.Syntax, SyntaxRemoveOptions.KeepNoTrivia | SyntaxRemoveOptions.KeepEndOfLine);
                }

                newSolution = newSolution.WithDocumentText(item.Document.Id, newRoot.GetText());
            }
            return newSolution;
        }

        private class SyntaxDocumentPair
        {
            public TypeParameterSyntax Syntax { get; set; }
            public Document Document { get; set; }
        }
    }
}

