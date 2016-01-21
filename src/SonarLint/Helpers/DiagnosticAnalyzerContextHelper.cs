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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SonarLint.Helpers
{
    public static class DiagnosticAnalyzerContextHelper
    {
        #region Register*ActionInNonGenerated

        public static void RegisterSyntaxNodeActionInNonGenerated<TLanguageKindEnum>(
            this AnalysisContext context,
            GeneratedCodeRecognizer generatedCodeRecognizer,
            Action<SyntaxNodeAnalysisContext> action,
            params TLanguageKindEnum[] syntaxKinds) where TLanguageKindEnum : struct
        {
            context.RegisterSyntaxNodeAction(
                c =>
                {
                    if (!c.Node.SyntaxTree.IsGenerated(generatedCodeRecognizer, c.SemanticModel.Compilation))
                    {
                        action(c);
                    }
                },
                syntaxKinds);
        }

        public static void RegisterSyntaxTreeActionInNonGenerated(
            this AnalysisContext context,
            GeneratedCodeRecognizer generatedCodeRecognizer,
            Action<SyntaxTreeAnalysisContext> action)
        {
            context.RegisterCompilationStartAction(
                csac =>
                {
                    csac.RegisterSyntaxTreeAction(
                        c =>
                        {
                            if (!c.Tree.IsGenerated(generatedCodeRecognizer, csac.Compilation))
                            {
                                action(c);
                            }
                        });
                });
        }

        public static void RegisterCodeBlockStartActionInNonGenerated<TLanguageKindEnum>(
            this AnalysisContext context,
            GeneratedCodeRecognizer generatedCodeRecognizer,
            Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) where TLanguageKindEnum : struct
        {
            context.RegisterCodeBlockStartAction<TLanguageKindEnum>(
                c =>
                {
                    if (!c.CodeBlock.SyntaxTree.IsGenerated(generatedCodeRecognizer, c.SemanticModel.Compilation))
                    {
                        action(c);
                    }
                });
        }

        #endregion

        #region ReportDiagnosticIfNonGenerated

        public static void ReportDiagnosticIfNonGenerated(
            this CompilationAnalysisContext context,
            GeneratedCodeRecognizer generatedCodeRecognizer,
            Diagnostic diagnostic,
            Compilation compilation)
        {
            if (!diagnostic.Location.SourceTree.IsGenerated(generatedCodeRecognizer, compilation))
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        public static void ReportDiagnosticIfNonGenerated(
            this SymbolAnalysisContext context,
            GeneratedCodeRecognizer generatedCodeRecognizer,
            Diagnostic diagnostic,
            Compilation compilation)
        {
            if (!diagnostic.Location.SourceTree.IsGenerated(generatedCodeRecognizer, compilation))
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        public static void ReportDiagnosticIfNonGenerated(
            this SymbolAnalysisContext context,
            GeneratedCodeRecognizer generatedCodeRecognizer,
            Diagnostic diagnostic)
        {
            context.ReportDiagnosticIfNonGenerated(generatedCodeRecognizer, diagnostic, context.Compilation);
        }

        #endregion

        #region SyntaxTree.IsGenerated

        private static readonly ConditionalWeakTable<Compilation, ConcurrentDictionary<SyntaxTree, bool>> Cache
            = new ConditionalWeakTable<Compilation, ConcurrentDictionary<SyntaxTree, bool>>();

        private static bool IsGenerated(this SyntaxTree tree,
            GeneratedCodeRecognizer generatedCodeRecognizer,
            Compilation compilation)
        {
            if (tree == null)
            {
                return false;
            }

            //this is locking if the compilation is not present in the Cache.
            var cache = Cache.GetOrCreateValue(compilation);
            bool result;
            if (cache.TryGetValue(tree, out result))
            {
                return result;
            }

            var generated = generatedCodeRecognizer.IsGenerated(tree);
            cache.TryAdd(tree, generated);
            return generated;
        }

        #endregion
    }
}
