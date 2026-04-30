using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

namespace ModernMediator.Tests;

public sealed class DistributedIdempotencyStoreTests
{
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(10);

    [Fact]
    public async Task TryGetAsync_UnknownFingerprint_ReturnsNull()
    {
        var cache = new FakeDistributedCache();
        var store = new DistributedIdempotencyStore(cache);

        var result = await store.TryGetAsync<string>("unknown", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryGetAsync_AfterSet_ReturnsDeserializedEntry()
    {
        var cache = new FakeDistributedCache();
        var store = new DistributedIdempotencyStore(cache);

        await store.SetAsync("fp-roundtrip", "hello", DefaultWindow, CancellationToken.None);

        var result = await store.TryGetAsync<string>("fp-roundtrip", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("hello", result!.Response);
    }

    [Fact]
    public async Task SetAsync_PersistsSerializedEntryUnderFingerprintKey()
    {
        var cache = new FakeDistributedCache();
        var store = new DistributedIdempotencyStore(cache);

        var before = DateTimeOffset.UtcNow;
        await store.SetAsync("fp-persist", "response-val", DefaultWindow, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.True(cache.Storage.ContainsKey("fp-persist"));

        var entry = JsonSerializer.Deserialize<IdempotencyEntry<string>>(cache.Storage["fp-persist"]);
        Assert.NotNull(entry);
        Assert.Equal("response-val", entry!.Response);
        Assert.True(entry.CachedAt >= before);
        Assert.True(entry.CachedAt <= after);
    }

    [Fact]
    public async Task SetAsync_PassesWindowAsAbsoluteExpirationRelativeToNow()
    {
        var cache = new FakeDistributedCache();
        var store = new DistributedIdempotencyStore(cache);

        var window = TimeSpan.FromSeconds(42);
        await store.SetAsync("fp-ttl", "v", window, CancellationToken.None);

        Assert.NotNull(cache.LastSetOptions);
        Assert.Equal(window, cache.LastSetOptions!.AbsoluteExpirationRelativeToNow);
        Assert.Null(cache.LastSetOptions.AbsoluteExpiration);
        Assert.Null(cache.LastSetOptions.SlidingExpiration);
    }

    [Fact]
    public async Task TryGetAsync_CacheThrows_PropagatesException()
    {
        var cache = new FakeDistributedCache
        {
            FailWith = new InvalidOperationException("redis down")
        };
        var store = new DistributedIdempotencyStore(cache);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.TryGetAsync<string>("fp", CancellationToken.None));

        Assert.Equal("redis down", ex.Message);
    }

    [Fact]
    public async Task SetAsync_CacheThrows_PropagatesException()
    {
        var cache = new FakeDistributedCache
        {
            FailWith = new InvalidOperationException("redis down")
        };
        var store = new DistributedIdempotencyStore(cache);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await store.SetAsync("fp", "v", DefaultWindow, CancellationToken.None));

        Assert.Equal("redis down", ex.Message);
    }
}

internal sealed class FakeDistributedCache : IDistributedCache
{
    public Dictionary<string, byte[]> Storage { get; } = new();
    public DistributedCacheEntryOptions? LastSetOptions { get; private set; }
    public Exception? FailWith { get; set; }

    public byte[]? Get(string key) => throw new NotImplementedException("synchronous path not exercised");

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        if (FailWith is not null) throw FailWith;
        return Task.FromResult(Storage.TryGetValue(key, out var value) ? value : null);
    }

    public void Refresh(string key) => throw new NotImplementedException();

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => throw new NotImplementedException();

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Storage.Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        throw new NotImplementedException();

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        if (FailWith is not null) throw FailWith;
        Storage[key] = value;
        LastSetOptions = options;
        return Task.CompletedTask;
    }
}
