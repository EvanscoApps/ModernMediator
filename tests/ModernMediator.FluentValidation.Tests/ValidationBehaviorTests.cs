using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using ModernMediator.FluentValidation;
using Xunit;

namespace ModernMediator.FluentValidation.Tests
{
    #region Test Requests, Responses, and Handlers

    public record TestRequest(string Name, int Age) : IRequest<TestResponse>;

    public record TestResponse(string Message);

    public class TestRequestHandler : IRequestHandler<TestRequest, TestResponse>
    {
        public Task<TestResponse> Handle(TestRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestResponse($"Hello, {request.Name}!"));
        }
    }

    #endregion

    #region Test Validators

    public class NameValidator : AbstractValidator<TestRequest>
    {
        public NameValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    public class AgeValidator : AbstractValidator<TestRequest>
    {
        public AgeValidator()
        {
            RuleFor(x => x.Age).GreaterThan(0).WithMessage("Age must be positive.");
        }
    }

    public class AlwaysPassValidator : AbstractValidator<TestRequest>
    {
        public AlwaysPassValidator()
        {
            // No rules — always passes
        }
    }

    public class AsyncValidator : AbstractValidator<TestRequest>
    {
        public AsyncValidator()
        {
            RuleFor(x => x.Name).MustAsync(async (name, ct) =>
            {
                await Task.Delay(10, ct);
                return !string.IsNullOrWhiteSpace(name);
            }).WithMessage("Async: Name must not be empty.");
        }
    }

    #endregion

    public class ValidationBehaviorTests
    {
        [Fact]
        public async Task Handle_ValidationFails_ThrowsModernValidationException()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>> { new NameValidator() };
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var request = new TestRequest("", 25);

            // Act & Assert
            await Assert.ThrowsAsync<ModernValidationException>(
                () => behavior.Handle(request, (req, ct) => Task.FromResult(new TestResponse("ok")), CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidationPasses_CallsNextAndReturnsResult()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>> { new NameValidator() };
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var request = new TestRequest("Alice", 25);
            var expectedResponse = new TestResponse("Hello, Alice!");

            // Act
            var result = await behavior.Handle(request, (req, ct) => Task.FromResult(expectedResponse), CancellationToken.None);

            // Assert
            Assert.Equal(expectedResponse, result);
        }

        [Fact]
        public async Task Handle_NoValidatorsRegistered_CallsNextWithoutError()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>>();
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var request = new TestRequest("Alice", 25);
            var expectedResponse = new TestResponse("Hello, Alice!");

            // Act
            var result = await behavior.Handle(request, (req, ct) => Task.FromResult(expectedResponse), CancellationToken.None);

            // Assert
            Assert.Equal(expectedResponse, result);
        }

        [Fact]
        public async Task Handle_MultipleValidators_AggregatesAllFailures()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>> { new NameValidator(), new AgeValidator() };
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var request = new TestRequest("", -1);

            // Act
            var ex = await Assert.ThrowsAsync<ModernValidationException>(
                () => behavior.Handle(request, (req, ct) => Task.FromResult(new TestResponse("ok")), CancellationToken.None));

            // Assert
            Assert.Equal(2, ex.Errors.Count);
            Assert.Contains(ex.Errors, e => e.PropertyName == "Name");
            Assert.Contains(ex.Errors, e => e.PropertyName == "Age");
        }

        [Fact]
        public async Task Handle_ValidationFails_ExceptionContainsAllExpectedFailures()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>> { new NameValidator(), new AgeValidator() };
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var request = new TestRequest("", 0);

            // Act
            var ex = await Assert.ThrowsAsync<ModernValidationException>(
                () => behavior.Handle(request, (req, ct) => Task.FromResult(new TestResponse("ok")), CancellationToken.None));

            // Assert
            Assert.Equal(2, ex.Errors.Count);
            Assert.Contains(ex.Errors, e => e.ErrorMessage == "Name is required.");
            Assert.Contains(ex.Errors, e => e.ErrorMessage == "Age must be positive.");
            Assert.Contains("2 error(s)", ex.Message);
        }

        [Fact]
        public async Task Handle_ValidRequest_FullPipeline_NoExceptionAndHandlerResponseReturned()
        {
            // Arrange — valid request goes through the full pipeline
            var validators = new List<IValidator<TestRequest>> { new NameValidator(), new AgeValidator() };
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var request = new TestRequest("Alice", 30);
            var expectedResponse = new TestResponse("Hello, Alice!");

            // Act
            var result = await behavior.Handle(
                request,
                (req, ct) => Task.FromResult(expectedResponse),
                CancellationToken.None);

            // Assert — no exception thrown, handler response returned normally
            Assert.Equal(expectedResponse, result);
            Assert.Equal("Hello, Alice!", result.Message);
        }

        [Fact]
        public async Task Handle_NoRegisteredValidator_PassesThroughWithoutException()
        {
            // Arrange — empty validator list (no validators registered for this request type)
            var validators = new List<IValidator<TestRequest>>();
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var request = new TestRequest("Bob", 25);
            var expectedResponse = new TestResponse("Hello, Bob!");

            // Act
            var result = await behavior.Handle(
                request,
                (req, ct) => Task.FromResult(expectedResponse),
                CancellationToken.None);

            // Assert — passes through with no exception
            Assert.Equal(expectedResponse, result);
        }

        [Fact]
        public async Task Handle_AsyncValidationRule_IsProperlyAwaited()
        {
            // Arrange
            var validators = new List<IValidator<TestRequest>> { new AsyncValidator() };
            var behavior = new ValidationBehavior<TestRequest, TestResponse>(validators);
            var failingRequest = new TestRequest("", 25);
            var passingRequest = new TestRequest("Alice", 25);
            var expectedResponse = new TestResponse("Hello, Alice!");

            // Act — failing async rule
            var ex = await Assert.ThrowsAsync<ModernValidationException>(
                () => behavior.Handle(failingRequest, (req, ct) => Task.FromResult(new TestResponse("ok")), CancellationToken.None));

            // Assert — failure case
            Assert.Single(ex.Errors);
            Assert.Equal("Async: Name must not be empty.", ex.Errors[0].ErrorMessage);

            // Act — passing async rule
            var result = await behavior.Handle(passingRequest, (req, ct) => Task.FromResult(expectedResponse), CancellationToken.None);

            // Assert — success case
            Assert.Equal(expectedResponse, result);
        }
    }
}
