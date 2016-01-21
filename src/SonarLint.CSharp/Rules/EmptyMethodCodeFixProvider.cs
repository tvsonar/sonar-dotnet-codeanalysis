﻿/*
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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using System;

namespace SonarLint.Rules.CSharp
{
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public class EmptyMethodCodeFixProvider : CodeFixProvider
    {
        public const string TitleThrow = "Throw NotSupportedException";
        public const string TitleComment = "Add comment";
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(EmptyMethod.DiagnosticId);
            }
        }
        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        private const string LiteralNotSupportedException = "NotSupportedException";
        private const string LiteralSystem = "System";

        public override sealed async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var syntaxNode = root.FindNode(diagnosticSpan);
            var method = syntaxNode.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            if (method.Body.CloseBraceToken.IsMissing ||
                method.Body.OpenBraceToken.IsMissing)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    TitleComment,
                    c =>
                    {
                        var newMethodBody = method.Body;

                        newMethodBody = newMethodBody
                            .WithOpenBraceToken(newMethodBody.OpenBraceToken
                                .WithTrailingTrivia(SyntaxFactory.TriviaList()
                                    .Add(SyntaxFactory.EndOfLine(Environment.NewLine))));

                        newMethodBody = newMethodBody
                            .WithCloseBraceToken(newMethodBody.CloseBraceToken
                                .WithLeadingTrivia(SyntaxFactory.TriviaList()
                                    .Add(SyntaxFactory.Comment("// Method intentionally left empty."))
                                    .Add(SyntaxFactory.EndOfLine(Environment.NewLine))));

                        var newRoot = root.ReplaceNode(
                            method.Body,
                            newMethodBody.WithTriviaFrom(method.Body).WithAdditionalAnnotations(Formatter.Annotation));
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    TitleComment),
                context.Diagnostics);

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            
            var systemNeedsToBeAdded = NamespaceNeedsToBeAdded(method, semanticModel);

            var memberAccessRoot = systemNeedsToBeAdded
                ? (NameSyntax)SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName(LiteralSystem),
                        SyntaxFactory.IdentifierName(LiteralNotSupportedException))
                : SyntaxFactory.IdentifierName(LiteralNotSupportedException);

            context.RegisterCodeFix(
                CodeAction.Create(
                    TitleThrow,
                    c =>
                    {
                        var newRoot = root.ReplaceNode(method.Body,
                            method.Body.WithStatements(
                                SyntaxFactory.List(
                                    new StatementSyntax[]
                                    {
                                        SyntaxFactory.ThrowStatement(
                                            SyntaxFactory.ObjectCreationExpression(
                                                memberAccessRoot,
                                                SyntaxFactory.ArgumentList(),
                                                null))
                                    }))
                                    .WithTriviaFrom(method.Body)
                                    .WithAdditionalAnnotations(Formatter.Annotation));
                        return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
                    },
                    TitleThrow),
                context.Diagnostics);
        }

        private static bool NamespaceNeedsToBeAdded(MethodDeclarationSyntax method,
            SemanticModel semanticModel)
        {
            return !semanticModel.LookupNamespacesAndTypes(method.Body.CloseBraceToken.SpanStart)
                .OfType<INamedTypeSymbol>()
                .Any(nt =>
                    nt.IsType &&
                    nt.Name == LiteralNotSupportedException &&
                    nt.ContainingNamespace.Name == LiteralSystem);
        }
    }
}

