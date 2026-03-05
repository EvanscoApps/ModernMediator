using ModernMediator;
using ModernMediator.AspNetCore;

namespace ModernMediator.Starter.Requests;

[Endpoint("/api/ping", "GET")]
public record Ping : IRequest<PingResponse>;

public record PingResponse(string Message, DateTime Timestamp);

public class PingHandler : IRequestHandler<Ping, PingResponse>
{
    public Task<PingResponse> Handle(Ping request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PingResponse("pong", DateTime.UtcNow));
    }
}
