namespace ModernMediator
{
    /// <summary>
    /// Marker interface for requests that return a stream of responses.
    /// Use this for scenarios where you need to yield multiple results over time,
    /// such as pagination, real-time data feeds, or large dataset processing.
    /// </summary>
    /// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
    /// <example>
    /// <code>
    /// public record GetAllUsersStreamRequest : IStreamRequest&lt;UserDto&gt;;
    /// 
    /// // Usage:
    /// await foreach (var user in mediator.CreateStream(new GetAllUsersStreamRequest()))
    /// {
    ///     Console.WriteLine(user.Name);
    /// }
    /// </code>
    /// </example>
    public interface IStreamRequest<out TResponse> { }
}