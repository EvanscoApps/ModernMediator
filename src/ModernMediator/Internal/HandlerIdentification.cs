using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ModernMediator.Internal
{
    /// <summary>
    /// Helpers for identifying the source handler of a notification dispatch error.
    /// Used to populate <see cref="HandlerErrorEventArgs.HandlerType"/> and
    /// <see cref="HandlerErrorEventArgs.HandlerInstance"/> consistently across
    /// dispatch paths per ADR-005.
    /// </summary>
    internal static class HandlerIdentification
    {
        /// <summary>
        /// Resolves the handler type for a Subscribe-callback delegate. Returns
        /// the delegate's <see cref="MethodInfo.DeclaringType"/>, walking past
        /// compiler-generated closure types to the enclosing user type.
        /// </summary>
        /// <param name="handler">The subscribed delegate. Must not be null.</param>
        /// <returns>
        /// The resolved handler type. For named-method subscriptions, this is the
        /// declaring type directly. For lambda subscriptions, this is the enclosing
        /// user type after walking past any compiler-generated closure types.
        /// Returns null only if the delegate's declaring type cannot be determined,
        /// which should not occur in practice.
        /// </returns>
        public static Type? ResolveHandlerType(Delegate handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var declaringType = handler.Method.DeclaringType;
            return UnwrapCompilerGeneratedType(declaringType);
        }

        /// <summary>
        /// Resolves the handler type for a Subscribe-callback subscription that holds
        /// only a MethodInfo (used by weak-reference entries that retain the method
        /// rather than the full delegate). Walks past compiler-generated closure types
        /// to the enclosing user type, identical to the Delegate-based overload.
        /// </summary>
        /// <param name="method">The subscribed method. Must not be null.</param>
        /// <returns>
        /// The resolved handler type. Returns null only if the method's declaring type
        /// cannot be determined, which should not occur in practice.
        /// </returns>
        public static Type? ResolveHandlerType(System.Reflection.MethodInfo method)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            return UnwrapCompilerGeneratedType(method.DeclaringType);
        }

        /// <summary>
        /// Resolves the handler instance for a Subscribe-callback delegate.
        /// Returns <see cref="Delegate.Target"/>, which is null for static delegates.
        /// For lambda subscriptions over compiler-generated closure types, this is
        /// the closure instance itself; consumers who want the enclosing user instance
        /// should not rely on this value matching their own object reference.
        /// </summary>
        /// <param name="handler">The subscribed delegate. Must not be null.</param>
        /// <returns>The delegate target, or null for static delegates.</returns>
        public static object? ResolveHandlerInstance(Delegate handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return handler.Target;
        }

        /// <summary>
        /// Walks a type chain past compiler-generated closure types to the
        /// enclosing user type. If the input type is not compiler-generated,
        /// returns it directly. If the input type is compiler-generated, returns
        /// the first non-compiler-generated type found by walking up the
        /// DeclaringType chain. Returns null only if the entire chain is
        /// compiler-generated, which should not occur in practice.
        /// </summary>
        private static Type? UnwrapCompilerGeneratedType(Type? type)
        {
            while (type != null && IsCompilerGenerated(type))
            {
                type = type.DeclaringType;
            }

            return type;
        }

        private static bool IsCompilerGenerated(Type type)
        {
            return type.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
        }
    }
}
