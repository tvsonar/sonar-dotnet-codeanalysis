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
using System.Linq;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class PropertyToAutoProperty : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S2292";
        internal const string Title = "Trivial properties should be auto-implemented";
        internal const string Description =
            "Trivial properties, which include no logic but setting and getting a backing field should be converted to auto-implemented " +
            "properties, yielding cleaner and more readable code.";
        internal const string MessageFormat = "Make this an auto-implemented property and remove its backing field.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = true;

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
                    var propertyDeclaration = (PropertyDeclarationSyntax)c.Node;
                    var propertySymbol = c.SemanticModel.GetDeclaredSymbol(propertyDeclaration);
                    if (propertyDeclaration.AccessorList == null ||
                        propertyDeclaration.AccessorList.Accessors.Count != 2 ||
                        propertySymbol == null)
                    {
                        return;
                    }

                    var getter = propertyDeclaration.AccessorList.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
                    var setter = propertyDeclaration.AccessorList.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));

                    if (getter == null || setter == null)
                    {
                        return;
                    }

                    IFieldSymbol getterField;
                    IFieldSymbol setterField;
                    if (TryGetFieldFromGetter(getter, c.SemanticModel, out getterField) &&
                        TryGetFieldFromSetter(setter, c.SemanticModel, out setterField) &&
                        getterField.Equals(setterField) &&
                        !getterField.GetAttributes().Any() &&
                        getterField.IsStatic == propertySymbol.IsStatic &&
                        getterField.Type.Equals(propertySymbol.Type))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, propertyDeclaration.Identifier.GetLocation()));
                    }
                },
                SyntaxKind.PropertyDeclaration);
        }

        private static bool TryGetFieldFromSetter(AccessorDeclarationSyntax setter, SemanticModel semanticModel, out IFieldSymbol setterField)
        {
            setterField = null;
            if (setter.Body == null ||
                setter.Body.Statements.Count != 1)
            {
                return false;
            }

            var statement = setter.Body.Statements[0] as ExpressionStatementSyntax;
            if (statement == null)
            {
                return false;
            }

            var assignment = statement.Expression as AssignmentExpressionSyntax;
            if (assignment == null || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                return false;
            }

            var parameter = semanticModel.GetSymbolInfo(assignment.Right).Symbol as IParameterSymbol;
            if (parameter == null ||
                parameter.Name != "value" ||
                !parameter.IsImplicitlyDeclared)
            {
                return false;
            }

            return TryGetField(assignment.Left, semanticModel.GetDeclaredSymbol(setter).ContainingType,
                semanticModel, out setterField);
        }

        private static bool TryGetField(ExpressionSyntax expression, INamedTypeSymbol declaringType,
            SemanticModel semanticModel, out IFieldSymbol field)
        {
            if (expression is IdentifierNameSyntax)
            {
                field = semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
                return field != null;
            }

            var memberAccess = expression as MemberAccessExpressionSyntax;
            if (memberAccess == null ||
                !memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                field = null;
                return false;
            }

            if (memberAccess.Expression is ThisExpressionSyntax)
            {
                field = semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
                return field != null;
            }

            var identifier = memberAccess.Expression as IdentifierNameSyntax;
            if (identifier == null)
            {
                field = null;
                return false;
            }

            var type = semanticModel.GetSymbolInfo(identifier).Symbol as INamedTypeSymbol;
            if (type == null ||
                !type.Equals(declaringType))
            {
                field = null;
                return false;
            }

            field = semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
            return field != null;
        }

        private static bool TryGetFieldFromGetter(AccessorDeclarationSyntax getter, SemanticModel semanticModel, out IFieldSymbol getterField)
        {
            getterField = null;
            if (getter.Body == null ||
                getter.Body.Statements.Count != 1)
            {
                return false;
            }

            var statement = getter.Body.Statements[0] as ReturnStatementSyntax;
            if (statement == null ||
                statement.Expression == null)
            {
                return false;
            }

            return TryGetField(statement.Expression, semanticModel.GetDeclaredSymbol(getter).ContainingType,
                semanticModel, out getterField);
        }
    }
}
