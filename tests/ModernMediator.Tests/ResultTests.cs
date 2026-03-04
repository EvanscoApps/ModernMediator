using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests
{
    public class ResultTests
    {
        [Fact]
        public void Success_IsSuccessTrue_IsFailureFalse_ValueAccessible_ErrorIsNone()
        {
            var result = Result<int>.Success(42);

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal(42, result.Value);
            Assert.Equal(ResultError.None, result.Error);
        }

        [Fact]
        public void Failure_IsFailureTrue_ValueThrowsInvalidOperationException()
        {
            var error = new ResultError("ERR01", "Something went wrong");
            var result = Result<int>.Failure(error);

            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Assert.Equal("ERR01", result.Error.Code);
            Assert.Equal("Something went wrong", result.Error.Message);

            var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
            Assert.Equal("Cannot access Value of a failed Result.", ex.Message);
        }

        [Fact]
        public void Failure_ConvenienceOverload_PopulatesCodeAndMessage()
        {
            var result = Result<string>.Failure("NOT_FOUND", "Item not found");

            Assert.True(result.IsFailure);
            Assert.Equal("NOT_FOUND", result.Error.Code);
            Assert.Equal("Item not found", result.Error.Message);
        }

        [Fact]
        public void ImplicitConversion_FromT_ProducesSuccess()
        {
            Result<string> result = "hello";

            Assert.True(result.IsSuccess);
            Assert.Equal("hello", result.Value);
        }

        [Fact]
        public void ImplicitConversion_FromResultError_ProducesFailure()
        {
            Result<string> result = new ResultError("E1", "fail");

            Assert.True(result.IsFailure);
            Assert.Equal("E1", result.Error.Code);
        }

        [Fact]
        public void Map_OnSuccess_TransformsValue()
        {
            var result = Result<int>.Success(5);

            var mapped = result.Map(v => v * 2);

            Assert.True(mapped.IsSuccess);
            Assert.Equal(10, mapped.Value);
        }

        [Fact]
        public void Map_OnFailure_PropagatesErrorWithoutCallingMapper()
        {
            var result = Result<int>.Failure("ERR", "fail");
            bool mapperCalled = false;

            var mapped = result.Map(v =>
            {
                mapperCalled = true;
                return v.ToString();
            });

            Assert.True(mapped.IsFailure);
            Assert.False(mapperCalled);
            Assert.Equal("ERR", mapped.Error.Code);
            Assert.Equal("fail", mapped.Error.Message);
        }

        [Fact]
        public void GetValueOrDefault_ReturnsValue_OnSuccess()
        {
            var result = Result<int>.Success(42);

            Assert.Equal(42, result.GetValueOrDefault(0));
        }

        [Fact]
        public void GetValueOrDefault_ReturnsDefault_OnFailure()
        {
            var result = Result<int>.Failure("ERR", "fail");

            Assert.Equal(-1, result.GetValueOrDefault(-1));
        }

        [Fact]
        public void GetValueOrDefault_ReturnsTypeDefault_WhenNoArgument()
        {
            var result = Result<int>.Failure("ERR", "fail");

            Assert.Equal(0, result.GetValueOrDefault());
        }

        [Fact]
        public void ToString_Success_ReturnsCorrectFormat()
        {
            var result = Result<int>.Success(42);

            Assert.Equal("Success(42)", result.ToString());
        }

        [Fact]
        public void ToString_Failure_ReturnsCorrectFormat()
        {
            var result = Result<int>.Failure("NOT_FOUND", "Item not found");

            Assert.Equal("Failure(NOT_FOUND: Item not found)", result.ToString());
        }

        [Fact]
        public void ToResult_Extension_ProducesSuccess()
        {
            var result = "hello".ToResult();

            Assert.True(result.IsSuccess);
            Assert.Equal("hello", result.Value);
        }

        [Fact]
        public void ToFailedResult_Extension_ProducesFailure()
        {
            var error = new ResultError("ERR", "bad");
            var result = error.ToFailedResult<int>();

            Assert.True(result.IsFailure);
            Assert.Equal("ERR", result.Error.Code);
        }

        #region End-to-End Mediator Integration

        public record GetItemRequest(int Id) : IRequest<Result<string>>;

        public class GetItemHandler : IRequestHandler<GetItemRequest, Result<string>>
        {
            public Task<Result<string>> Handle(GetItemRequest request, CancellationToken cancellationToken = default)
            {
                if (request.Id > 0)
                    return Task.FromResult<Result<string>>($"Item-{request.Id}");

                return Task.FromResult(Result<string>.Failure("NOT_FOUND", "Item not found"));
            }
        }

        [Fact]
        public async Task EndToEnd_SuccessPath_ThroughMediator()
        {
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<GetItemHandler>();
            });
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new GetItemRequest(1));

            Assert.True(result.IsSuccess);
            Assert.Equal("Item-1", result.Value);
        }

        [Fact]
        public async Task EndToEnd_FailurePath_ThroughMediator()
        {
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.RegisterHandler<GetItemHandler>();
            });
            var provider = services.BuildServiceProvider();
            var mediator = provider.GetRequiredService<IMediator>();

            var result = await mediator.Send(new GetItemRequest(-1));

            Assert.True(result.IsFailure);
            Assert.Equal("NOT_FOUND", result.Error.Code);
            Assert.Equal("Item not found", result.Error.Message);
        }

        #endregion
    }
}
