using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Audit.EntityFramework.Tests;

public sealed class EfCoreAuditWriterTests
{
    /// <summary>
    /// Test subclass that replaces HasNoKey() and nvarchar(max) with
    /// InMemory-compatible configuration (auto-generated shadow key).
    /// </summary>
    private sealed class TestAuditDbContext : AuditDbContext
    {
        public TestAuditDbContext(DbContextOptions<AuditDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuditRecord>(entity =>
            {
                entity.Property<int>("Id").ValueGeneratedOnAdd();
                entity.HasKey("Id");
                entity.Property(r => r.RequestTypeName).IsRequired();
                entity.Property(r => r.SerializedPayload).IsRequired();
                entity.Property(r => r.Timestamp).IsRequired();
                entity.Property(r => r.Succeeded).IsRequired();
                entity.Property(r => r.Duration).IsRequired()
                    .HasConversion(
                        v => v.Ticks,
                        v => TimeSpan.FromTicks(v));
            });
        }
    }

    private static IServiceProvider BuildServiceProvider(string databaseName)
    {
        var services = new ServiceCollection();

        // Build DbContextOptions<AuditDbContext> manually with InMemory
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        // Register AuditDbContext as scoped, resolved via TestAuditDbContext
        services.AddScoped<AuditDbContext>(_ => new TestAuditDbContext(options));

        return services.BuildServiceProvider();
    }

    private static AuditRecord MakeSuccessRecord(
        string requestTypeName = "TestApp.MyCommand",
        string? userId = "user-1",
        string? userName = "Alice",
        string? correlationId = "corr-1")
    {
        return new AuditRecord
        {
            RequestTypeName = requestTypeName,
            SerializedPayload = "{\"Key\":\"Value\"}",
            UserId = userId,
            UserName = userName,
            Timestamp = DateTimeOffset.UtcNow,
            Succeeded = true,
            Duration = TimeSpan.FromMilliseconds(42),
            CorrelationId = correlationId
        };
    }

    private static AuditRecord MakeFailureRecord(
        string failureReason = "Something broke")
    {
        return new AuditRecord
        {
            RequestTypeName = "TestApp.FailingCommand",
            SerializedPayload = "{}",
            UserId = null,
            UserName = null,
            Timestamp = DateTimeOffset.UtcNow,
            Succeeded = false,
            FailureReason = failureReason,
            Duration = TimeSpan.FromMilliseconds(10)
        };
    }

    [Fact]
    public async Task WriteAsync_SuccessRecord_PersistsToDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var writer = new EfCoreAuditWriter(sp);

        await writer.WriteAsync(MakeSuccessRecord(), CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var records = await context.AuditRecords.ToListAsync();

        Assert.Single(records);
        Assert.True(records[0].Succeeded);
    }

    [Fact]
    public async Task WriteAsync_FailureRecord_PersistsFailureData()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var writer = new EfCoreAuditWriter(sp);

        await writer.WriteAsync(MakeFailureRecord("db timeout"), CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var records = await context.AuditRecords.ToListAsync();

        Assert.Single(records);
        Assert.False(records[0].Succeeded);
        Assert.Equal("db timeout", records[0].FailureReason);
    }

    [Fact]
    public async Task WriteAsync_PersistedFields_MatchSourceRecord()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var writer = new EfCoreAuditWriter(sp);

        var source = MakeSuccessRecord(
            requestTypeName: "TestApp.GetUserQuery",
            userId: "uid-77",
            userName: "Bob",
            correlationId: "corr-99");

        await writer.WriteAsync(source, CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var persisted = (await context.AuditRecords.ToListAsync()).Single();

        Assert.Equal("TestApp.GetUserQuery", persisted.RequestTypeName);
        Assert.Equal("{\"Key\":\"Value\"}", persisted.SerializedPayload);
        Assert.Equal("uid-77", persisted.UserId);
        Assert.Equal("Bob", persisted.UserName);
        Assert.True(persisted.Succeeded);
        Assert.Null(persisted.FailureReason);
        Assert.Equal("corr-99", persisted.CorrelationId);
        Assert.True(persisted.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task WriteAsync_MultipleWrites_ProduceSeparateRows()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var writer = new EfCoreAuditWriter(sp);

        await writer.WriteAsync(MakeSuccessRecord(requestTypeName: "Cmd1"), CancellationToken.None);
        await writer.WriteAsync(MakeSuccessRecord(requestTypeName: "Cmd2"), CancellationToken.None);
        await writer.WriteAsync(MakeFailureRecord("err"), CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var records = await context.AuditRecords.ToListAsync();

        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task WriteAsync_NullOptionalFields_PersistsSuccessfully()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var writer = new EfCoreAuditWriter(sp);

        var record = MakeSuccessRecord(userId: null, userName: null, correlationId: null);

        await writer.WriteAsync(record, CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var persisted = (await context.AuditRecords.ToListAsync()).Single();

        Assert.Null(persisted.UserId);
        Assert.Null(persisted.UserName);
        Assert.Null(persisted.CorrelationId);
    }

    [Fact]
    public async Task WriteAsync_EachCallCreatesNewScope()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var writer = new EfCoreAuditWriter(sp);

        await writer.WriteAsync(MakeSuccessRecord(requestTypeName: "First"), CancellationToken.None);
        await writer.WriteAsync(MakeSuccessRecord(requestTypeName: "Second"), CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var names = (await context.AuditRecords.ToListAsync())
            .Select(r => r.RequestTypeName)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "First", "Second" }, names);
    }
}
