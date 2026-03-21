using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace ModernMediator.Audit.Serilog.Tests;

public sealed class SerilogAuditWriterTests
{
    private static (SerilogAuditWriter Writer, List<LogEvent> Events) CreateWriter()
    {
        var events = new List<LogEvent>();
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new ListSink(events))
            .CreateLogger();

        var writer = new SerilogAuditWriter(logger);
        return (writer, events);
    }

    private static AuditRecord SuccessRecord(
        string requestTypeName = "TestApp.MyCommand",
        string? userId = "user-1",
        string? userName = "Alice",
        string? correlationId = "corr-42") =>
        new()
        {
            RequestTypeName = requestTypeName,
            SerializedPayload = "{}",
            UserId = userId,
            UserName = userName,
            Timestamp = DateTimeOffset.UtcNow,
            Succeeded = true,
            Duration = TimeSpan.FromMilliseconds(123),
            CorrelationId = correlationId
        };

    private static AuditRecord FailureRecord(
        string failureReason = "Something went wrong") =>
        new()
        {
            RequestTypeName = "TestApp.FailingCommand",
            SerializedPayload = "{}",
            UserId = null,
            UserName = null,
            Timestamp = DateTimeOffset.UtcNow,
            Succeeded = false,
            FailureReason = failureReason,
            Duration = TimeSpan.FromMilliseconds(50)
        };

    [Fact]
    public async Task WriteAsync_SuccessfulRecord_LogsAtInformationLevel()
    {
        var (writer, events) = CreateWriter();

        await writer.WriteAsync(SuccessRecord(), CancellationToken.None);

        Assert.Single(events);
        Assert.Equal(LogEventLevel.Information, events[0].Level);
    }

    [Fact]
    public async Task WriteAsync_FailedRecord_LogsAtWarningLevel()
    {
        var (writer, events) = CreateWriter();

        await writer.WriteAsync(FailureRecord(), CancellationToken.None);

        Assert.Single(events);
        Assert.Equal(LogEventLevel.Warning, events[0].Level);
    }

    [Fact]
    public async Task WriteAsync_IncludesRequestTypeName()
    {
        var (writer, events) = CreateWriter();

        await writer.WriteAsync(SuccessRecord(requestTypeName: "TestApp.GetUserQuery"), CancellationToken.None);

        var rendered = events[0].RenderMessage();
        Assert.Contains("TestApp.GetUserQuery", rendered);
    }

    [Fact]
    public async Task WriteAsync_IncludesUserId()
    {
        var (writer, events) = CreateWriter();

        await writer.WriteAsync(SuccessRecord(userId: "uid-99"), CancellationToken.None);

        var rendered = events[0].RenderMessage();
        Assert.Contains("uid-99", rendered);
    }

    [Fact]
    public async Task WriteAsync_NullUserId_LogsAnonymous()
    {
        var (writer, events) = CreateWriter();

        await writer.WriteAsync(SuccessRecord(userId: null), CancellationToken.None);

        var rendered = events[0].RenderMessage();
        Assert.Contains("anonymous", rendered);
    }

    [Fact]
    public async Task WriteAsync_NullCorrelationId_LogsNone()
    {
        var (writer, events) = CreateWriter();

        await writer.WriteAsync(SuccessRecord(correlationId: null), CancellationToken.None);

        var rendered = events[0].RenderMessage();
        Assert.Contains("none", rendered);
    }

    [Fact]
    public async Task WriteAsync_FailureRecord_IncludesFailureReason()
    {
        var (writer, events) = CreateWriter();

        await writer.WriteAsync(FailureRecord("db timeout"), CancellationToken.None);

        var rendered = events[0].RenderMessage();
        Assert.Contains("db timeout", rendered);
    }

    [Fact]
    public void WriteAsync_ReturnsCompletedValueTask()
    {
        var (writer, _) = CreateWriter();

        var task = writer.WriteAsync(SuccessRecord(), CancellationToken.None);

        Assert.True(task.IsCompleted);
    }

    private sealed class ListSink : ILogEventSink
    {
        private readonly List<LogEvent> _events;

        public ListSink(List<LogEvent> events)
        {
            _events = events;
        }

        public void Emit(LogEvent logEvent)
        {
            _events.Add(logEvent);
        }
    }
}
