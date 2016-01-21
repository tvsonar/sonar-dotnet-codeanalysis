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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SonarLint.Rules.Common
{
    public abstract class SingleStatementPerLineBase : DiagnosticAnalyzer, IMultiLanguageDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S122";
        protected const string Title = "Statements should be on separate lines";
        protected const string Description =
            "For better readability, do not put more than one statement on a single line.";
        protected const string MessageFormat = "Reformat the code to have only one statement per line.";
        protected const string Category = SonarLint.Common.Category.Maintainability;
        protected const Severity RuleSeverity = Severity.Minor;
        protected const bool IsActivatedByDefault = false;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected abstract GeneratedCodeRecognizer GeneratedCodeRecognizer { get; }
        GeneratedCodeRecognizer IMultiLanguageDiagnosticAnalyzer.GeneratedCodeRecognizer => GeneratedCodeRecognizer;
    }

    public abstract class SingleStatementPerLineBase<TStatementSyntax> : SingleStatementPerLineBase
        where TStatementSyntax : SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var statements = GetStatements(c.Tree);

                    var statementsByLines = MultiValueDictionary<int, TStatementSyntax>.Create<HashSet<TStatementSyntax>>();
                    foreach (var statement in statements)
                    {
                        AddStatementToLineCache(statement, statementsByLines);
                    }

                    var lines = c.Tree.GetText().Lines;
                    foreach (var statementsByLine in statementsByLines.Where(pair => pair.Value.Count > 1))
                    {
                        var location = CalculateLocationForLine(lines[statementsByLine.Key], c.Tree, statementsByLine.Value);
                        c.ReportDiagnostic(Diagnostic.Create(Rule, location));
                    }
                });
        }

        private IEnumerable<TStatementSyntax> GetStatements(SyntaxTree tree)
        {
            var statements = tree.GetRoot()
                    .DescendantNodesAndSelf()
                    .OfType<TStatementSyntax>()
                    .Where(st => !StatementShouldBeExcluded(st));

            return statements;
        }

        protected abstract bool StatementShouldBeExcluded(TStatementSyntax statement);
        private TStatementSyntax GetContainingStatement(SyntaxToken token)
        {
            var node = token.Parent;
            var statement = node as TStatementSyntax;
            while (node != null &&
                (statement == null || !StatementShouldBeExcluded(statement)))
            {
                node = node.Parent;
                statement = node as TStatementSyntax;
            }
            return statement;
        }

        private static Location CalculateLocationForLine(TextLine line, SyntaxTree tree,
            ICollection<TStatementSyntax> statements)
        {
            var lineSpan = line.Span;

            var min = statements.Min(st => lineSpan.Intersection(st.Span).Value.Start);
            var max = statements.Max(st => lineSpan.Intersection(st.Span).Value.End);

            return Location.Create(tree, TextSpan.FromBounds(min, max));
        }

        private void AddStatementToLineCache(TStatementSyntax statement, MultiValueDictionary<int, TStatementSyntax> statementsByLines)
        {
            var startLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line;
            AddStatementWithLine(statement, startLine, statementsByLines);

            var lastToken = statement.GetLastToken();
            var tokenBelonsTo = GetContainingStatement(lastToken);
            if (tokenBelonsTo == statement)
            {
                var endLine = statement.GetLocation().GetLineSpan().EndLinePosition.Line;
                AddStatementWithLine(statement, endLine, statementsByLines);
            }
        }

        private static void AddStatementWithLine(TStatementSyntax statement, int line,
            MultiValueDictionary<int, TStatementSyntax> statementsByLines)
        {
            statementsByLines.AddWithKey(line, statement);
        }
    }
}
