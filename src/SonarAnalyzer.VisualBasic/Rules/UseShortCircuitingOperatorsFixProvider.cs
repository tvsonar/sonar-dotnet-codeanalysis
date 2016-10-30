using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Helpers;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace SonarAnalyzer.Rules
{
    [ExportCodeFixProvider(LanguageNames.VisualBasic)]
    public class UseShortCircuitingOperatorsFixProvider : SonarCodeFixProvider
    {
        internal const string Title = "Use short-circuiting operators";

        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(UseShortCircuitingOperators.DiagnosticId);
            }
        }

        protected override async Task RegisterCodeFixesAsync(SyntaxNode root, CodeFixContext context)
        {
            context.RegisterCodeFix(
                CodeAction.Create(Title, c => ReplaceOperator(root, context), Title),
                context.Diagnostics);
        }

        private Task<Document> ReplaceOperator(SyntaxNode root, CodeFixContext context)
        {
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true) as BinaryExpressionSyntax;
            var replacement = GetShortCircuitingExpressionNode(node);
            var newRoot = root.ReplaceNode(node, replacement);
            return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
        }

        private SyntaxNode GetShortCircuitingExpressionNode(BinaryExpressionSyntax node)
        {
            var kind = node.Kind();
            if(kind == SyntaxKind.AndExpression)
            {
                return SyntaxFactory.AndAlsoExpression(node.Left, node.Right);
            }
            if(kind == SyntaxKind.OrExpression)
            {
                return SyntaxFactory.OrElseExpression(node.Left, node.Right);
            }
            return node;
        }
    }
}
