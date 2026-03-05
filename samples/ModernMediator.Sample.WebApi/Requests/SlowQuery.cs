using ModernMediator;
using ModernMediator.AspNetCore;

namespace ModernMediator.Sample.WebApi.Requests;

// ── GET with [Timeout] enforcement ───────────────────────────

[Timeout(2000)]
[Endpoint("/api/slow", "GET")]
public record SlowQuery : IRequest<string>;

public class SlowQueryHandler : IRequestHandler<SlowQuery, string>
{
    public async Task<string> Handle(SlowQuery request, CancellationToken cancellationToken = default)
    {
        // Simulates a slow operation that completes within the 2s timeout
        await Task.Delay(500, cancellationToken);
        return "Completed within timeout";
    }
}
