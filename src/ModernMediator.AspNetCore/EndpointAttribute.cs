namespace ModernMediator.AspNetCore
{
    /// <summary>
    /// Marks an <see cref="IRequest{TResponse}"/> type for automatic Minimal API endpoint generation.
    /// The source generator emits a <c>MapMediatorEndpoints</c> extension method that wires each
    /// decorated request type to the specified route and HTTP method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EndpointAttribute : Attribute
    {
        /// <summary>
        /// Gets the route pattern for the endpoint (e.g., <c>"/api/items"</c>).
        /// </summary>
        public string Route { get; }

        /// <summary>
        /// Gets the HTTP method for the endpoint. Defaults to <c>"POST"</c>.
        /// Valid values: <c>"GET"</c>, <c>"POST"</c>, <c>"PUT"</c>, <c>"DELETE"</c>, <c>"PATCH"</c>.
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EndpointAttribute"/> class.
        /// </summary>
        /// <param name="route">The route pattern for the endpoint.</param>
        /// <param name="method">The HTTP method. Defaults to <c>"POST"</c>.</param>
        public EndpointAttribute(string route, string method = "POST")
        {
            Route = route;
            Method = method;
        }
    }
}
