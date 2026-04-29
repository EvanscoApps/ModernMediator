using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ModernMediator.Generators
{
    /// <summary>
    /// Diagnostic analyzer that surfaces dispatcher overload mismatch at compile time.
    /// When the consumer calls Send or SendAsync against a request whose handler is
    /// registered under the alternate dispatch interface, MM009 fires at the call site
    /// before the code runs. Companion to the runtime MM200 check (ADR-009); see ADR-010.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DispatcherOverloadMismatchAnalyzer : DiagnosticAnalyzer
    {
        private const string IRequestHandlerMetadataName = "ModernMediator.IRequestHandler`2";
        private const string IValueTaskRequestHandlerMetadataName = "ModernMediator.IValueTaskRequestHandler`2";
        private const string IMediatorMetadataName = "ModernMediator.IMediator";
        private const string ISenderMetadataName = "ModernMediator.ISender";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.DispatcherOverloadMismatch);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var taskHandlerSymbol = context.Compilation.GetTypeByMetadataName(IRequestHandlerMetadataName);
            var valueTaskHandlerSymbol = context.Compilation.GetTypeByMetadataName(IValueTaskRequestHandlerMetadataName);

            // If neither dispatch interface is in the compilation, the analyzer has nothing to do.
            if (taskHandlerSymbol == null && valueTaskHandlerSymbol == null)
                return;

            var registry = BuildRegistry(context.Compilation, taskHandlerSymbol, valueTaskHandlerSymbol);

            var mediatorSymbol = context.Compilation.GetTypeByMetadataName(IMediatorMetadataName);
            var senderSymbol = context.Compilation.GetTypeByMetadataName(ISenderMetadataName);

            context.RegisterSyntaxNodeAction(
                c => AnalyzeInvocation(c, registry, mediatorSymbol, senderSymbol),
                SyntaxKind.InvocationExpression);
        }

        private static Dictionary<ITypeSymbol, RegistryEntry> BuildRegistry(
            Compilation compilation,
            INamedTypeSymbol? taskHandlerSymbol,
            INamedTypeSymbol? valueTaskHandlerSymbol)
        {
            var registry = new Dictionary<ITypeSymbol, RegistryEntry>(SymbolEqualityComparer.Default);
            VisitNamespace(compilation.GlobalNamespace, taskHandlerSymbol, valueTaskHandlerSymbol, registry);
            return registry;
        }

        private static void VisitNamespace(
            INamespaceSymbol ns,
            INamedTypeSymbol? taskHandlerSymbol,
            INamedTypeSymbol? valueTaskHandlerSymbol,
            Dictionary<ITypeSymbol, RegistryEntry> registry)
        {
            foreach (var type in ns.GetTypeMembers())
                VisitType(type, taskHandlerSymbol, valueTaskHandlerSymbol, registry);

            foreach (var childNs in ns.GetNamespaceMembers())
                VisitNamespace(childNs, taskHandlerSymbol, valueTaskHandlerSymbol, registry);
        }

        private static void VisitType(
            INamedTypeSymbol type,
            INamedTypeSymbol? taskHandlerSymbol,
            INamedTypeSymbol? valueTaskHandlerSymbol,
            Dictionary<ITypeSymbol, RegistryEntry> registry)
        {
            if (!type.IsAbstract && (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct))
            {
                foreach (var iface in type.AllInterfaces)
                {
                    if (iface.TypeArguments.Length != 2)
                        continue;

                    var requestType = iface.TypeArguments[0];
                    var def = iface.OriginalDefinition;

                    if (taskHandlerSymbol != null && SymbolEqualityComparer.Default.Equals(def, taskHandlerSymbol))
                    {
                        registry.TryGetValue(requestType, out var entry);
                        registry[requestType] = new RegistryEntry(true, entry.HasValueTask);
                    }
                    else if (valueTaskHandlerSymbol != null && SymbolEqualityComparer.Default.Equals(def, valueTaskHandlerSymbol))
                    {
                        registry.TryGetValue(requestType, out var entry);
                        registry[requestType] = new RegistryEntry(entry.HasTask, true);
                    }
                }
            }

            foreach (var nested in type.GetTypeMembers())
                VisitType(nested, taskHandlerSymbol, valueTaskHandlerSymbol, registry);
        }

        private static void AnalyzeInvocation(
            SyntaxNodeAnalysisContext context,
            Dictionary<ITypeSymbol, RegistryEntry> registry,
            INamedTypeSymbol? mediatorSymbol,
            INamedTypeSymbol? senderSymbol)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            var methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName != "Send" && methodName != "SendAsync")
                return;

            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                return;

            if (!IsDispatchMethod(methodSymbol, mediatorSymbol, senderSymbol))
                return;

            var requestType = ResolveRequestType(methodSymbol, invocation, context.SemanticModel);
            if (requestType == null)
                return;

            if (!registry.TryGetValue(requestType, out var entry))
                return;

            // Both interfaces registered: either dispatch path is valid.
            if (entry.HasTask && entry.HasValueTask)
                return;

            string registeredInterface;
            string suggestedMethod;
            string calledMethod = methodName;

            if (methodName == "Send" && !entry.HasTask && entry.HasValueTask)
            {
                registeredInterface = "IValueTaskRequestHandler";
                suggestedMethod = "SendAsync";
            }
            else if (methodName == "SendAsync" && !entry.HasValueTask && entry.HasTask)
            {
                registeredInterface = "IRequestHandler";
                suggestedMethod = "Send";
            }
            else
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DispatcherOverloadMismatch,
                invocation.GetLocation(),
                requestType.Name,
                registeredInterface,
                suggestedMethod,
                calledMethod));
        }

        private static bool IsDispatchMethod(
            IMethodSymbol methodSymbol,
            INamedTypeSymbol? mediatorSymbol,
            INamedTypeSymbol? senderSymbol)
        {
            var containingType = methodSymbol.ContainingType;
            if (containingType == null)
                return false;

            // Direct interface method: IMediator.Send / ISender.SendAsync etc.
            if (mediatorSymbol != null &&
                (SymbolEqualityComparer.Default.Equals(containingType, mediatorSymbol) ||
                 containingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, mediatorSymbol))))
            {
                return true;
            }

            if (senderSymbol != null &&
                (SymbolEqualityComparer.Default.Equals(containingType, senderSymbol) ||
                 containingType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, senderSymbol))))
            {
                return true;
            }

            // Generated extension method: first parameter is IMediator or ISender.
            if (methodSymbol.IsExtensionMethod && methodSymbol.Parameters.Length > 0)
            {
                var firstParamType = methodSymbol.Parameters[0].Type;
                if (mediatorSymbol != null && SymbolEqualityComparer.Default.Equals(firstParamType, mediatorSymbol))
                    return true;
                if (senderSymbol != null && SymbolEqualityComparer.Default.Equals(firstParamType, senderSymbol))
                    return true;
                if (mediatorSymbol != null && firstParamType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, mediatorSymbol)))
                    return true;
                if (senderSymbol != null && firstParamType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, senderSymbol)))
                    return true;
            }

            return false;
        }

        private static ITypeSymbol? ResolveRequestType(
            IMethodSymbol methodSymbol,
            InvocationExpressionSyntax invocation,
            SemanticModel semanticModel)
        {
            // For both the runtime IMediator.Send<TResponse>(IRequest<TResponse>) signature
            // and the generated MediatorSendExtensions.Send(this IMediator, TRequest) signature,
            // the request expression is the first non-receiver argument. Read its compile-time type.
            if (invocation.ArgumentList.Arguments.Count == 0)
                return null;

            var firstArg = invocation.ArgumentList.Arguments[0].Expression;
            var typeInfo = semanticModel.GetTypeInfo(firstArg);
            return typeInfo.Type;
        }

        private readonly struct RegistryEntry
        {
            public RegistryEntry(bool hasTask, bool hasValueTask)
            {
                HasTask = hasTask;
                HasValueTask = hasValueTask;
            }

            public bool HasTask { get; }
            public bool HasValueTask { get; }
        }
    }
}
