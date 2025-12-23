using Microsoft.CodeAnalysis;

namespace ModernMediator.Generators
{
    /// <summary>
    /// Diagnostic descriptors for ModernMediator source generator.
    /// </summary>
    public static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor DuplicateHandler = new(
            id: "MM001",
            title: "Duplicate handler registration",
            messageFormat: "Multiple handlers found for request type '{0}': {1}",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NoHandlerFound = new(
            id: "MM002",
            title: "No handler found",
            messageFormat: "No handler found for request type '{0}'",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor HandlerMustBeNonAbstract = new(
            id: "MM003",
            title: "Handler must be non-abstract",
            messageFormat: "Handler '{0}' must be a non-abstract class",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GeneratorSuccess = new(
            id: "MM100",
            title: "ModernMediator registration generated",
            messageFormat: "Generated registration for {0} handlers, {1} stream handlers, {2} behaviors, {3} pre-processors, {4} post-processors",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);
    }
}