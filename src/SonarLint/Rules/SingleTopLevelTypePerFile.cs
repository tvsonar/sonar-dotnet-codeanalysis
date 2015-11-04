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
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Rules.Common;

namespace SonarLint.Rules
{
    namespace CSharp
    {
        using Microsoft.CodeAnalysis.CSharp;
        using Microsoft.CodeAnalysis.CSharp.Syntax;

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        [SqaleConstantRemediation("20min")]
        [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
        [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
        [Tags(Tag.BrainOverload)]
        public class SingleTopLevelTypePerFile : SingleTopLevelTypePerFileBase<SyntaxKind>
        {
            private static readonly ImmutableArray<SyntaxKind> kindsOfInterest =
                ImmutableArray.Create(SyntaxKind.ClassDeclaration, SyntaxKind.InterfaceDeclaration);
            protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

            protected override bool IsKind(SyntaxNode node, SyntaxKind kind) => node.IsKind(kind);

            protected override void ReportDiagnostic(SyntaxNode topLevelType, SyntaxTreeAnalysisContext context)
            {
                var type = topLevelType as TypeDeclarationSyntax;
                if (type == null)
                {
                    return;
                }
                context.ReportDiagnostic(Diagnostic.Create(Rule, type.Identifier.GetLocation(),
                    topLevelType.IsKind(SyntaxKind.ClassDeclaration) ? "class" : "interface"));
            }
        }
    }


    namespace VisualBasic
    {
        using Microsoft.CodeAnalysis.VisualBasic;
        using Microsoft.CodeAnalysis.VisualBasic.Syntax;

        [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
        [SqaleConstantRemediation("20min")]
        [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
        [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
        [Tags(Tag.BrainOverload)]
        public class SingleTopLevelTypePerFile : SingleTopLevelTypePerFileBase<SyntaxKind>
        {
            private static readonly ImmutableArray<SyntaxKind> kindsOfInterest =
                ImmutableArray.Create(SyntaxKind.ClassBlock, SyntaxKind.InterfaceBlock, SyntaxKind.ModuleBlock);
            protected override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

            protected override void ReportDiagnostic(SyntaxNode topLevelType, SyntaxTreeAnalysisContext context)
            {
                var interfaceBlock = topLevelType as InterfaceBlockSyntax;
                if (interfaceBlock != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, interfaceBlock.InterfaceStatement.Identifier.GetLocation(), "interface"));
                    return;
                }
                var moduleBlock = topLevelType as ModuleBlockSyntax;
                if (moduleBlock != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, moduleBlock.ModuleStatement.Identifier.GetLocation(), "module"));
                    return;
                }
                var classBlock = topLevelType as ClassBlockSyntax;
                if (classBlock != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, classBlock.ClassStatement.Identifier.GetLocation(), "class"));
                    return;
                }
            }

            protected override bool IsKind(SyntaxNode node, SyntaxKind kind) => node.IsKind(kind);
        }
    }
}