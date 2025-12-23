namespace ModernMediator
{
    /// <summary>
    /// Marker interface for requests that return a response.
    /// </summary>
    /// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
    public interface IRequest<TResponse> { }

    /// <summary>
    /// Marker interface for requests that return no meaningful value.
    /// Equivalent to IRequest&lt;Unit&gt;.
    /// </summary>
    public interface IRequest : IRequest<Unit> { }
}