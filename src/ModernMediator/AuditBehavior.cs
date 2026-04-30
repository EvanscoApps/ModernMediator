using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ModernMediator;

/// <summary>
/// Pipeline behavior that automatically records an <see cref="AuditRecord"/> for
/// every request not decorated with <see cref="NoAuditAttribute"/>. Captures the
/// request type, serialized payload, user identity, timestamp, outcome, and duration.
/// </summary>
/// <remarks>
/// Audit writes are dispatched via a background channel drainer by default, keeping
/// write latency off the request pipeline. See ADR-001 for rationale.
/// A failure in the audit write path never propagates to the caller.
/// <para>
/// Recommended pipeline position: outermost. AuditBehavior captures the full
/// duration of the request including all retries, timeouts, validation failures,
/// and circuit-breaker fast-fails. Registering it inside other behaviors would
/// scope the audit record to a sub-pipeline and miss outer-layer failures.
/// </para>
/// </remarks>
#pragma warning disable MM006
public sealed class AuditBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
#pragma warning restore MM006

    private readonly IAuditWriter _writer;
    private readonly ICurrentUserAccessor? _userAccessor;
    private readonly AuditOptions _options;
    private readonly Channel<AuditRecord>? _channel;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditBehavior{TRequest, TResponse}"/>.
    /// </summary>
    public AuditBehavior(
        IAuditWriter writer,
        AuditOptions options,
        ILogger<AuditBehavior<TRequest, TResponse>> logger,
        ICurrentUserAccessor? userAccessor = null,
        Channel<AuditRecord>? channel = null)
    {
        _writer = writer;
        _options = options;
        _logger = logger;
        _userAccessor = userAccessor;
        _channel = channel;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (HasNoAuditAttribute())
            return await next(request, cancellationToken).ConfigureAwait(false);

        var timestamp = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        bool succeeded = false;
        string? failureReason = null;
        TResponse response;

        try
        {
            response = await next(request, cancellationToken).ConfigureAwait(false);
            succeeded = true;
        }
        catch (Exception ex)
        {
            failureReason = ex.Message;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var record = BuildRecord(request, timestamp, stopwatch.Elapsed, succeeded, failureReason);
            await DispatchAsync(record).ConfigureAwait(false);
        }

        return response;
    }

    private async ValueTask DispatchAsync(AuditRecord record)
    {
        if (_options.DispatchMode == AuditDispatchMode.FireAndForget)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _writer.WriteAsync(record, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Audit write failed for request type {RequestType}.",
                        record.RequestTypeName);
                }
            });
            return;
        }

        if (_channel is not null)
        {
            try
            {
                await _channel.Writer.WriteAsync(record, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit channel write failed for request type {RequestType}.",
                    record.RequestTypeName);
            }
        }
    }

    private AuditRecord BuildRecord(
        TRequest request,
        DateTimeOffset timestamp,
        TimeSpan duration,
        bool succeeded,
        string? failureReason)
    {
        string serializedPayload;

        try
        {
            serializedPayload = JsonSerializer.Serialize(request);
        }
        catch
        {
            serializedPayload = "{}";
        }

        return new AuditRecord
        {
            RequestTypeName = typeof(TRequest).FullName ?? typeof(TRequest).Name,
            SerializedPayload = serializedPayload,
            UserId = _userAccessor?.UserId,
            UserName = _userAccessor?.UserName,
            Timestamp = timestamp,
            Succeeded = succeeded,
            FailureReason = failureReason,
            Duration = duration,
            CorrelationId = null,
            TraceId = Activity.Current?.TraceId.ToString()
        };
    }

    private static bool HasNoAuditAttribute() =>
        Attribute.IsDefined(typeof(TRequest), typeof(NoAuditAttribute));
}
