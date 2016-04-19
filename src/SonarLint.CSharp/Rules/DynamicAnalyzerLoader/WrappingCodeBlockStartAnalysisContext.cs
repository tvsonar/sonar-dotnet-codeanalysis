/*
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

using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SonarLint.Rules.CSharp.DynamicAnalyzerLoader
{
    public class WrappingCodeBlockStartAnalysisContext<TLanguageKindEnum> : CodeBlockStartAnalysisContext<TLanguageKindEnum>
        where TLanguageKindEnum : struct
    {
        private readonly CodeBlockStartAnalysisContext<TLanguageKindEnum> context;
        private readonly WrappingAnalyzer wrappingAnalyzer;

        public WrappingCodeBlockStartAnalysisContext(CodeBlockStartAnalysisContext<TLanguageKindEnum> context,
            WrappingAnalyzer wrappingAnalyzer)
            : this (context.CodeBlock, context.OwningSymbol, context.SemanticModel, context.Options, context.CancellationToken)
        {
            this.context = context;
            this.wrappingAnalyzer = wrappingAnalyzer;
        }

        private WrappingCodeBlockStartAnalysisContext(SyntaxNode codeBlock, ISymbol owningSymbol,
            SemanticModel semanticModel, AnalyzerOptions options, CancellationToken cancellationToken)
            : base(codeBlock, owningSymbol, semanticModel, options, cancellationToken)
        {
        }

        public override void RegisterCodeBlockEndAction(Action<CodeBlockAnalysisContext> action)
        {
            context.RegisterCodeBlockEndAction(
                c => action(wrappingAnalyzer.GetWrappedCodeBlockAnalysisContext(c)));
        }

        public override void RegisterSyntaxNodeAction(Action<SyntaxNodeAnalysisContext> action,
            ImmutableArray<TLanguageKindEnum> syntaxKinds)
        {
            context.RegisterSyntaxNodeAction(
                c => action(wrappingAnalyzer.GetWrappedSyntaxNodeAnalysisContext(c)),
                syntaxKinds);
        }
    }
}