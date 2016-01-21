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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Common.Sqale;
using SonarLint.Helpers;
using System;

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags("api-design")]
    public class GenericTypeParameterInOut : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3246";
        internal const string Title = "Generic type parameters should be co/contravariant when possible";
        internal const string Description =
            "In the interests of making code as usable as possible, interfaces and delegates with generic parameters should " +
            "use the \"out\" and \"in\" modifiers when possible to make the interfaces and delegates covariant and contravariant, " +
            "respectively.";
        internal const string MessageFormat = "Add the \"{0}\" keyword to parameter \"{1}\" to make it \"{2}\".";
        internal const string Category = SonarLint.Common.Category.Design;
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
                c => CheckInterfaceVariance((InterfaceDeclarationSyntax)c.Node, c),
                SyntaxKind.InterfaceDeclaration);

            context.RegisterSyntaxNodeAction(
                c => CheckDelegateVariance((DelegateDeclarationSyntax)c.Node, c),
                SyntaxKind.DelegateDeclaration);
        }

        #region Top level

        private static void CheckInterfaceVariance(InterfaceDeclarationSyntax declaration, SyntaxNodeAnalysisContext context)
        {
            var interfaceType = context.SemanticModel.GetDeclaredSymbol(declaration);
            if (interfaceType == null)
            {
                return;
            }

            foreach (var typeParameter in interfaceType.TypeParameters
                .Where(typeParameter => typeParameter.Variance == VarianceKind.None))
            {
                var canBeIn = CheckTypeParameter(typeParameter, VarianceKind.In, interfaceType);
                var canBeOut = CheckTypeParameter(typeParameter, VarianceKind.Out, interfaceType);

                if (canBeIn ^ canBeOut)
                {
                    ReportIssue(typeParameter, canBeIn ? VarianceKind.In : VarianceKind.Out, context);
                }
            }
        }
        private static void CheckDelegateVariance(DelegateDeclarationSyntax declaration, SyntaxNodeAnalysisContext context)
        {
            var declaredSymbol = context.SemanticModel.GetDeclaredSymbol(declaration);
            if (declaredSymbol == null)
            {
                return;
            }

            var returnType = context.SemanticModel.GetTypeInfo(declaration.ReturnType).Type;
            if (returnType == null)
            {
                return;
            }

            var parameterSymbols = declaration.ParameterList == null
                ? ImmutableArray<IParameterSymbol>.Empty
                : declaration.ParameterList.Parameters
                    .Select(p => context.SemanticModel.GetDeclaredSymbol(p))
                    .ToImmutableArray();
            if (parameterSymbols.Any(parameter => parameter == null))
            {
                return;
            }

            foreach (var typeParameter in declaredSymbol.TypeParameters
                .Where(typeParameter => typeParameter.Variance == VarianceKind.None))
            {
                var canBeIn = CheckTypeParameter(typeParameter, VarianceKind.In, declaredSymbol, returnType, parameterSymbols);
                var canBeOut = CheckTypeParameter(typeParameter, VarianceKind.Out, declaredSymbol, returnType, parameterSymbols);

                if (canBeIn ^ canBeOut)
                {
                    ReportIssue(typeParameter, canBeIn ? VarianceKind.In : VarianceKind.Out, context);
                }
            }
        }

        #endregion

        #region Top level per type parameter

        private static bool CheckTypeParameter(ITypeParameterSymbol typeParameter, VarianceKind variance,
            INamedTypeSymbol delegateType, ITypeSymbol returnType, ImmutableArray<IParameterSymbol> parameters)
        {
            var canBe = CheckTypeParameterContraintsInSymbol(typeParameter, variance, delegateType);
            if (!canBe)
            {
                return false;
            }

            canBe = CanTypeParameterBeVariant(typeParameter, variance, returnType,
                true, false, delegateType);

            if (!canBe)
            {
                return false;
            }

            canBe = CheckTypeParameterInParameters(typeParameter, variance, parameters, delegateType);
            return canBe;
        }
        private static bool CheckTypeParameter(ITypeParameterSymbol typeParameter, VarianceKind variance,
            INamedTypeSymbol interfaceType)
        {
            if (typeParameter.Variance != VarianceKind.None)
            {
                return false;
            }

            foreach (INamedTypeSymbol baseInterface in interfaceType.AllInterfaces)
            {
                var canBeVariant = CanTypeParameterBeVariant(
                    typeParameter, variance,
                    baseInterface,
                    true,
                    false,
                    baseInterface);

                if (!canBeVariant)
                {
                    return false;
                }
            }

            foreach (ISymbol member in interfaceType.GetMembers())
            {
                bool canBeVariant;
                switch (member.Kind)
                {
                    case SymbolKind.Method:
                        canBeVariant = CheckTypeParameterInMethod(typeParameter, variance, (IMethodSymbol)member);
                        if (!canBeVariant)
                        {
                            return false;
                        }
                        break;
                    case SymbolKind.Event:
                        canBeVariant = CheckTypeParameterInEvent(typeParameter, variance, (IEventSymbol)member);
                        if (!canBeVariant)
                        {
                            return false;
                        }
                        break;
                    default:
                        break;
                }
            }

            return true;
        }

        #endregion

        private static void ReportIssue(ITypeParameterSymbol typeParameter, VarianceKind variance, SyntaxNodeAnalysisContext context)
        {
            if (!typeParameter.DeclaringSyntaxReferences.Any())
            {
                return;
            }

            var location = typeParameter.DeclaringSyntaxReferences.First().GetSyntax().GetLocation();

            if (variance == VarianceKind.In)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, "in", typeParameter.Name, "contravariant"));
                return;
            }

            if (variance == VarianceKind.Out)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, "out", typeParameter.Name, "covariant"));
                return;
            }
        }

        #region Check type parameters method/event/parameters

        private static bool CheckTypeParameterInMethod(ITypeParameterSymbol typeParameter, VarianceKind variance,
            IMethodSymbol method)
        {
            var canBe = CheckTypeParameterContraintsInSymbol(typeParameter, variance, method);
            if (!canBe)
            {
                return false;
            }

            canBe = CanTypeParameterBeVariant(
                typeParameter, variance,
                method.ReturnType,
                true,
                false,
                method);

            if (!canBe)
            {
                return false;
            }

            return CheckTypeParameterInParameters(typeParameter, variance, method.Parameters, method);
        }

        private static bool CheckTypeParameterInEvent(ITypeParameterSymbol typeParameter, VarianceKind variance,
            IEventSymbol @event)
        {
            return CanTypeParameterBeVariant(
                typeParameter, variance,
                @event.Type,
                false,
                true,
                @event);
        }

        private static bool CheckTypeParameterInParameters(ITypeParameterSymbol typeParameter, VarianceKind variance,
            ImmutableArray<IParameterSymbol> parameters, ISymbol context)
        {
            foreach (IParameterSymbol param in parameters)
            {
                var canBe = CanTypeParameterBeVariant(
                    typeParameter, variance,
                    param.Type,
                    param.RefKind != RefKind.None,
                    true,
                    context);

                if (!canBe)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CheckTypeParameterContraintsInSymbol(ITypeParameterSymbol typeParameter, VarianceKind variance,
            ISymbol context)
        {
            foreach (ITypeSymbol constraintType in typeParameter.ConstraintTypes)
            {
                var canBe = CanTypeParameterBeVariant(
                    typeParameter,
                    variance,
                    constraintType,
                    false,
                    true,
                    context);

                if (!canBe)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region Check type parameter variance low level

        private static bool CanTypeParameterBeVariant(
            ITypeParameterSymbol parameter,
            VarianceKind variance,
            ITypeSymbol type,
            bool requireOutputSafety,
            bool requireInputSafety,
            ISymbol context)
        {
            switch (type.Kind)
            {
                case SymbolKind.TypeParameter:
                    var typeParam = (ITypeParameterSymbol)type;
                    if (!typeParam.Equals(parameter))
                    {
                        return true;
                    }

                    return !((requireInputSafety && requireOutputSafety && variance != VarianceKind.None) ||
                        (requireOutputSafety && variance == VarianceKind.In) ||
                        (requireInputSafety && variance == VarianceKind.Out));
                case SymbolKind.ArrayType:
                    return CanTypeParameterBeVariant(parameter, variance, ((IArrayTypeSymbol)type).ElementType, requireOutputSafety, requireInputSafety, context);
                case SymbolKind.ErrorType:
                case SymbolKind.NamedType:
                    return CanTypeParameterBeVariant(parameter, variance, (INamedTypeSymbol)type, requireOutputSafety, requireInputSafety, context);
                default:
                    return true;
            }
        }

        private static bool CanTypeParameterBeVariant(
            ITypeParameterSymbol parameter,
            VarianceKind variance,
            INamedTypeSymbol namedType,
            bool requireOutputSafety,
            bool requireInputSafety,
            ISymbol context)
        {

            switch (namedType.TypeKind)
            {
                case TypeKind.Class:
                case TypeKind.Struct:
                case TypeKind.Enum:
                case TypeKind.Interface:
                case TypeKind.Delegate:
                case TypeKind.Error:
                    break;
                default:
                    return true;
            }

            var currentNamedType = namedType;
            while (currentNamedType != null)
            {
                for (int i = 0; i < currentNamedType.Arity; i++)
                {
                    var typeParam = currentNamedType.TypeParameters[i];
                    var typeArg = currentNamedType.TypeArguments[i];

                    if (!typeArg.Equals(parameter))
                    {
                        return false;
                    }

                    bool requireOut;
                    bool requireIn;

                    switch (typeParam.Variance)
                    {
                        case VarianceKind.Out:
                            requireOut = requireOutputSafety;
                            requireIn = requireInputSafety;
                            break;
                        case VarianceKind.In:
                            requireOut = requireInputSafety;
                            requireIn = requireOutputSafety;
                            break;
                        case VarianceKind.None:
                            requireIn = true;
                            requireOut = true;
                            break;
                        default:
                            throw new NotSupportedException();
                    }

                    if (!CanTypeParameterBeVariant(parameter, variance, typeArg, requireOut, requireIn, context))
                    {
                        return false;
                    }
                }

                currentNamedType = currentNamedType.ContainingType;
            }

            return true;
        }

        #endregion
    }
}
