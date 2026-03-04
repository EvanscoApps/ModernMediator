using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ModernMediator.AspNetCore.Generators
{
    /// <summary>
    /// Source generator that discovers <c>[Endpoint]</c>-decorated <c>IRequest&lt;TResponse&gt;</c> types
    /// and emits a <c>MapMediatorEndpoints</c> extension method on <c>IEndpointRouteBuilder</c>.
    /// </summary>
    [Generator]
    public class EndpointGenerator : IIncrementalGenerator
    {
        private const string EndpointAttributeFullName = "ModernMediator.AspNetCore.EndpointAttribute";
        private const string IRequestFullName = "ModernMediator.IRequest`1";

        private static readonly HashSet<string> ValidHttpMethods = new HashSet<string>
        {
            "GET", "POST", "PUT", "DELETE", "PATCH"
        };

        private static readonly DiagnosticDescriptor InvalidHttpMethod = new DiagnosticDescriptor(
            id: "MM200",
            title: "Invalid HTTP method in [Endpoint]",
            messageFormat: "'{0}' is not a valid HTTP method. Valid values: GET, POST, PUT, DELETE, PATCH",
            category: "ModernMediator.AspNetCore",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsCandidateClass(s),
                    transform: static (ctx, _) => GetClassSymbol(ctx))
                .Where(static m => m is not null);

            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, static (spc, source) =>
                Execute(source.Left, source.Right!, spc));
        }

        private static bool IsCandidateClass(SyntaxNode node)
        {
            return node is TypeDeclarationSyntax typeDecl &&
                   typeDecl.AttributeLists.Count > 0;
        }

        private static INamedTypeSymbol? GetClassSymbol(GeneratorSyntaxContext context)
        {
            var typeDecl = (TypeDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl);
            return symbol as INamedTypeSymbol;
        }

        private static void Execute(Compilation compilation, ImmutableArray<INamedTypeSymbol?> classes, SourceProductionContext context)
        {
            if (classes.IsDefaultOrEmpty)
                return;

            var endpointAttributeSymbol = compilation.GetTypeByMetadataName(EndpointAttributeFullName);
            var requestInterfaceSymbol = compilation.GetTypeByMetadataName(IRequestFullName);

            if (endpointAttributeSymbol == null || requestInterfaceSymbol == null)
                return;

            var endpoints = new List<EndpointInfo>();

            var distinctClasses = classes
                .Where(c => c is not null)
                .Cast<INamedTypeSymbol>()
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var classSymbol in distinctClasses)
            {
                var endpointAttr = classSymbol.GetAttributes()
                    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(
                        a.AttributeClass, endpointAttributeSymbol));

                if (endpointAttr == null)
                    continue;

                // Extract route and method from constructor args
                var args = endpointAttr.ConstructorArguments;
                if (args.Length < 1)
                    continue;

                var route = args[0].Value as string ?? "";
                var method = args.Length >= 2 ? (args[1].Value as string ?? "POST") : "POST";

                // Validate HTTP method
                if (!ValidHttpMethods.Contains(method.ToUpperInvariant()))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidHttpMethod,
                        classSymbol.Locations.FirstOrDefault(),
                        method));
                    continue;
                }

                // Check that the class implements IRequest<TResponse>
                var requestIface = classSymbol.AllInterfaces
                    .FirstOrDefault(i => i.IsGenericType &&
                                         SymbolEqualityComparer.Default.Equals(
                                             i.OriginalDefinition, requestInterfaceSymbol));

                if (requestIface == null)
                    continue;

                var responseType = requestIface.TypeArguments[0];

                endpoints.Add(new EndpointInfo
                {
                    RequestType = classSymbol,
                    ResponseType = responseType,
                    Route = route,
                    Method = method.ToUpperInvariant()
                });
            }

            if (endpoints.Count == 0)
                return;

            var source = GenerateSource(endpoints);
            context.AddSource("MediatorEndpoints.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private static string GenerateSource(List<EndpointInfo> endpoints)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using Microsoft.AspNetCore.Builder;");
            sb.AppendLine("using Microsoft.AspNetCore.Http;");
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine("using Microsoft.AspNetCore.Routing;");
            sb.AppendLine();
            sb.AppendLine("namespace ModernMediator.AspNetCore.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Auto-generated Minimal API endpoint registrations for ModernMediator handlers.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class MediatorEndpoints");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Maps all <c>[Endpoint]</c>-decorated request types to Minimal API endpoints.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"app\">The endpoint route builder.</param>");
            sb.AppendLine("        /// <returns>The endpoint route builder for chaining.</returns>");
            sb.AppendLine("        public static IEndpointRouteBuilder MapMediatorEndpoints(this IEndpointRouteBuilder app)");
            sb.AppendLine("        {");

            foreach (var endpoint in endpoints)
            {
                var requestTypeName = GetFullTypeName(endpoint.RequestType);
                var mapMethod = GetMapMethodName(endpoint.Method);

                if (endpoint.Method == "GET")
                {
                    sb.AppendLine($"            app.{mapMethod}(\"{endpoint.Route}\", async ([AsParameters] {requestTypeName} request, [FromServices] global::ModernMediator.IMediator mediator) =>");
                }
                else
                {
                    sb.AppendLine($"            app.{mapMethod}(\"{endpoint.Route}\", async ([FromServices] global::ModernMediator.IMediator mediator, [FromBody] {requestTypeName} request) =>");
                }

                sb.AppendLine("            {");
                sb.AppendLine("                return await mediator.Send(request);");
                sb.AppendLine("            });");
                sb.AppendLine();
            }

            sb.AppendLine("            return app;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetMapMethodName(string method)
        {
            switch (method)
            {
                case "GET": return "MapGet";
                case "POST": return "MapPost";
                case "PUT": return "MapPut";
                case "DELETE": return "MapDelete";
                case "PATCH": return "MapPatch";
                default: return "MapPost";
            }
        }

        private static string GetFullTypeName(ITypeSymbol type)
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private class EndpointInfo
        {
            public INamedTypeSymbol RequestType { get; set; } = null!;
            public ITypeSymbol ResponseType { get; set; } = null!;
            public string Route { get; set; } = "";
            public string Method { get; set; } = "POST";
        }
    }
}
