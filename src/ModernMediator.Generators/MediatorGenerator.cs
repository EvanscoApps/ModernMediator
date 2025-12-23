using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ModernMediator.Generators
{
    /// <summary>
    /// Source generator that discovers handlers at compile time and generates
    /// registration code, eliminating reflection at runtime.
    /// </summary>
    [Generator]
    public class MediatorGenerator : IIncrementalGenerator
    {
        private const string IRequestHandler = "ModernMediator.IRequestHandler`2";
        private const string IStreamRequestHandler = "ModernMediator.IStreamRequestHandler`2";
        private const string IPipelineBehavior = "ModernMediator.IPipelineBehavior`2";
        private const string IRequestPreProcessor = "ModernMediator.IRequestPreProcessor`1";
        private const string IRequestPostProcessor = "ModernMediator.IRequestPostProcessor`2";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all class declarations
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsCandidateClass(s),
                    transform: static (ctx, _) => GetClassSymbol(ctx))
                .Where(static m => m is not null);

            // Combine with compilation
            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            // Generate source
            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Left, source.Right!, spc));
        }

        private static bool IsCandidateClass(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax classDecl &&
                   classDecl.BaseList is not null &&
                   !classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword);
        }

        private static INamedTypeSymbol? GetClassSymbol(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
            return symbol as INamedTypeSymbol;
        }

        private static void Execute(Compilation compilation, ImmutableArray<INamedTypeSymbol?> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
                return;

            var distinctClasses = classes
                .Where(c => c is not null)
                .Cast<INamedTypeSymbol>()
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .ToList();

            var handlers = new List<HandlerInfo>();
            var streamHandlers = new List<HandlerInfo>();
            var behaviors = new List<HandlerInfo>();
            var preProcessors = new List<HandlerInfo>();
            var postProcessors = new List<HandlerInfo>();

            foreach (var classSymbol in distinctClasses)
            {
                if (classSymbol.IsAbstract)
                    continue;

                foreach (var iface in classSymbol.AllInterfaces)
                {
                    var fullName = GetFullMetadataName(iface.OriginalDefinition);

                    if (fullName == IRequestHandler && iface.TypeArguments.Length == 2)
                    {
                        handlers.Add(new HandlerInfo
                        {
                            HandlerType = classSymbol,
                            InterfaceType = iface,
                            RequestType = iface.TypeArguments[0],
                            ResponseType = iface.TypeArguments[1]
                        });
                    }
                    else if (fullName == IStreamRequestHandler && iface.TypeArguments.Length == 2)
                    {
                        streamHandlers.Add(new HandlerInfo
                        {
                            HandlerType = classSymbol,
                            InterfaceType = iface,
                            RequestType = iface.TypeArguments[0],
                            ResponseType = iface.TypeArguments[1]
                        });
                    }
                    else if (fullName == IPipelineBehavior && iface.TypeArguments.Length == 2)
                    {
                        behaviors.Add(new HandlerInfo
                        {
                            HandlerType = classSymbol,
                            InterfaceType = iface,
                            RequestType = iface.TypeArguments[0],
                            ResponseType = iface.TypeArguments[1]
                        });
                    }
                    else if (fullName == IRequestPreProcessor && iface.TypeArguments.Length == 1)
                    {
                        preProcessors.Add(new HandlerInfo
                        {
                            HandlerType = classSymbol,
                            InterfaceType = iface,
                            RequestType = iface.TypeArguments[0],
                            ResponseType = null
                        });
                    }
                    else if (fullName == IRequestPostProcessor && iface.TypeArguments.Length == 2)
                    {
                        postProcessors.Add(new HandlerInfo
                        {
                            HandlerType = classSymbol,
                            InterfaceType = iface,
                            RequestType = iface.TypeArguments[0],
                            ResponseType = iface.TypeArguments[1]
                        });
                    }
                }
            }

            // Check for duplicate handlers
            var handlersByRequest = handlers
                .GroupBy(h => GetFullTypeName(h.RequestType), StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var dup in handlersByRequest)
            {
                var handlerNames = string.Join(", ", dup.Select(h => GetFullTypeName(h.HandlerType)));
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.DuplicateHandler,
                    Location.None,
                    dup.Key,
                    handlerNames));
            }

            // Generate registration code
            var source = GenerateSource(handlers, streamHandlers, behaviors, preProcessors, postProcessors);
            context.AddSource("ModernMediator.Generated.g.cs", SourceText.From(source, Encoding.UTF8));

            // Generate strongly-typed Send extensions
            var sendExtensions = GenerateSendExtensions(handlers, streamHandlers);
            context.AddSource("ModernMediator.SendExtensions.g.cs", SourceText.From(sendExtensions, Encoding.UTF8));
        }

        private static string GenerateSource(
            List<HandlerInfo> handlers,
            List<HandlerInfo> streamHandlers,
            List<HandlerInfo> behaviors,
            List<HandlerInfo> preProcessors,
            List<HandlerInfo> postProcessors)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
            sb.AppendLine("using ModernMediator;");
            sb.AppendLine();
            sb.AppendLine("namespace ModernMediator.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Auto-generated registration for ModernMediator handlers.");
            sb.AppendLine("    /// This eliminates reflection at runtime for AOT compatibility.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class ModernMediatorRegistration");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Registers all discovered handlers, behaviors, and processors.");
            sb.AppendLine("        /// Generated at compile time - no reflection used.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static IServiceCollection AddModernMediatorGenerated(this IServiceCollection services, ServiceLifetime handlerLifetime = ServiceLifetime.Transient, ServiceLifetime behaviorLifetime = ServiceLifetime.Transient)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Register the mediator");
            sb.AppendLine("            services.TryAddSingleton<IMediator>(sp => Mediator.Create(sp));");
            sb.AppendLine();

            // Request handlers
            if (handlers.Count > 0)
            {
                sb.AppendLine("            // Request handlers");
                foreach (var handler in handlers)
                {
                    var interfaceType = GetFullTypeName(handler.InterfaceType);
                    var handlerType = GetFullTypeName(handler.HandlerType);
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({interfaceType}), typeof({handlerType}), handlerLifetime));");
                }
                sb.AppendLine();
            }

            // Stream handlers
            if (streamHandlers.Count > 0)
            {
                sb.AppendLine("            // Stream handlers");
                foreach (var handler in streamHandlers)
                {
                    var interfaceType = GetFullTypeName(handler.InterfaceType);
                    var handlerType = GetFullTypeName(handler.HandlerType);
                    sb.AppendLine($"            services.Add(new ServiceDescriptor(typeof({interfaceType}), typeof({handlerType}), handlerLifetime));");
                }
                sb.AppendLine();
            }

            // Behaviors
            if (behaviors.Count > 0)
            {
                sb.AppendLine("            // Pipeline behaviors");
                foreach (var behavior in behaviors)
                {
                    var interfaceType = GetFullTypeName(behavior.InterfaceType);
                    var handlerType = GetFullTypeName(behavior.HandlerType);
                    sb.AppendLine($"            services.TryAddEnumerable(new ServiceDescriptor(typeof({interfaceType}), typeof({handlerType}), behaviorLifetime));");
                }
                sb.AppendLine();
            }

            // Pre-processors
            if (preProcessors.Count > 0)
            {
                sb.AppendLine("            // Pre-processors");
                foreach (var processor in preProcessors)
                {
                    var interfaceType = GetFullTypeName(processor.InterfaceType);
                    var handlerType = GetFullTypeName(processor.HandlerType);
                    sb.AppendLine($"            services.TryAddEnumerable(new ServiceDescriptor(typeof({interfaceType}), typeof({handlerType}), behaviorLifetime));");
                }
                sb.AppendLine();
            }

            // Post-processors
            if (postProcessors.Count > 0)
            {
                sb.AppendLine("            // Post-processors");
                foreach (var processor in postProcessors)
                {
                    var interfaceType = GetFullTypeName(processor.InterfaceType);
                    var handlerType = GetFullTypeName(processor.HandlerType);
                    sb.AppendLine($"            services.TryAddEnumerable(new ServiceDescriptor(typeof({interfaceType}), typeof({handlerType}), behaviorLifetime));");
                }
                sb.AppendLine();
            }

            sb.AppendLine("            return services;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Summary property
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Summary of discovered handlers.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static string Summary => \"Handlers: {handlers.Count}, StreamHandlers: {streamHandlers.Count}, Behaviors: {behaviors.Count}, PreProcessors: {preProcessors.Count}, PostProcessors: {postProcessors.Count}\";");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateSendExtensions(List<HandlerInfo> handlers, List<HandlerInfo> streamHandlers)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine("using ModernMediator;");
            sb.AppendLine();
            sb.AppendLine("namespace ModernMediator.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Strongly-typed Send extensions that bypass reflection.");
            sb.AppendLine("    /// Use these for maximum performance and AOT compatibility.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class MediatorSendExtensions");
            sb.AppendLine("    {");

            // Generate Send methods for each handler
            foreach (var handler in handlers)
            {
                var requestType = GetFullTypeName(handler.RequestType);
                var responseType = GetFullTypeName(handler.ResponseType!);
                var interfaceType = GetFullTypeName(handler.InterfaceType);

                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Sends a {handler.RequestType.Name} request without reflection.");
                sb.AppendLine("        /// </summary>");
                sb.AppendLine($"        public static async Task<{responseType}> Send(this IMediator mediator, {requestType} request, CancellationToken cancellationToken = default)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (mediator is not IServiceProviderAccessor accessor)");
                sb.AppendLine("                return await mediator.Send<" + responseType + ">(request, cancellationToken);");
                sb.AppendLine();
                sb.AppendLine($"            var handler = accessor.ServiceProvider?.GetService<{interfaceType}>();");
                sb.AppendLine("            if (handler == null)");
                sb.AppendLine($"                throw new InvalidOperationException(\"No handler registered for {handler.RequestType.Name}\");");
                sb.AppendLine();
                sb.AppendLine("            return await handler.Handle(request, cancellationToken);");
                sb.AppendLine("        }");
            }

            // Generate CreateStream methods for each stream handler
            foreach (var handler in streamHandlers)
            {
                var requestType = GetFullTypeName(handler.RequestType);
                var responseType = GetFullTypeName(handler.ResponseType!);
                var interfaceType = GetFullTypeName(handler.InterfaceType);

                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Creates a stream for {handler.RequestType.Name} without reflection.");
                sb.AppendLine("        /// </summary>");
                sb.AppendLine($"        public static IAsyncEnumerable<{responseType}> CreateStream(this IMediator mediator, {requestType} request, CancellationToken cancellationToken = default)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (mediator is not IServiceProviderAccessor accessor || accessor.ServiceProvider == null)");
                sb.AppendLine("                return mediator.CreateStream<" + responseType + ">(request, cancellationToken);");
                sb.AppendLine();
                sb.AppendLine($"            var handler = accessor.ServiceProvider.GetService<{interfaceType}>();");
                sb.AppendLine("            if (handler == null)");
                sb.AppendLine($"                throw new InvalidOperationException(\"No stream handler registered for {handler.RequestType.Name}\");");
                sb.AppendLine();
                sb.AppendLine("            return handler.Handle(request, cancellationToken);");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetFullMetadataName(ISymbol symbol)
        {
            if (symbol is INamedTypeSymbol namedType)
            {
                var ns = namedType.ContainingNamespace?.ToDisplayString();
                var name = namedType.MetadataName;
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
            return symbol.ToDisplayString();
        }

        private static string GetFullTypeName(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                .Replace("global::", "");
        }

        private class HandlerInfo
        {
            public INamedTypeSymbol HandlerType { get; set; } = null!;
            public INamedTypeSymbol InterfaceType { get; set; } = null!;
            public ITypeSymbol RequestType { get; set; } = null!;
            public ITypeSymbol? ResponseType { get; set; }
        }
    }
}