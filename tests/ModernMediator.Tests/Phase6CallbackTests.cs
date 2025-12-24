using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;
using Xunit;

namespace ModernMediator.Tests
{
    public record ConfirmationRequest(string Message);
    public record ConfirmationResponse(bool Confirmed, string Source);

    public record PingMessage(int Id);
    public record PongResponse(int Id, string Handler);

    public class Phase6CallbackTests
    {
        [Fact]
        public void Publish_WithCallback_CollectsResponsesFromAllSubscribers()
        {
            // Arrange
            using var mediator = Mediator.Create();

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "Handler1"),
                weak: false);

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "Handler2"),
                weak: false);

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "Handler3"),
                weak: false);

            // Act
            var responses = mediator.Publish<PingMessage, PongResponse>(new PingMessage(42));

            // Assert
            Assert.Equal(3, responses.Count);
            Assert.All(responses, r => Assert.Equal(42, r.Id));
            Assert.Contains(responses, r => r.Handler == "Handler1");
            Assert.Contains(responses, r => r.Handler == "Handler2");
            Assert.Contains(responses, r => r.Handler == "Handler3");
        }

        [Fact]
        public void Publish_WithCallback_NoSubscribers_ReturnsEmptyList()
        {
            // Arrange
            using var mediator = Mediator.Create();

            // Act
            var responses = mediator.Publish<PingMessage, PongResponse>(new PingMessage(42));

            // Assert
            Assert.Empty(responses);
        }

        [Fact]
        public void Publish_WithCallback_NullMessage_ReturnsEmptyList()
        {
            // Arrange
            using var mediator = Mediator.Create();

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "Handler1"),
                weak: false);

            // Act
            var responses = mediator.Publish<PingMessage, PongResponse>(null);

            // Assert
            Assert.Empty(responses);
        }

        [Fact]
        public void Subscribe_WithCallback_ReturnsDisposableToken()
        {
            // Arrange
            using var mediator = Mediator.Create();

            // Act
            var subscription = mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "Handler1"),
                weak: false);

            var responsesBeforeDispose = mediator.Publish<PingMessage, PongResponse>(new PingMessage(1));

            subscription.Dispose();

            var responsesAfterDispose = mediator.Publish<PingMessage, PongResponse>(new PingMessage(2));

            // Assert
            Assert.Single(responsesBeforeDispose);
            Assert.Empty(responsesAfterDispose);
        }

        [Fact]
        public void Subscribe_WithCallback_Filter_OnlyMatchingMessagesGetResponses()
        {
            // Arrange
            using var mediator = Mediator.Create();

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "FilteredHandler"),
                weak: false,
                filter: msg => msg.Id > 10);

            // Act
            var lowIdResponses = mediator.Publish<PingMessage, PongResponse>(new PingMessage(5));
            var highIdResponses = mediator.Publish<PingMessage, PongResponse>(new PingMessage(15));

            // Assert
            Assert.Empty(lowIdResponses);
            Assert.Single(highIdResponses);
            Assert.Equal("FilteredHandler", highIdResponses[0].Handler);
        }

        [Fact]
        public async Task PublishAsync_WithCallback_CollectsResponsesFromAllAsyncSubscribers()
        {
            // Arrange
            using var mediator = Mediator.Create();

            mediator.SubscribeAsync<PingMessage, PongResponse>(
                async msg =>
                {
                    await Task.Delay(10);
                    return new PongResponse(msg.Id, "AsyncHandler1");
                },
                weak: false);

            mediator.SubscribeAsync<PingMessage, PongResponse>(
                async msg =>
                {
                    await Task.Delay(10);
                    return new PongResponse(msg.Id, "AsyncHandler2");
                },
                weak: false);

            // Act
            var responses = await mediator.PublishAsync<PingMessage, PongResponse>(new PingMessage(42));

            // Assert
            Assert.Equal(2, responses.Count);
            Assert.All(responses, r => Assert.Equal(42, r.Id));
            Assert.Contains(responses, r => r.Handler == "AsyncHandler1");
            Assert.Contains(responses, r => r.Handler == "AsyncHandler2");
        }

        [Fact]
        public async Task PublishAsync_WithCallback_NoSubscribers_ReturnsEmptyList()
        {
            // Arrange
            using var mediator = Mediator.Create();

            // Act
            var responses = await mediator.PublishAsync<PingMessage, PongResponse>(new PingMessage(42));

            // Assert
            Assert.Empty(responses);
        }

        [Fact]
        public async Task PublishAsync_WithCallback_NullMessage_ReturnsEmptyList()
        {
            // Arrange
            using var mediator = Mediator.Create();

            mediator.SubscribeAsync<PingMessage, PongResponse>(
                async msg =>
                {
                    await Task.Delay(1);
                    return new PongResponse(msg.Id, "Handler");
                },
                weak: false);

            // Act
            var responses = await mediator.PublishAsync<PingMessage, PongResponse>(null);

            // Assert
            Assert.Empty(responses);
        }

        [Fact]
        public async Task PublishAsync_WithCallback_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            using var mediator = Mediator.Create();
            using var cts = new CancellationTokenSource();

            mediator.SubscribeAsync<PingMessage, PongResponse>(
                async msg =>
                {
                    await Task.Delay(1000);
                    return new PongResponse(msg.Id, "SlowHandler");
                },
                weak: false);

            // Act
            cts.CancelAfter(50);

            // Assert - TaskCanceledException inherits from OperationCanceledException
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => mediator.PublishAsync<PingMessage, PongResponse>(new PingMessage(42), cts.Token));
        }

        [Fact]
        public void Publish_WithCallback_HandlerThrows_ContinueAndAggregate()
        {
            // Arrange
            using var mediator = Mediator.Create();
            mediator.ErrorPolicy = ErrorPolicy.ContinueAndAggregate;

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "GoodHandler"),
                weak: false);

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => throw new InvalidOperationException("Handler failed"),
                weak: false);

            // Act & Assert
            var ex = Assert.Throws<AggregateException>(
                () => mediator.Publish<PingMessage, PongResponse>(new PingMessage(42)));

            Assert.Single(ex.InnerExceptions);
            Assert.IsType<InvalidOperationException>(ex.InnerExceptions[0]);
        }

        [Fact]
        public void Publish_WithCallback_HandlerThrows_StopOnFirstError()
        {
            // Arrange
            using var mediator = Mediator.Create();
            mediator.ErrorPolicy = ErrorPolicy.StopOnFirstError;

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => throw new InvalidOperationException("First handler failed"),
                weak: false);

            mediator.Subscribe<PingMessage, PongResponse>(
                msg => new PongResponse(msg.Id, "SecondHandler"),
                weak: false);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(
                () => mediator.Publish<PingMessage, PongResponse>(new PingMessage(42)));

            Assert.Equal("First handler failed", ex.Message);
        }

        [Fact]
        public void Publish_WithCallback_RealWorldDialogScenario()
        {
            // Arrange - simulates confirmation dialog pattern
            using var mediator = Mediator.Create();

            // Dialog service subscribes to confirmation requests
            mediator.Subscribe<ConfirmationRequest, ConfirmationResponse>(
                request => new ConfirmationResponse(
                    Confirmed: request.Message.Contains("delete"),
                    Source: "DialogService"),
                weak: false);

            // Act - publisher asks for confirmation
            var responses = mediator.Publish<ConfirmationRequest, ConfirmationResponse>(
                new ConfirmationRequest("Do you want to delete this item?"));

            // Assert
            Assert.Single(responses);
            Assert.True(responses[0].Confirmed);
            Assert.Equal("DialogService", responses[0].Source);
        }

        [Fact]
        public async Task PublishAsync_WithCallback_MultipleHandlers_BothInvoked()
        {
            // Arrange - simplified test to verify multiple handlers work
            using var mediator = Mediator.Create();
            var invocations = new List<string>();

            mediator.SubscribeAsync<ValidateRequest, ValidationResult>(
                async request =>
                {
                    invocations.Add("Handler1");
                    await Task.Delay(1);
                    return new ValidationResult(true, "Handler1");
                },
                weak: false);

            mediator.SubscribeAsync<ValidateRequest, ValidationResult>(
                async request =>
                {
                    invocations.Add("Handler2");
                    await Task.Delay(1);
                    return new ValidationResult(true, "Handler2");
                },
                weak: false);

            // Act
            var results = await mediator.PublishAsync<ValidateRequest, ValidationResult>(
                new ValidateRequest("test"));

            // Assert
            Assert.Equal(2, invocations.Count); // Both handlers were invoked
            Assert.Equal(2, results.Count);     // Both returned results
        }

        [Fact]
        public async Task PublishAsync_WithCallback_RealWorldValidationScenario()
        {
            // Arrange - simulates multiple validators
            using var mediator = Mediator.Create();

            // Validator 1: Length check (min 5 chars)
            mediator.SubscribeAsync<ValidateRequest, ValidationResult>(
                async request =>
                {
                    await Task.Delay(5);
                    var isValid = request.Value.Length >= 5;
                    return new ValidationResult(isValid, isValid ? null : "Too short");
                },
                weak: false);

            // Validator 2: Character check (no ! allowed)
            mediator.SubscribeAsync<ValidateRequest, ValidationResult>(
                async request =>
                {
                    await Task.Delay(5);
                    var isValid = !request.Value.Contains("!");
                    return new ValidationResult(isValid, isValid ? null : "Invalid character");
                },
                weak: false);

            // Act - "AB!" fails both: length (3 < 5) and character check (contains !)
            var results = await mediator.PublishAsync<ValidateRequest, ValidationResult>(
                new ValidateRequest("AB!"));

            // Assert - should have 2 responses (one from each validator)
            Assert.Equal(2, results.Count);
            
            // Both validations should fail
            Assert.All(results, r => Assert.False(r.IsValid));
            Assert.Contains(results, r => r.ErrorMessage == "Too short");
            Assert.Contains(results, r => r.ErrorMessage == "Invalid character");
        }
    }

    public record ValidateRequest(string Value);
    public record ValidationResult(bool IsValid, string? ErrorMessage);
}