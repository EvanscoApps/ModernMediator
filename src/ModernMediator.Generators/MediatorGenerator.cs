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
                   classDecl.BaseList is not null;
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
                {
                    // Report MM003 if abstract class implements any handler interface
                    bool implementsHandlerInterface = classSymbol.AllInterfaces.Any(iface =>
                    {
                        var fn = GetFullMetadataName(iface.OriginalDefinition);
                        return fn == IRequestHandler || fn == IStreamRequestHandler ||
                               fn == IPipelineBehavior || fn == IRequestPreProcessor ||
                               fn == IRequestPostProcessor;
                    });

                    if (implementsHandlerInterface)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.HandlerMustBeNonAbstract,
                            classSymbol.Locations.FirstOrDefault(),
                            classSymbol.Name));
                    }
                    continue;
                }

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

            // Resolve IRequest<TResponse> once for MM004 and MM002 checks
            var requestInterfaceSymbol = compilation.GetTypeByMetadataName("ModernMediator.IRequest`1");

            // Check for handler return type mismatch (MM004)
            if (requestInterfaceSymbol != null)
            {
                foreach (var handler in handlers)
                {
                    var requestIface = handler.RequestType.AllInterfaces
                        .FirstOrDefault(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, requestInterfaceSymbol));
                    if (requestIface != null && handler.ResponseType != null)
                    {
                        var expectedResponseType = requestIface.TypeArguments[0];
                        if (!SymbolEqualityComparer.Default.Equals(expectedResponseType, handler.ResponseType))
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.HandlerReturnTypeMismatch,
                                handler.HandlerType.Locations.FirstOrDefault(),
                                handler.HandlerType.Name,
                                GetFullTypeName(handler.ResponseType),
                                GetFullTypeName(handler.RequestType),
                                GetFullTypeName(expectedResponseType)));
                        }
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

            // Check for request types with no handler (MM002)
            if (requestInterfaceSymbol != null)
            {
                var handledRequestTypeNames = new HashSet<string>(
                    handlers.Select(h => GetFullTypeName(h.RequestType)));

                var allRequestTypes = new List<INamedTypeSymbol>();
                FindRequestTypesInNamespace(compilation.GlobalNamespace, requestInterfaceSymbol, allRequestTypes);

                foreach (var requestType in allRequestTypes)
                {
                    var requestTypeName = GetFullTypeName(requestType);
                    if (!handledRequestTypeNames.Contains(requestTypeName))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.NoHandlerFound,
                            requestType.Locations.FirstOrDefault(),
                            requestTypeName));
                    }
                }
            }

            // Generate registration code
            var source = GenerateSource(handlers, streamHandlers, behaviors, preProcessors, postProcessors);
            context.AddSource("ModernMediator.Generated.g.cs", SourceText.From(source, Encoding.UTF8));

            // Generate strongly-typed Send extensions
            var sendExtensions = GenerateSendExtensions(handlers, streamHandlers);
            context.AddSource("ModernMediator.SendExtensions.g.cs", SourceText.From(sendExtensions, Encoding.UTF8));

            // Report successful generation (MM100)
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GeneratorSuccess,
                Location.None,
                handlers.Count,
                streamHandlers.Count,
                behaviors.Count,
                preProcessors.Count,
                postProcessors.Count));
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

            // Original method without configuration
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Registers all discovered handlers, behaviors, and processors.");
            sb.AppendLine("        /// Generated at compile time - no reflection used.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static IServiceCollection AddModernMediatorGenerated(this IServiceCollection services, ServiceLifetime handlerLifetime = ServiceLifetime.Transient, ServiceLifetime behaviorLifetime = ServiceLifetime.Transient)");
            sb.AppendLine("        {");
            sb.AppendLine("            return AddModernMediatorGenerated(services, null, handlerLifetime, behaviorLifetime);");
            sb.AppendLine("        }");
            sb.AppendLine();

            // New overload with configuration
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Registers all discovered handlers, behaviors, and processors with configuration.");
            sb.AppendLine("        /// Generated at compile time - no reflection used.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"services\">The service collection.</param>");
            sb.AppendLine("        /// <param name=\"configure\">Optional configuration action for ErrorPolicy, CachingMode, and Dispatcher.</param>");
            sb.AppendLine("        /// <param name=\"handlerLifetime\">Lifetime for handlers (default: Transient).</param>");
            sb.AppendLine("        /// <param name=\"behaviorLifetime\">Lifetime for behaviors and processors (default: Transient).</param>");
            sb.AppendLine("        public static IServiceCollection AddModernMediatorGenerated(this IServiceCollection services, Action<MediatorConfiguration>? configure, ServiceLifetime handlerLifetime = ServiceLifetime.Transient, ServiceLifetime behaviorLifetime = ServiceLifetime.Transient)");
            sb.AppendLine("        {");
            sb.AppendLine("            // Create and apply configuration");
            sb.AppendLine("            var config = new MediatorConfiguration();");
            sb.AppendLine("            configure?.Invoke(config);");
            sb.AppendLine();
            sb.AppendLine("            // Register the mediator as scoped to support scoped dependencies");
            sb.AppendLine("            services.TryAddScoped<IMediator>(sp =>");
            sb.AppendLine("            {");
            sb.AppendLine("                var mediator = new Mediator(sp);");
            sb.AppendLine("                if (config.ErrorPolicy.HasValue)");
            sb.AppendLine("                {");
            sb.AppendLine("                    mediator.ErrorPolicy = config.ErrorPolicy.Value;");
            sb.AppendLine("                }");
            sb.AppendLine("                mediator.SetCachingMode(config.CachingMode);");
            sb.AppendLine("                config.ApplyConfiguration(mediator);");
            sb.AppendLine("                return mediator;");
            sb.AppendLine("            });");
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

        private static void FindRequestTypesInNamespace(INamespaceSymbol ns, INamedTypeSymbol requestInterface, List<INamedTypeSymbol> result)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                FindRequestTypesInType(type, requestInterface, result);
            }

            foreach (var childNs in ns.GetNamespaceMembers())
            {
                FindRequestTypesInNamespace(childNs, requestInterface, result);
            }
        }

        private static void FindRequestTypesInType(INamedTypeSymbol type, INamedTypeSymbol requestInterface, List<INamedTypeSymbol> result)
        {
            if (!type.IsAbstract &&
                (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) &&
                type.Locations.Any(l => l.IsInSource) &&
                type.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, requestInterface)))
            {
                result.Add(type);
            }

            foreach (var nestedType in type.GetTypeMembers())
            {
                FindRequestTypesInType(nestedType, requestInterface, result);
            }
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