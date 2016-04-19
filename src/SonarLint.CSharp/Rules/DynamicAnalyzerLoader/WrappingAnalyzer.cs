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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Helpers;

namespace SonarLint.Rules.CSharp.DynamicAnalyzerLoader
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WrappingAnalyzer : SonarDiagnosticAnalyzer
    {
        public static readonly ISet<string> AnalyzerPaths = new HashSet<string>();

        private readonly ICollection<DiagnosticAnalyzer> analyzers;
        private readonly Dictionary<string, DiagnosticDescriptor> newDiagnosticDescriptors =
            new Dictionary<string, DiagnosticDescriptor>();

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            newDiagnosticDescriptors.Values.ToImmutableArray();

        public WrappingAnalyzer()
        {
            if (!AnalyzerPaths.Any())
            {
                return;
            }

            var analyzerTypes = GetAnalyzerTypes();

            analyzers = analyzerTypes
                .Select(InvokeDefaultConstructor)
                .Where(analyzer => analyzer != null)
                .ToList();

            var supportedDiagnostics = analyzers
                .SelectMany(a => a.SupportedDiagnostics);

            foreach (var supportedDiagnostic in supportedDiagnostics)
            {
                var newDiagnosticDescriptor = GetEnabledNonConfigurableDiagnosticDescriptor(supportedDiagnostic);
                newDiagnosticDescriptors.Add(supportedDiagnostic.Id, newDiagnosticDescriptor);
            }
        }

        private static DiagnosticDescriptor GetEnabledNonConfigurableDiagnosticDescriptor(DiagnosticDescriptor supportedDiagnostic)
        {
            return new DiagnosticDescriptor(supportedDiagnostic.Id,
                supportedDiagnostic.Title,
                supportedDiagnostic.MessageFormat,
                supportedDiagnostic.Category,
                supportedDiagnostic.DefaultSeverity,
                true,
                supportedDiagnostic.Description,
                supportedDiagnostic.HelpLinkUri,
                    new[] { WellKnownDiagnosticTags.NotConfigurable }
                        .Concat(supportedDiagnostic.CustomTags ?? Enumerable.Empty<string>())
                        .ToArray());
        }

        private static DiagnosticAnalyzer InvokeDefaultConstructor(Type analyzerType)
        {
            // reflection to create analyzers
            return (DiagnosticAnalyzer)analyzerType.GetConstructors()
                .FirstOrDefault(ct => !ct.GetParameters().Any())?.Invoke(null);
        }

        private static IEnumerable<Type> GetAnalyzerTypes()
        {
            // reflection to get the analyzer types
            var assemblies = AnalyzerPaths.Select(Assembly.LoadFrom);
            var analyzerTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    typeof (DiagnosticAnalyzer).IsAssignableFrom(t) &&
                    t.GetCustomAttribute<DiagnosticAnalyzerAttribute>() != null &&
                    !t.IsAbstract &&
                    t.IsClass);

            return analyzerTypes;
        }

        protected override void Initialize(SonarAnalysisContext context)
        {
            foreach (var analyzer in analyzers)
            {
                analyzer.Initialize(new WrappingAnalysisContext(context, this));
            }
        }

        #region GetWrapped*Context

        public SyntaxNodeAnalysisContext GetWrappedSyntaxNodeAnalysisContext(SyntaxNodeAnalysisContext context)
        {
            return new SyntaxNodeAnalysisContext(
                context.Node,
                context.SemanticModel,
                context.Options,
                diagnostic => ReportIfEnabled(context.ReportDiagnostic, diagnostic),
                d => true,
                context.CancellationToken);
        }

        public SyntaxTreeAnalysisContext GetWrappedSyntaxTreeAnalysisContext(SyntaxTreeAnalysisContext context)
        {
            return new SyntaxTreeAnalysisContext(
                context.Tree,
                context.Options,
                diagnostic => ReportIfEnabled(context.ReportDiagnostic, diagnostic),
                d => true,
                context.CancellationToken);
        }

        public CodeBlockAnalysisContext GetWrappedCodeBlockAnalysisContext(CodeBlockAnalysisContext context)
        {
            return new CodeBlockAnalysisContext(
                context.CodeBlock,
                context.OwningSymbol,
                context.SemanticModel,
                context.Options,
                diagnostic => ReportIfEnabled(context.ReportDiagnostic, diagnostic),
                d => true,
                context.CancellationToken);
        }

        public SymbolAnalysisContext GetWrappedSymbolAnalysisContext(SymbolAnalysisContext context)
        {
            return new SymbolAnalysisContext(
                context.Symbol,
                context.Compilation,
                context.Options,
                diagnostic => ReportIfEnabled(context.ReportDiagnostic, diagnostic),
                d => true,
                context.CancellationToken);
        }

        public SemanticModelAnalysisContext GetWrappedSemanticModelAnalysisContext(SemanticModelAnalysisContext context)
        {
            return new SemanticModelAnalysisContext(
                context.SemanticModel,
                context.Options,
                diagnostic => ReportIfEnabled(context.ReportDiagnostic, diagnostic),
                d => true,
                context.CancellationToken);
        }

        public CompilationAnalysisContext GetWrappedCompilationAnalysisContext(CompilationAnalysisContext context)
        {
            return new CompilationAnalysisContext(
                context.Compilation,
                context.Options,
                diagnostic => ReportIfEnabled(context.ReportDiagnostic, diagnostic),
                d => true,
                context.CancellationToken);
        }

        #endregion

        private void ReportIfEnabled(Action<Diagnostic> reportDiagnostic, Diagnostic diagnostic)
        {
            if (WrappingAnalysisContext.DisabledDiagnosticIds.Contains(diagnostic.Id))
            {
                return;
            }

            DiagnosticDescriptor diagnosticDescriptor;
            if (!newDiagnosticDescriptors.TryGetValue(diagnostic.Id, out diagnosticDescriptor))
            {
                return;
            }

            // reflection to get the _messageArgs
            var originalMessageArgs = (object[])diagnostic.GetType()
                .GetField("_messageArgs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(diagnostic);

            var newDiagnostic = Diagnostic.Create(
                diagnosticDescriptor,
                diagnostic.Location,
                diagnostic.AdditionalLocations,
                diagnostic.Properties,
                originalMessageArgs);

            reportDiagnostic(newDiagnostic);
        }
    }
}
