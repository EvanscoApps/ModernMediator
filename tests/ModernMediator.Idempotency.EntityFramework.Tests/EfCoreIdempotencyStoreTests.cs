using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Idempotency.EntityFramework.Tests;

public sealed class EfCoreIdempotencyStoreTests
{
    /// <summary>
    /// Test subclass that removes nvarchar(max) and IsFixedLength() which the
    /// InMemory provider does not support. The real key (Fingerprint) is preserved.
    /// </summary>
    private sealed class TestIdempotencyDbContext : IdempotencyDbContext
    {
        public TestIdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IdempotencyRecord>(entity =>
            {
                entity.HasKey(r => r.Fingerprint);
                entity.Property(r => r.Fingerprint).IsRequired().HasMaxLength(64);
                entity.Property(r => r.SerializedResponse).IsRequired();
                entity.Property(r => r.ResponseTypeName).IsRequired().HasMaxLength(512);
                entity.Property(r => r.CachedAt).IsRequired();
                entity.Property(r => r.ExpiresAt).IsRequired();
            });
        }
    }

    private static IServiceProvider BuildServiceProvider(string databaseName)
    {
        var services = new ServiceCollection();

        var options = new DbContextOptionsBuilder<IdempotencyDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        services.AddScoped<IdempotencyDbContext>(_ => new TestIdempotencyDbContext(options));

        return services.BuildServiceProvider();
    }

    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task TryGetAsync_UnknownFingerprint_ReturnsNull()
    {
        var sp = BuildServiceProvider(Guid.NewGuid().ToString());
        var store = new EfCoreIdempotencyStore(sp);

        var result = await store.TryGetAsync<string>("unknown", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAsync_AfterSet_ReturnsEntry()
    {
        var sp = BuildServiceProvider(Guid.NewGuid().ToString());
        var store = new EfCoreIdempotencyStore(sp);

        await store.SetAsync("fp-1", "hello", DefaultWindow, CancellationToken.None);

        var result = await store.TryGetAsync<string>("fp-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("hello", result.Response);
    }

    [Fact]
    public async Task TryGetAsync_AfterSet_CachedAtIsReasonable()
    {
        var before = DateTimeOffset.UtcNow;
        var sp = BuildServiceProvider(Guid.NewGuid().ToString());
        var store = new EfCoreIdempotencyStore(sp);

        await store.SetAsync("fp-time", "value", DefaultWindow, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        var result = await store.TryGetAsync<string>("fp-time", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.CachedAt >= before);
        Assert.True(result.CachedAt <= after);
    }

    [Fact]
    public async Task TryGetAsync_ExpiredEntry_ReturnsNull()
    {
        var sp = BuildServiceProvider(Guid.NewGuid().ToString());
        var store = new EfCoreIdempotencyStore(sp);

        // Store with a window of zero — expires immediately
        await store.SetAsync("fp-expired", "data", TimeSpan.Zero, CancellationToken.None);

        // Small delay to ensure expiry
        await Task.Delay(10);

        var result = await store.TryGetAsync<string>("fp-expired", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_PersistsRecordToDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var store = new EfCoreIdempotencyStore(sp);

        await store.SetAsync("fp-persist", "response-val", DefaultWindow, CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
        var record = await context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Fingerprint == "fp-persist");

        Assert.NotNull(record);
        Assert.Contains("response-val", record.SerializedResponse);
        Assert.Equal("System.String", record.ResponseTypeName);
    }

    [Fact]
    public async Task SetAsync_DuplicateFingerprint_OriginalEntryPreserved()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var store = new EfCoreIdempotencyStore(sp);

        await store.SetAsync("fp-dup", "first", DefaultWindow, CancellationToken.None);

        // Second insert with same fingerprint — may throw on InMemory provider
        // (production relies on DbUpdateException catch; InMemory throws differently).
        // The important invariant is that the original entry is preserved.
        try
        {
            await store.SetAsync("fp-dup", "second", DefaultWindow, CancellationToken.None);
        }
        catch
        {
            // Expected on InMemory provider
        }

        var result = await store.TryGetAsync<string>("fp-dup", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("first", result.Response);
    }

    [Fact]
    public async Task TryGetAsync_IntResponse_DeserializesCorrectly()
    {
        var sp = BuildServiceProvider(Guid.NewGuid().ToString());
        var store = new EfCoreIdempotencyStore(sp);

        await store.SetAsync("fp-int", 42, DefaultWindow, CancellationToken.None);

        var result = await store.TryGetAsync<int>("fp-int", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(42, result.Response);
    }

    [Fact]
    public async Task TryGetAsync_ComplexResponse_DeserializesCorrectly()
    {
        var sp = BuildServiceProvider(Guid.NewGuid().ToString());
        var store = new EfCoreIdempotencyStore(sp);

        var response = new TestDto { Id = 7, Name = "test" };
        await store.SetAsync("fp-dto", response, DefaultWindow, CancellationToken.None);

        var result = await store.TryGetAsync<TestDto>("fp-dto", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(7, result.Response.Id);
        Assert.Equal("test", result.Response.Name);
    }

    [Fact]
    public async Task SetAsync_MultipleDistinctFingerprints_AllPersisted()
    {
        var dbName = Guid.NewGuid().ToString();
        var sp = BuildServiceProvider(dbName);
        var store = new EfCoreIdempotencyStore(sp);

        await store.SetAsync("fp-a", "a", DefaultWindow, CancellationToken.None);
        await store.SetAsync("fp-b", "b", DefaultWindow, CancellationToken.None);
        await store.SetAsync("fp-c", "c", DefaultWindow, CancellationToken.None);

        using var scope = sp.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdempotencyDbContext>();
        var count = await context.IdempotencyRecords.CountAsync();

        Assert.Equal(3, count);
    }
}

public sealed class TestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
