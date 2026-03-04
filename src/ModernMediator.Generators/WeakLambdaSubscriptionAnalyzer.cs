using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ModernMediator.Generators
{
    /// <summary>
    /// Diagnostic analyzer that detects Subscribe calls using lambda expressions
    /// with weak references (the default). Lambdas captured as weak references
    /// may be garbage collected immediately since nothing else holds a strong reference.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WeakLambdaSubscriptionAnalyzer : DiagnosticAnalyzer
    {
        private static readonly string[] SubscribeMethodNames =
        {
            "Subscribe",
            "SubscribeAsync",
            "SubscribeOnMainThread",
            "SubscribeAsyncOnMainThread"
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.WeakLambdaSubscription);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (!(symbolInfo.Symbol is IMethodSymbol methodSymbol))
                return;

            if (!SubscribeMethodNames.Contains(methodSymbol.Name))
                return;

            var containingType = methodSymbol.ContainingType;
            if (containingType == null)
                return;

            var iMediatorSymbol = context.Compilation.GetTypeByMetadataName("ModernMediator.IMediator");
            if (iMediatorSymbol == null)
                return;

            bool isOnMediator = SymbolEqualityComparer.Default.Equals(containingType, iMediatorSymbol) ||
                                containingType.AllInterfaces.Any(i =>
                                    SymbolEqualityComparer.Default.Equals(i, iMediatorSymbol));
            if (!isOnMediator)
                return;

            var handlerArg = FindArgumentForParameter(invocation.ArgumentList.Arguments, methodSymbol, "handler");
            if (handlerArg == null)
                return;

            bool isLambdaOrAnonymous = handlerArg is LambdaExpressionSyntax ||
                                       handlerArg is AnonymousMethodExpressionSyntax;
            if (!isLambdaOrAnonymous)
                return;

            var weakArg = FindArgumentForParameter(invocation.ArgumentList.Arguments, methodSymbol, "weak");
            if (weakArg != null)
            {
                var constantValue = context.SemanticModel.GetConstantValue(weakArg);
                if (constantValue.HasValue && constantValue.Value is bool weakValue && !weakValue)
                    return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.WeakLambdaSubscription,
                invocation.GetLocation()));
        }

        private static ExpressionSyntax? FindArgumentForParameter(
            SeparatedSyntaxList<ArgumentSyntax> arguments,
            IMethodSymbol method,
            string parameterName)
        {
            foreach (var arg in arguments)
            {
                if (arg.NameColon != null && arg.NameColon.Name.Identifier.Text == parameterName)
                    return arg.Expression;
            }

            var param = method.Parameters.FirstOrDefault(p => p.Name == parameterName);
            if (param == null)
                return null;

            int paramIndex = -1;
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (method.Parameters[i].Name == parameterName)
                {
                    paramIndex = i;
                    break;
                }
            }

            if (paramIndex >= 0 && paramIndex < arguments.Count && arguments[paramIndex].NameColon == null)
                return arguments[paramIndex].Expression;

            return null;
        }
    }
}
