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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;
using System.Collections.Generic;

namespace SonarLint.Rules.Common
{
    public abstract class SingleTopLevelTypePerFileBase : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S1996";
        internal const string Title = "Files should contain only one top-level class or interface each";
        internal const string Description =
            "A file that grows too much tends to aggregate too many responsibilities and inevitably becomes harder to " +
            "understand and therefore to maintain. This is doubly true for a file with multiple top-level classes and " +
            "interfaces. It is strongly advised to divide the file into one top-level class or interface per file.";
        internal const string MessageFormat = "Put this top-level {0} declaration in a dedicated source file.";
        internal const string Category = Constants.SonarLint;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }
    }

    public abstract class SingleTopLevelTypePerFileBase<TLanguageKindEnum> : SingleTopLevelTypePerFileBase
        where TLanguageKindEnum : struct
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxTreeActionInNonGenerated(
                c =>
                {
                    var topLevelTypes = GetTopLevelTypes(c.Tree);
                    if (topLevelTypes.Count() > 1)
                    {
                        foreach (var topLevelType in topLevelTypes)
                        {
                            ReportDiagnostic(topLevelType, c);
                        }
                    }
                });
        }

        private IEnumerable<SyntaxNode> GetTopLevelTypes(SyntaxTree tree)
        {
            return tree.GetRoot()
                .DescendantNodesAndSelf()
                .Where(node => SyntaxKindsOfInterest.Any(sk => IsKind(node, sk)))
                .Where(node => IsTopLevel(node));
        }

        private bool IsTopLevel(SyntaxNode node)
        {
            var parent = node.Parent;
            while(parent != null)
            {
                if (SyntaxKindsOfInterest.Any(sk => IsKind(parent, sk)))
                {
                    return false;
                }
                parent = parent.Parent;
            }
            return true;
        }

        protected abstract void ReportDiagnostic(SyntaxNode topLevelType, SyntaxTreeAnalysisContext context);
        protected abstract bool IsKind(SyntaxNode node, TLanguageKindEnum kind);
        protected abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
    }
}
