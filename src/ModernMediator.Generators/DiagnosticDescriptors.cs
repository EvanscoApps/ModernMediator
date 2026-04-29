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

        public static readonly DiagnosticDescriptor HandlerReturnTypeMismatch = new(
            id: "MM004",
            title: "Handler return type mismatch",
            messageFormat: "Handler '{0}' returns '{1}' but the request type '{2}' expects '{3}'",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor NotificationHandlerReturnsValue = new(
            id: "MM005",
            title: "Notification handler has return value",
            messageFormat: "'{0}' implements INotificationHandler but its Handle method returns a value — this is likely unintentional",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OpenGenericBehavior = new(
            id: "MM006",
            title: "Open generic behavior may need explicit registration",
            messageFormat: "'{0}' is an open generic IPipelineBehavior<,> — it will not be discovered by assembly scanning. Register it with AddOpenBehavior() in your configuration",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor HandlerNoMatchingRequestType = new(
            id: "MM007",
            title: "Handler has no matching request type",
            messageFormat: "'{0}' implements IRequestHandler<{1}, {2}> but no type implementing IRequest<{2}> named '{1}' was found in the assembly",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor WeakLambdaSubscription = new(
            id: "MM008",
            title: "Lambda used with weak reference subscription",
            messageFormat: "Subscribing a lambda or closure with a weak reference may cause the handler to be garbage collected immediately. Use weak: false or pass a method reference instead",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DispatcherOverloadMismatch = new(
            id: "MM009",
            title: "Dispatcher overload mismatch",
            messageFormat: "{0} is registered as {1}; call {2} instead of {3}",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Send was called for a request whose handler is registered as IValueTaskRequestHandler (or vice versa). The mismatch will throw at runtime; calling the matching dispatch method resolves it.");

        public static readonly DiagnosticDescriptor GeneratorSuccess = new(
            id: "MM100",
            title: "ModernMediator registration generated",
            messageFormat: "Generated registration for {0} handlers, {1} stream handlers, {2} behaviors, {3} pre-processors, {4} post-processors",
            category: "ModernMediator",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);

        // --- Runtime diagnostic codes (MM2xx) ---
        // Per ADR-008, MM2xx codes identify runtime conditions surfaced through exception
        // messages rather than Roslyn diagnostics. They are defined here as constants so
        // consumers grepping the codebase for an MM-prefix code find a single source of
        // truth alongside the analyzer-style descriptors.

        /// <summary>
        /// MM200: A request handler is registered under one dispatch interface but the
        /// dispatcher was called via the other (e.g. handler is IValueTaskRequestHandler
        /// and the caller invoked Send instead of SendAsync).
        /// Surfaced as InvalidOperationException with a guiding message; not a Roslyn diagnostic.
        /// </summary>
        public const string DispatcherOverloadMismatchCode = "MM200";
    }
}