using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.VisualBasic;
using SonarAnalyzer.Common;
using SonarAnalyzer.Common.Sqale;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SonarAnalyzer.Rules
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [SqaleConstantRemediation("5min")]
    [SqaleSubCharacteristic(SqaleSubCharacteristic.Understandability)]
    [Rule(DiagnosticId, RuleSeverity, Title, IsActivatedByDefault)]
    [Tags(Tag.Suspicious, Tag.Performance)]
    public class UseShortCircuitingOperators : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "?XXX";
        internal const string Title = "Only use short-circuiting operators";
        internal const string Description =
            "Only use short-circuiting operators, as further analyzing the expression will not change the outcome.";
        internal const string MessageFormat = "Use the short-circuiting alternative {0}.";
        internal const string Category = SonarAnalyzer.Common.Category.Performance;
        internal const Severity RuleSeverity = Severity.Major;
        internal const bool IsActivatedByDefault = true;
        private const IdeVisibility ideVisibility = IdeVisibility.Hidden;

        internal static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(ideVisibility), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description,
                customTags: ideVisibility.ToCustomTags());

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(DetectUsageOfNonShortCircuitingOperators,
                SyntaxKind.AndExpression,
                SyntaxKind.OrExpression);
        }

        private void DetectUsageOfNonShortCircuitingOperators(SyntaxNodeAnalysisContext context)
        {
            var alternative = ShortCircuitingAlternative[context.Node.Kind()];
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), alternative));
        }

        private readonly Dictionary<SyntaxKind, SyntaxKind> ShortCircuitingAlternative = new Dictionary<SyntaxKind, SyntaxKind>()
        {
            { SyntaxKind.AndExpression,SyntaxKind.AndAlsoExpression },
            { SyntaxKind.OrExpression,SyntaxKind.OrElseExpression },
        };
    }
}
