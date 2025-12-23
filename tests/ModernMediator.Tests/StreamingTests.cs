using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModernMediator;
using Xunit;

namespace ModernMediator.Tests
{
    #region Test Stream Requests and Handlers

    // Simple stream request
    public record GetNumbersStreamRequest(int Count) : IStreamRequest<int>;

    public class GetNumbersStreamHandler : IStreamRequestHandler<GetNumbersStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(
            GetNumbersStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= request.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(10, cancellationToken); // Simulate async work
                yield return i;
            }
        }
    }

    // Stream request with complex type
    public record StreamUserDto(int Id, string Name);
    public record GetUsersStreamRequest(int Count) : IStreamRequest<StreamUserDto>;

    public class GetUsersStreamHandler : IStreamRequestHandler<GetUsersStreamRequest, StreamUserDto>
    {
        public async IAsyncEnumerable<StreamUserDto> Handle(
            GetUsersStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= request.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return new StreamUserDto(i, $"User {i}");
            }
        }
    }

    // Stream that throws exception mid-stream
    public record FailingStreamRequest(int FailAfter) : IStreamRequest<string>;

    public class FailingStreamHandler : IStreamRequestHandler<FailingStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(
            FailingStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= 10; i++)
            {
                if (i > request.FailAfter)
                {
                    throw new InvalidOperationException($"Failed after {request.FailAfter} items");
                }
                await Task.Yield();
                yield return $"Item {i}";
            }
        }
    }

    // Stream with delay for cancellation testing
    public record SlowStreamRequest(int DelayMs) : IStreamRequest<int>;

    public class SlowStreamHandler : IStreamRequestHandler<SlowStreamRequest, int>
    {
        public async IAsyncEnumerable<int> Handle(
            SlowStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 1; i <= 100; i++)
            {
                await Task.Delay(request.DelayMs, cancellationToken);
                yield return i;
            }
        }
    }

    // Empty stream
    public record EmptyStreamRequest : IStreamRequest<string>;

    public class EmptyStreamHandler : IStreamRequestHandler<EmptyStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(
            EmptyStreamRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    #endregion

    public class StreamingTests
    {
        [Fact]
        public async Task CreateStream_YieldsAllItems()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<GetNumbersStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var results = new List<int>();
            await foreach (var item in mediator.CreateStream(new GetNumbersStreamRequest(5)))
            {
                results.Add(item);
            }

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
        }

        [Fact]
        public async Task CreateStream_WithComplexType_YieldsAllItems()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<GetUsersStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var results = new List<StreamUserDto>();
            await foreach (var user in mediator.CreateStream(new GetUsersStreamRequest(3)))
            {
                results.Add(user);
            }

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal("User 1", results[0].Name);
            Assert.Equal("User 2", results[1].Name);
            Assert.Equal("User 3", results[2].Name);
        }

        [Fact]
        public async Task CreateStream_WithNoHandler_ThrowsInvalidOperationException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(); // No handlers registered

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var _ in mediator.CreateStream(new GetNumbersStreamRequest(5)))
                {
                    // Should not reach here
                }
            });
        }

        [Fact]
        public async Task CreateStream_NullRequest_ThrowsArgumentNullException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator();

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var _ in mediator.CreateStream<int>(null!))
                {
                    // Should not reach here
                }
            });
        }

        [Fact]
        public async Task CreateStream_WithCancellation_StopsEarly()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<SlowStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            var cts = new CancellationTokenSource();

            // Act
            var results = new List<int>();
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var item in mediator.CreateStream(new SlowStreamRequest(50), cts.Token))
                {
                    results.Add(item);
                    if (results.Count >= 3)
                    {
                        cts.Cancel();
                    }
                }
            });

            // Assert
            Assert.True(results.Count >= 3);
            Assert.True(results.Count < 100);
        }

        [Fact]
        public async Task CreateStream_HandlerThrows_PropagatesException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<FailingStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var results = new List<string>();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var item in mediator.CreateStream(new FailingStreamRequest(3)))
                {
                    results.Add(item);
                }
            });

            // Assert
            Assert.Equal(3, results.Count); // Got 3 items before failure
            Assert.Contains("Failed after 3 items", exception.Message);
        }

        [Fact]
        public async Task CreateStream_EmptyStream_YieldsNoItems()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<EmptyStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var results = new List<string>();
            await foreach (var item in mediator.CreateStream(new EmptyStreamRequest()))
            {
                results.Add(item);
            }

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task CreateStream_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<GetNumbersStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            mediator.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await foreach (var _ in mediator.CreateStream(new GetNumbersStreamRequest(5)))
                {
                    // Should not reach here
                }
            });
        }

        [Fact]
        public async Task CreateStream_CanBeEnumeratedMultipleTimes()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<GetNumbersStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();
            var request = new GetNumbersStreamRequest(3);

            // Act - enumerate twice
            var results1 = new List<int>();
            await foreach (var item in mediator.CreateStream(request))
            {
                results1.Add(item);
            }

            var results2 = new List<int>();
            await foreach (var item in mediator.CreateStream(request))
            {
                results2.Add(item);
            }

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, results1);
            Assert.Equal(new[] { 1, 2, 3 }, results2);
        }

        [Fact]
        public async Task CreateStream_WithToListAsync_CollectsAllItems()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<GetNumbersStreamHandler>();
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act - collect to list using LINQ
            var stream = mediator.CreateStream(new GetNumbersStreamRequest(5));
            var results = await stream.ToListAsync();

            // Assert
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, results);
        }
    }

    public class StreamHandlerRegistrationTests
    {
        [Fact]
        public void AssemblyScanning_DiscoversStreamHandlers()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterServicesFromAssemblyContaining<GetNumbersStreamHandler>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var handler = provider.GetService<IStreamRequestHandler<GetNumbersStreamRequest, int>>();

            // Assert
            Assert.NotNull(handler);
            Assert.IsType<GetNumbersStreamHandler>(handler);
        }

        [Fact]
        public void RegisterStreamHandler_Generic_RegistersHandler()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<GetNumbersStreamHandler>();
            });

            var provider = services.BuildServiceProvider();

            // Act
            var handler = provider.GetService<IStreamRequestHandler<GetNumbersStreamRequest, int>>();

            // Assert
            Assert.NotNull(handler);
        }

        [Fact]
        public async Task RegisterStreamHandler_Instance_UsesProvidedInstance()
        {
            // Arrange
            var handlerInstance = new GetNumbersStreamHandler();
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterStreamHandler<GetNumbersStreamRequest, int>(handlerInstance);
            });

            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            // Act
            var results = new List<int>();
            await foreach (var item in mediator.CreateStream(new GetNumbersStreamRequest(3)))
            {
                results.Add(item);
            }

            // Assert
            Assert.Equal(new[] { 1, 2, 3 }, results);
        }
    }

    // Extension method for collecting IAsyncEnumerable to List
    internal static class AsyncEnumerableExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
        {
            var list = new List<T>();
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                list.Add(item);
            }
            return list;
        }
    }
}