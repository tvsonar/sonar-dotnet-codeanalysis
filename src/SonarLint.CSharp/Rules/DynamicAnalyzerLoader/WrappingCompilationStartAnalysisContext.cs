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
    public class WrappingCompilationStartAnalysisContext : CompilationStartAnalysisContext
    {
        private readonly CompilationStartAnalysisContext context;
        private readonly WrappingAnalyzer wrappingAnalyzer;

        public WrappingCompilationStartAnalysisContext(CompilationStartAnalysisContext context,
            WrappingAnalyzer wrappingAnalyzer)
            : this(context.Compilation, context.Options, context.CancellationToken)
        {
            this.context = context;
            this.wrappingAnalyzer = wrappingAnalyzer;
        }

        private WrappingCompilationStartAnalysisContext(Compilation compilation, AnalyzerOptions options, CancellationToken cancellationToken)
            : base(compilation, options, cancellationToken)
        {
        }

        public override void RegisterCompilationEndAction(Action<CompilationAnalysisContext> action)
        {
            context.RegisterCompilationEndAction(
                c => action(wrappingAnalyzer.GetWrappedCompilationAnalysisContext(c)));
        }

        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action)
        {
            context.RegisterSemanticModelAction(
                c => action(wrappingAnalyzer.GetWrappedSemanticModelAnalysisContext(c)));
        }

        public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds)
        {
            context.RegisterSymbolAction(
                c => action(wrappingAnalyzer.GetWrappedSymbolAnalysisContext(c)),
                symbolKinds);
        }

        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action)
        {
            context.RegisterCodeBlockStartAction<TLanguageKindEnum>(
                c => action(new WrappingCodeBlockStartAnalysisContext<TLanguageKindEnum>(c, this.wrappingAnalyzer)));
        }

        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action)
        {
            context.RegisterCodeBlockAction(
                c => action(wrappingAnalyzer.GetWrappedCodeBlockAnalysisContext(c)));
        }

        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action)
        {
            context.RegisterSyntaxTreeAction(
                c => action(wrappingAnalyzer.GetWrappedSyntaxTreeAnalysisContext(c)));
        }

        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(
            Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds)
        {
            context.RegisterSyntaxNodeAction(
                c => action(wrappingAnalyzer.GetWrappedSyntaxNodeAnalysisContext(c)),
                syntaxKinds);
        }
    }
}