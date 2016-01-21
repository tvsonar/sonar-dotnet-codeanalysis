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

namespace SonarLint.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Readability)]
    [SqaleConstantRemediation("2min")]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Clumsy)]
    public class ConditionalSimplification : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S3240";
        internal const string Title = "The simplest possible condition syntax should be used";
        internal const string Description =
            "In the interests of keeping code clean, the simplest possible conditional syntax should be used. That " +
            "means using the \"??\" operator for an assign-if-not-null operator, and using the ternary operator \"?:\" " +
            "for assignment to a single variable.";
        internal const string MessageFormat = "Use the \"{0}\" operator here.";
        internal const string Category = SonarLint.Common.Category.Maintainability;
        internal const Severity RuleSeverity = Severity.Minor;
        internal const bool IsActivatedByDefault = false;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        private static readonly ExpressionSyntax NullExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

        internal const string IsNullCoalescingKey = "isNullCoalescing";

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckConditionalExpression(c),
                SyntaxKind.ConditionalExpression);

            context.RegisterSyntaxNodeActionInNonGenerated(
                c => CheckIfStatement(c),
                SyntaxKind.IfStatement);
        }

        private static void CheckIfStatement(SyntaxNodeAnalysisContext c)
        {
            var ifStatement = (IfStatementSyntax)c.Node;
            if (ifStatement.Else == null ||
                ifStatement.Parent is ElseClauseSyntax)
            {
                return;
            }

            var whenTrue = ExtractSingleStatement(ifStatement.Statement);
            var whenFalse = ExtractSingleStatement(ifStatement.Else.Statement);

            if (whenTrue == null ||
                whenFalse == null ||
                EquivalenceChecker.AreEquivalent(whenTrue, whenFalse))
            {
                /// Equivalence handled by S1871, <see cref="ConditionalStructureSameImplementation"/>
                return;
            }

            ExpressionSyntax comparedToNull;
            bool comparedIsNullInTrue;
            var possiblyNullCoalescing =
                TryGetExpressionComparedToNull(ifStatement.Condition, out comparedToNull, out comparedIsNullInTrue) &&
                ExpressionCanBeNull(comparedToNull, c.SemanticModel);

            bool isNullCoalescing;
            if (CanBeSimplified(whenTrue, whenFalse,
                    possiblyNullCoalescing ? comparedToNull : null, comparedIsNullInTrue,
                    c.SemanticModel, out isNullCoalescing))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, ifStatement.IfKeyword.GetLocation(),
                    ImmutableDictionary<string, string>.Empty.Add(IsNullCoalescingKey, isNullCoalescing.ToString()),
                    isNullCoalescing ? "??" : "?:"));
            }
        }

        private static void CheckConditionalExpression(SyntaxNodeAnalysisContext c)
        {
            var conditional = (ConditionalExpressionSyntax)c.Node;

            var condition = TernaryOperatorPointless.RemoveParentheses(conditional.Condition);
            var whenTrue = TernaryOperatorPointless.RemoveParentheses(conditional.WhenTrue);
            var whenFalse = TernaryOperatorPointless.RemoveParentheses(conditional.WhenFalse);

            if (EquivalenceChecker.AreEquivalent(whenTrue, whenFalse))
            {
                /// handled by S2758, <see cref="TernaryOperatorPointless"/>
                return;
            }

            ExpressionSyntax comparedToNull;
            bool comparedIsNullInTrue;
            if (!TryGetExpressionComparedToNull(condition, out comparedToNull, out comparedIsNullInTrue) ||
                !ExpressionCanBeNull(comparedToNull, c.SemanticModel))
            {
                // expression not compared to null, or can't be null
                return;
            }

            if (CanExpressionBeNullCoalescing(whenTrue, whenFalse, comparedToNull, comparedIsNullInTrue, c.SemanticModel))
            {
                c.ReportDiagnostic(Diagnostic.Create(Rule, conditional.GetLocation(), "??"));
            }
        }

        private static bool AreTypesCompatible(ExpressionSyntax expression1, ExpressionSyntax expression2, SemanticModel semanticModel)
        {
            var type1 = semanticModel.GetTypeInfo(expression1).Type;
            var type2 = semanticModel.GetTypeInfo(expression2).Type;

            if (type1 is IErrorTypeSymbol || type2 is IErrorTypeSymbol)
            {
                return false;
            }

            if (type1 == null || type2 == null)
            {
                return true;
            }

            return type1.Equals(type2);
        }

        private static bool CanBeSimplified(StatementSyntax statement1, StatementSyntax statement2,
            ExpressionSyntax comparedToNull, bool comparedIsNullInTrue, SemanticModel semanticModel, out bool isNullCoalescing)
        {
            isNullCoalescing = false;
            var return1 = statement1 as ReturnStatementSyntax;
            var return2 = statement2 as ReturnStatementSyntax;

            if (return1 != null && return2 != null)
            {
                var retExpr1 = TernaryOperatorPointless.RemoveParentheses(return1.Expression);
                var retExpr2 = TernaryOperatorPointless.RemoveParentheses(return2.Expression);

                if (!AreTypesCompatible(return1.Expression, return2.Expression, semanticModel))
                {
                    return false;
                }

                if (comparedToNull != null &&
                    CanExpressionBeNullCoalescing(retExpr1, retExpr2, comparedToNull, comparedIsNullInTrue, semanticModel))
                {
                    isNullCoalescing = true;
                }

                return true;
            }

            var expressionStatement1 = statement1 as ExpressionStatementSyntax;
            var expressionStatement2 = statement2 as ExpressionStatementSyntax;

            if (expressionStatement1 == null || expressionStatement2 == null)
            {
                return false;
            }

            var expression1 = TernaryOperatorPointless.RemoveParentheses(expressionStatement1.Expression);
            var expression2 = TernaryOperatorPointless.RemoveParentheses(expressionStatement2.Expression);

            if (AreCandidateAssignments(expression1, expression2, comparedToNull, comparedIsNullInTrue,
                    semanticModel, out isNullCoalescing))
            {
                return true;
            }

            if (comparedToNull != null &&
                CanExpressionBeNullCoalescing(expression1, expression2, comparedToNull, comparedIsNullInTrue, semanticModel))
            {
                isNullCoalescing = true;
                return true;
            }

            if (AreCandidateInvocationsForTernary(expression1, expression2, semanticModel))
            {
                return true;
            }

            return false;
        }

        private static bool AreCandidateAssignments(ExpressionSyntax expression1, ExpressionSyntax expression2,
            ExpressionSyntax compared, bool comparedIsNullInTrue, SemanticModel semanticModel, out bool isNullCoalescing)
        {
            isNullCoalescing = false;
            var assignment1 = expression1 as AssignmentExpressionSyntax;
            var assignment2 = expression2 as AssignmentExpressionSyntax;
            var canBeSimplified =
                assignment1 != null &&
                assignment2 != null &&
                EquivalenceChecker.AreEquivalent(assignment1.Left, assignment2.Left) &&
                assignment1.Kind() == assignment2.Kind();

            if (!canBeSimplified)
            {
                return false;
            }

            if (!AreTypesCompatible(assignment1.Right, assignment2.Right, semanticModel))
            {
                return false;
            }

            if (compared != null &&
                CanExpressionBeNullCoalescing(assignment1.Right, assignment2.Right, compared, comparedIsNullInTrue, semanticModel))
            {
                isNullCoalescing = true;
            }

            return true;
        }

        internal static StatementSyntax ExtractSingleStatement(StatementSyntax statement)
        {
            var block = statement as BlockSyntax;
            if (block != null)
            {
                if (block.Statements.Count != 1)
                {
                    return null;
                }
                return block.Statements.First();
            }

            return statement;
        }

        private static bool AreCandidateInvocationsForNullCoalescing(ExpressionSyntax expression1, ExpressionSyntax expression2,
            ExpressionSyntax comparedToNull, bool comparedIsNullInTrue,
            SemanticModel semanticModel)
        {
            return AreCandidateInvocations(expression1, expression2, comparedToNull, comparedIsNullInTrue, semanticModel);
        }

        private static bool AreCandidateInvocationsForTernary(ExpressionSyntax expression1, ExpressionSyntax expression2,
            SemanticModel semanticModel)
        {
            return AreCandidateInvocations(expression1, expression2, null, false, semanticModel);
        }

        private static bool AreCandidateInvocations(ExpressionSyntax expression1, ExpressionSyntax expression2,
            ExpressionSyntax comparedToNull, bool comparedIsNullInTrue,
            SemanticModel semanticModel)
        {
            var methodCall1 = expression1 as InvocationExpressionSyntax;
            var methodCall2 = expression2 as InvocationExpressionSyntax;

            if (methodCall1 == null || methodCall2 == null)
            {
                return false;
            }

            var methodSymbol1 = semanticModel.GetSymbolInfo(methodCall1).Symbol;
            var methodSymbol2 = semanticModel.GetSymbolInfo(methodCall2).Symbol;

            if (methodSymbol1 == null ||
                methodSymbol2 == null ||
                !methodSymbol1.Equals(methodSymbol2))
            {
                return false;
            }

            if (methodCall1.ArgumentList == null ||
                methodCall2.ArgumentList == null ||
                methodCall1.ArgumentList.Arguments.Count != methodCall2.ArgumentList.Arguments.Count)
            {
                return false;
            }

            var numberOfDifferences = 0;
            var numberOfComparisonsToCondition = 0;
            for (int i = 0; i < methodCall1.ArgumentList.Arguments.Count; i++)
            {
                var arg1 = methodCall1.ArgumentList.Arguments[i];
                var arg2 = methodCall2.ArgumentList.Arguments[i];

                if (!EquivalenceChecker.AreEquivalent(arg1.Expression, arg2.Expression))
                {
                    numberOfDifferences++;

                    if (comparedToNull != null)
                    {
                        var arg1IsComparedToNull = EquivalenceChecker.AreEquivalent(arg1.Expression, comparedToNull);
                        var arg2IsComparedToNull = EquivalenceChecker.AreEquivalent(arg2.Expression, comparedToNull);

                        if (arg1IsComparedToNull && !comparedIsNullInTrue)
                        {
                            numberOfComparisonsToCondition++;
                        }

                        if (arg2IsComparedToNull && comparedIsNullInTrue)
                        {
                            numberOfComparisonsToCondition++;
                        }

                        if (!AreTypesCompatible(arg1.Expression, arg2.Expression, semanticModel))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (comparedToNull != null && EquivalenceChecker.AreEquivalent(arg1.Expression, comparedToNull))
                    {
                        return false;
                    }
                }
            }

            return numberOfDifferences == 1 && (comparedToNull == null || numberOfComparisonsToCondition == 1);
        }

        private static bool CanExpressionBeNullCoalescing(ExpressionSyntax whenTrue, ExpressionSyntax whenFalse,
            ExpressionSyntax comparedToNull, bool comparedIsNullInTrue, SemanticModel semanticModel)
        {
            if (EquivalenceChecker.AreEquivalent(whenTrue, comparedToNull))
            {
                return !comparedIsNullInTrue;
            }

            if (EquivalenceChecker.AreEquivalent(whenFalse, comparedToNull))
            {
                return comparedIsNullInTrue;
            }

            return AreCandidateInvocationsForNullCoalescing(whenTrue, whenFalse, comparedToNull,
                comparedIsNullInTrue, semanticModel);
        }

        internal static bool TryGetExpressionComparedToNull(ExpressionSyntax expression,
            out ExpressionSyntax compared, out bool comparedIsNullInTrue)
        {
            compared = null;
            comparedIsNullInTrue = false;
            var binary = expression as BinaryExpressionSyntax;
            if (binary == null ||
                !EqualsOrNotEquals.Contains(binary.Kind()))
            {
                return false;
            }

            comparedIsNullInTrue = binary.IsKind(SyntaxKind.EqualsExpression);

            if (EquivalenceChecker.AreEquivalent(binary.Left, NullExpression))
            {
                compared = binary.Right;
                return true;
            }

            if (EquivalenceChecker.AreEquivalent(binary.Right, NullExpression))
            {
                compared = binary.Left;
                return true;
            }

            return false;
        }

        private static bool ExpressionCanBeNull(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var expressionType = semanticModel.GetTypeInfo(expression).Type;
            return expressionType != null &&
                   (expressionType.IsReferenceType ||
                    expressionType.SpecialType == SpecialType.System_Nullable_T);
        }

        private static readonly SyntaxKind[] EqualsOrNotEquals = { SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression };
    }
}
