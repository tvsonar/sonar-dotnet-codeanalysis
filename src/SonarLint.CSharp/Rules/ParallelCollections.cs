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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("30min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Design)]
    public class ParallelCollections : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3221";
        internal const string Title = "Parallel collections should not be maintained";
        internal const string Description =
            "Using parallel collections or arrays rather than classes to hold and process related pieces of data is " +
            "an antipattern. Instead, define a type for the entity the arrays represent and use an array or " +
            "collection of that type.";
        internal const string MessageFormat =
            "Create a type that holds an element from {0}, and merge these parallel collections indexed by the same value.";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    if (c.SemanticModel.Compilation.IsTest())
                    {
                        return;
                    }

                    var forLoop = (ForStatementSyntax)c.Node;
                    var loopIdentifiers = forLoop.Declaration.Variables.Select(v => v.Identifier);
                    var elementAccesses = GetElementAccesses(forLoop.Statement);

                    CheckElementAccessExpressions(c, loopIdentifiers, elementAccesses);
                },
                SyntaxKind.ForStatement);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    if (c.SemanticModel.Compilation.IsTest())
                    {
                        return;
                    }

                    var foreachLoop = (ForEachStatementSyntax)c.Node;
                    var elementAccesses = GetElementAccesses(foreachLoop.Statement);

                    CheckElementAccessExpressions(c, new[] { foreachLoop.Identifier }, elementAccesses);
                },
                SyntaxKind.ForEachStatement);
        }

        private static List<ElementAccessExpressionSyntax> GetElementAccesses(StatementSyntax statement)
        {
            return statement.DescendantNodes()
                .OfType<ElementAccessExpressionSyntax>()
                .Where(elementAccess => elementAccess.ArgumentList.Arguments.Count == 1)
                .Where(elementAccess =>
                {
                    var assignment = elementAccess.Parent as AssignmentExpressionSyntax;
                    return assignment == null || assignment.Left != elementAccess;
                })
                .ToList();
        }

        private static void CheckElementAccessExpressions(SyntaxNodeAnalysisContext c, IEnumerable<SyntaxToken> loopIdentifiers, List<ElementAccessExpressionSyntax> elementAccesses)
        {
            var alreadyReportedOn = new Dictionary<ExpressionSyntax, List<ExpressionSyntax>>();

            for (int i = 0; i < elementAccesses.Count; i++)
            {
                var elementAccess = elementAccesses[i];
                var matchingCollections = new List<ExpressionSyntax> { elementAccess.Expression };
                var sameExpressions = new List<ExpressionSyntax>();
                var argument = elementAccess.ArgumentList.Arguments.First();

                if (ContainsOrAdd(elementAccess.Expression, argument, alreadyReportedOn))
                {
                    continue;
                }

                if (!argument.Expression.DescendantNodesAndSelf()
                    .OfType<IdentifierNameSyntax>()
                    .Any(identifier => loopIdentifiers
                        .Any(loopIdentifier => identifier.Identifier.ValueText == loopIdentifier.ValueText)))
                {
                    continue;
                }

                for (int j = i + 1; j < elementAccesses.Count; j++)
                {
                    var otherElementAccess = elementAccesses[j];

                    var otherArgument = otherElementAccess.ArgumentList.Arguments.First();

                    if (EquivalenceChecker.AreEquivalent(argument.Expression, otherArgument.Expression))
                    {
                        if (matchingCollections.Any(collection => EquivalenceChecker.AreEquivalent(collection, otherElementAccess.Expression)))
                        {
                            sameExpressions.Add(otherElementAccess.Expression);
                        }
                        else
                        {
                            matchingCollections.Add(otherElementAccess.Expression);
                        }
                    }
                }

                if (matchingCollections.Count > 1)
                {
                    foreach (var collection in matchingCollections)
                    {
                        ContainsOrAdd(collection, argument, alreadyReportedOn);
                    }

                    var collectionNames = string.Join(", ", matchingCollections.Select(collection => $"\"{collection.ToString()}\""));
                    foreach (var collection in matchingCollections.Union(sameExpressions))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, collection.GetLocation(), collectionNames));
                    }
                }
            }
        }

        private static bool ContainsOrAdd(ExpressionSyntax elementAccess, ArgumentSyntax argument,
            Dictionary<ExpressionSyntax, List<ExpressionSyntax>> alreadyReportedOn)
        {
            var alreadyReportedCollection = alreadyReportedOn.Keys
                .SingleOrDefault(e => EquivalenceChecker.AreEquivalent(e, elementAccess));

            if (alreadyReportedCollection == null)
            {
                alreadyReportedOn.Add(elementAccess, new List<ExpressionSyntax> { argument.Expression });
            }
            else
            {
                var alreadyReportedArgument = alreadyReportedOn[alreadyReportedCollection]
                    .SingleOrDefault(a => EquivalenceChecker.AreEquivalent(a, argument.Expression));

                if (alreadyReportedArgument == null)
                {
                    alreadyReportedOn[alreadyReportedCollection].Add(argument.Expression);
                }
                else
                {
                    return true;
                }
            }
            return false;
        }
    }
}
