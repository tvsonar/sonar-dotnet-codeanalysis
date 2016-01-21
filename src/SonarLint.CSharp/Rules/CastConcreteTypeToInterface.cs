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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("1h")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.InstructionReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Design)]
    public class CastConcreteTypeToInterface : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3215";
        internal const string Title = "\"interface\" instances should not be cast to concrete types";
        internal const string Description =
            "Needing to cast from an interface to a concrete type indicates that something is wrong with the abstractions in use, " +
            "likely that something is missing from the interface. Instead of casting to a discrete type, the missing functionality " +
            "should be added to the interface. Otherwise there is the risk of runtime exceptions.";
        internal const string MessageFormat = "Remove this cast and edit the interface to add the missing functionality.";
        internal const string Category = SonarLint.Common.Category.Reliability;
        internal const Severity RuleSeverity = Severity.Major;
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
                    var castExpression = (CastExpressionSyntax)c.Node;
                    var castedTo = c.SemanticModel.GetTypeInfo(castExpression.Type).Type;
                    var castedFrom = c.SemanticModel.GetTypeInfo(castExpression.Expression).Type;
                    CheckForIssue(castedTo, castedFrom, c);
                },
                SyntaxKind.CastExpression);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var castExpression = (BinaryExpressionSyntax)c.Node;
                    var castedTo = c.SemanticModel.GetTypeInfo(castExpression.Right).Type;
                    var castedFrom = c.SemanticModel.GetTypeInfo(castExpression.Left).Type;
                    CheckForIssue(castedTo, castedFrom, c);
                },
                SyntaxKind.AsExpression);
        }

        public static void CheckForIssue(ITypeSymbol castedTo, ITypeSymbol castedFrom,
            SyntaxNodeAnalysisContext context)
        {
            if (castedFrom == null ||
                castedTo == null ||
                castedFrom.TypeKind != TypeKind.Interface ||
                castedTo.TypeKind != TypeKind.Class ||
                castedTo.SpecialType == SpecialType.System_Object)
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation()));
        }
    }
}
