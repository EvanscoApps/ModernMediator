using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using ModernMediator.AspNetCore.Generated;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ModernMediator.AspNetCore.Tests
{
    #region Test Request/Handler Types

    [Endpoint("/api/items", "POST")]
    public record CreateItemRequest(string Name) : IRequest<CreateItemResponse>;

    public record CreateItemResponse(int Id, string Name);

    public class CreateItemHandler : IRequestHandler<CreateItemRequest, CreateItemResponse>
    {
        public Task<CreateItemResponse> Handle(CreateItemRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreateItemResponse(1, request.Name));
        }
    }

    [Endpoint("/api/items", "GET")]
    public record GetItemsRequest : IRequest<GetItemsResponse>;

    public record GetItemsResponse(string[] Items);

    public class GetItemsHandler : IRequestHandler<GetItemsRequest, GetItemsResponse>
    {
        public Task<GetItemsResponse> Handle(GetItemsRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GetItemsResponse(new[] { "item1", "item2" }));
        }
    }

    #endregion

    public class EndpointIntegrationTests : IAsyncLifetime
    {
        private WebApplication _app = null!;
        private HttpClient _client = null!;

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddModernMediator(config =>
            {
                config.RegisterHandler<CreateItemHandler>();
                config.RegisterHandler<GetItemsHandler>();
            });

            _app = builder.Build();
            _app.MapMediatorEndpoints();

            await _app.StartAsync();
            _client = _app.GetTestClient();
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _app.DisposeAsync();
        }

        [Fact]
        public async Task PostEndpoint_DispatchesToHandler_ReturnsResponse()
        {
            var response = await _client.PostAsJsonAsync("/api/items", new CreateItemRequest("TestItem"));

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateItemResponse>();
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
            Assert.Equal("TestItem", result.Name);
        }

        [Fact]
        public async Task GetEndpoint_DispatchesToHandler_ReturnsResponse()
        {
            var response = await _client.GetAsync("/api/items");

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GetItemsResponse>();
            Assert.NotNull(result);
            Assert.Equal(2, result.Items.Length);
        }

        [Fact]
        public async Task UnregisteredRoute_Returns404()
        {
            var response = await _client.GetAsync("/api/nonexistent");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
