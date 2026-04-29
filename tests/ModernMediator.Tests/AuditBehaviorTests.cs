using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ModernMediator.Tests;

public sealed class AuditBehaviorTests
{
    // --- Test doubles ---

    private sealed class AuditedRequest : IRequest<string>
    {
        public string Value { get; init; } = string.Empty;
    }

    [NoAudit]
    private sealed class NonAuditedRequest : IRequest<string> { }

    private sealed class AuditedUnitRequest : IRequest<Unit> { }

    private sealed class FixedUserAccessor : ICurrentUserAccessor
    {
        public string? UserId { get; init; }
        public string? UserName { get; init; }
    }

    // --- Helpers ---

    private static (AuditBehavior<TRequest, TResponse> Behavior, CapturingAuditWriter Writer,
        Channel<AuditRecord> Channel)
        BuildChannelBehavior<TRequest, TResponse>(
            ICurrentUserAccessor? userAccessor = null)
        where TRequest : IRequest<TResponse>
    {
        var writer = new CapturingAuditWriter();
        var options = new AuditOptions { DispatchMode = AuditDispatchMode.Channel };
        var channel = Channel.CreateBounded<AuditRecord>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        var behavior = new AuditBehavior<TRequest, TResponse>(
            writer,
            options,
            NullLogger<AuditBehavior<TRequest, TResponse>>.Instance,
            userAccessor,
            channel);
        return (behavior, writer, channel);
    }

    private static AuditBehavior<TRequest, TResponse> BuildFireAndForgetBehavior<TRequest, TResponse>(
        CapturingAuditWriter writer,
        ICurrentUserAccessor? userAccessor = null)
        where TRequest : IRequest<TResponse>
    {
        var options = new AuditOptions { DispatchMode = AuditDispatchMode.FireAndForget };
        return new AuditBehavior<TRequest, TResponse>(
            writer,
            options,
            NullLogger<AuditBehavior<TRequest, TResponse>>.Instance,
            userAccessor,
            channel: null);
    }

    private static async Task<AuditRecord> DrainOneAsync(Channel<AuditRecord> channel)
    {
        return await channel.Reader.ReadAsync(CancellationToken.None);
    }

    // --- Tests ---

    [Fact]
    public async Task Handle_NoAuditAttribute_DoesNotWriteRecord()
    {
        var (behavior, _, channel) = BuildChannelBehavior<NonAuditedRequest, string>();

        await behavior.Handle(
            new NonAuditedRequest(),
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        Assert.Equal(0, channel.Reader.Count);
    }

    [Fact]
    public async Task Handle_AuditedRequest_SuccessfulHandler_WritesSuccessRecord()
    {
        var (behavior, _, channel) = BuildChannelBehavior<AuditedRequest, string>();

        await behavior.Handle(
            new AuditedRequest { Value = "test" },
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        var record = await DrainOneAsync(channel);

        Assert.True(record.Succeeded);
        Assert.Null(record.FailureReason);
        Assert.Contains("AuditedRequest", record.RequestTypeName);
        Assert.Contains("test", record.SerializedPayload);
        Assert.True(record.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Handle_AuditedRequest_FailingHandler_WritesFailureRecordAndRethrows()
    {
        var (behavior, _, channel) = BuildChannelBehavior<AuditedRequest, string>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new AuditedRequest { Value = "test" },
                (_, _) => Task.FromException<string>(new InvalidOperationException("boom")),
                CancellationToken.None));

        var record = await DrainOneAsync(channel);

        Assert.False(record.Succeeded);
        Assert.Equal("boom", record.FailureReason);
    }

    [Fact]
    public async Task Handle_AuditedRequest_CapturesUserIdentity()
    {
        var userAccessor = new FixedUserAccessor { UserId = "user-123", UserName = "Rob" };
        var (behavior, _, channel) = BuildChannelBehavior<AuditedRequest, string>(userAccessor);

        await behavior.Handle(
            new AuditedRequest(),
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        var record = await DrainOneAsync(channel);

        Assert.Equal("user-123", record.UserId);
        Assert.Equal("Rob", record.UserName);
    }

    [Fact]
    public async Task Handle_AuditedRequest_NoUserAccessor_NullIdentityFields()
    {
        var (behavior, _, channel) = BuildChannelBehavior<AuditedRequest, string>(
            userAccessor: null);

        await behavior.Handle(
            new AuditedRequest(),
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        var record = await DrainOneAsync(channel);

        Assert.Null(record.UserId);
        Assert.Null(record.UserName);
    }

    [Fact]
    public async Task Handle_AuditedRequest_RecordTimestampIsUtc()
    {
        var before = DateTimeOffset.UtcNow;
        var (behavior, _, channel) = BuildChannelBehavior<AuditedRequest, string>();

        await behavior.Handle(
            new AuditedRequest(),
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        var record = await DrainOneAsync(channel);

        Assert.True(record.Timestamp >= before);
        Assert.True(record.Timestamp <= after);
    }

    [Fact]
    public async Task Handle_AuditedUnitRequest_WritesRecord()
    {
        var (behavior, _, channel) = BuildChannelBehavior<AuditedUnitRequest, Unit>();

        await behavior.Handle(
            new AuditedUnitRequest(),
            (_, _) => Task.FromResult(Unit.Value),
            CancellationToken.None);

        var record = await DrainOneAsync(channel);

        Assert.True(record.Succeeded);
    }

    [Fact]
    public async Task Handle_FireAndForget_SuccessfulHandler_WritesRecord()
    {
        var writer = new CapturingAuditWriter();
        var behavior = BuildFireAndForgetBehavior<AuditedRequest, string>(writer);

        await behavior.Handle(
            new AuditedRequest { Value = "test" },
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        await writer.WaitForWriteAsync();

        Assert.Single(writer.Records);
        Assert.True(writer.Records[0].Succeeded);
    }

    [Fact]
    public async Task Handle_AuditedRequest_ActiveActivity_TraceIdPopulated()
    {
        var (behavior, _, channel) = BuildChannelBehavior<AuditedRequest, string>();

        using var activitySource = new ActivitySource("TestSource");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("TestOperation");
        Assert.NotNull(activity);

        await behavior.Handle(
            new AuditedRequest { Value = "traced" },
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        var record = await DrainOneAsync(channel);

        Assert.NotNull(record.TraceId);
        Assert.Equal(activity.TraceId.ToString(), record.TraceId);
    }

    [Fact]
    public async Task Handle_AuditedRequest_NoActivity_TraceIdIsNull()
    {
        var (behavior, _, channel) = BuildChannelBehavior<AuditedRequest, string>();

        // Ensure no ambient Activity
        Activity.Current = null;

        await behavior.Handle(
            new AuditedRequest { Value = "untraced" },
            (_, _) => Task.FromResult("ok"),
            CancellationToken.None);

        var record = await DrainOneAsync(channel);

        Assert.Null(record.TraceId);
    }
}
