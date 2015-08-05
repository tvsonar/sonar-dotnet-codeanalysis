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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;

namespace SonarLint.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.DataReliability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("confusing")]
    public class MemberShadowsParentMember : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2387";
        internal const string Title = "Child class members should not shadow parent class members";
        internal const string Description =
            "Shadowing parent class members by creating fields, properties and methods with the same signatures as non-\"virtual\" "+
            "parent class members can result in seemingly strange behavior if an instance of the child class is cast to the parent "+
            "class. In such cases, the parent class' code will be executed instead of the code in the child class, confusing " +
            "callers and potentially causing hard-to-find bugs.";
        internal const string MessageFormat = "{0}";
        internal const string MessageMatch = "\"{0}\" is the name of a member in \"{1}\"";
        internal const string MessageSimilar = "\"{0}\" differs only by case from \"{2}\" in \"{1}\".";
        internal const string Category = "SonarQube";
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var fieldDeclaration = (FieldDeclarationSyntax) c.Node;
                    foreach (var variableDeclarator in fieldDeclaration.Declaration.Variables)
                    {
                        CheckMember(variableDeclarator, variableDeclarator.Identifier.GetLocation(), c);
                    }
                },
                SyntaxKind.FieldDeclaration);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var propertyDeclaration = (PropertyDeclarationSyntax)c.Node;
                    CheckMember(propertyDeclaration, propertyDeclaration.Identifier.GetLocation(), c);
                },
                SyntaxKind.PropertyDeclaration);

            context.RegisterSyntaxNodeAction(
                c =>
                {
                    var methodDeclaration = (MethodDeclarationSyntax)c.Node;

                    if (methodDeclaration.Identifier.ValueText == "Equals")
                    {
                        return;
                    }

                    CheckMember(methodDeclaration, methodDeclaration.Identifier.GetLocation(), c);
                },
                SyntaxKind.MethodDeclaration);
        }

        private static void CheckMember(SyntaxNode syntaxNode, Location errorLocation, SyntaxNodeAnalysisContext context)
        {
            var checkedMember = context.SemanticModel.GetDeclaredSymbol(syntaxNode);
            if (checkedMember == null ||
                checkedMember.IsOverride)
            {
                return;
            }

            var interfaceImplementation = checkedMember.ContainingType.AllInterfaces
                .SelectMany(i => i.GetMembers())
                .Any(m => checkedMember.Equals(checkedMember.ContainingType.FindImplementationForInterfaceMember(m)));

            if (interfaceImplementation)
            {
                return;
            }

            var memberNameLowered = checkedMember.Name.ToLower();
            var baseType = checkedMember.ContainingType.BaseType;

            while (baseType != null &&
                   !(baseType is IErrorTypeSymbol))
            {
                var similarMembers = baseType.GetMembers()
                    .Where(m => m.DeclaredAccessibility != Accessibility.Private)
                    .Where(m => m.Name.ToLower() == memberNameLowered)
                    .ToList();

                if (similarMembers.Any(m => m.Name == checkedMember.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, errorLocation,
                        string.Format(MessageMatch, checkedMember.Name, baseType.Name)));
                    return;
                }

                if (similarMembers.Any())
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, errorLocation,
                        string.Format(MessageSimilar, checkedMember.Name, baseType.Name, similarMembers.First().Name)));
                    return;
                }

                baseType = baseType.BaseType;
            }
        }
    }
}
