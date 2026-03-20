using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator;

/// <summary>
/// Pipeline behavior that enforces idempotency for requests decorated with
/// <see cref="IdempotentAttribute"/>. Computes a SHA-256 fingerprint of the
/// request and returns a cached result if the same fingerprint has been seen
/// within the configured window, without re-executing the handler.
/// </summary>
/// <remarks>
/// Fingerprint computation requires the request to be serializable by
/// <see cref="System.Text.Json.JsonSerializer"/>. The backing store is
/// best-effort when cache-backed — see <see cref="IIdempotencyStore"/> and
/// ADR-004 for details.
/// </remarks>
#pragma warning disable MM006
public sealed class IdempotencyBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
#pragma warning restore MM006
    private readonly IIdempotencyStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="IdempotencyBehavior{TRequest, TResponse}"/>.
    /// </summary>
    public IdempotencyBehavior(IIdempotencyStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var attribute = GetAttribute();

        if (attribute is null)
            return await next(request, cancellationToken).ConfigureAwait(false);

        var fingerprint = ComputeFingerprint(request);
        var window = TimeSpan.FromSeconds(attribute.WindowSeconds);

        var existing = await _store
            .TryGetAsync<TResponse>(fingerprint, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
            return existing.Response;

        var response = await next(request, cancellationToken).ConfigureAwait(false);

        await _store
            .SetAsync(fingerprint, response, window, cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    private static IdempotentAttribute? GetAttribute() =>
        (IdempotentAttribute?)Attribute.GetCustomAttribute(
            typeof(TRequest), typeof(IdempotentAttribute));

    private static string ComputeFingerprint(TRequest request)
    {
        var payload = $"{typeof(TRequest).FullName}:{JsonSerializer.Serialize(request)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }
}
