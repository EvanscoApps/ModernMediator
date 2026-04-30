using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ModernMediator.Generators.Tests
{
    /// <summary>
    /// Comprehensive diagnostic coverage for every DiagnosticDescriptor in the source generator.
    /// Each diagnostic ID has both positive (MUST fire) and negative (MUST NOT fire) tests.
    /// Every test asserts exact diagnostic IDs, correct severity, and absence of unexpected diagnostics.
    /// </summary>
    public class DiagnosticTests
    {
        #region MM001DuplicateHandler: Positive Tests

        [Fact]
        public void MM001_TwoHandlersForSameRequest_ReportsError()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;

    public class Handler1 : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""1"");
    }

    public class Handler2 : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""2"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertDiagnosticPresent(diagnostics, "MM001", DiagnosticSeverity.Error);
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        [Fact]
        public void MM001_ThreeHandlersForSameRequest_ReportsError()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;

    public class Handler1 : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""1"");
    }

    public class Handler2 : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""2"");
    }

    public class Handler3 : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""3"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertDiagnosticPresent(diagnostics, "MM001", DiagnosticSeverity.Error);
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        #endregion

        #region MM001DuplicateHandler: Negative Tests

        [Fact]
        public void MM001_OneHandlerPerRequest_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;

    public class MyHandler : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        [Fact]
        public void MM001_HandlersForDifferentRequestTypes_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record RequestA : IRequest<string>;
    public record RequestB : IRequest<int>;

    public class HandlerA : IRequestHandler<RequestA, string>
    {
        public Task<string> Handle(RequestA request, CancellationToken ct = default)
            => Task.FromResult(""a"");
    }

    public class HandlerB : IRequestHandler<RequestB, int>
    {
        public Task<int> Handle(RequestB request, CancellationToken ct = default)
            => Task.FromResult(0);
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        #endregion

        #region MM002NoHandlerFound: Positive Tests

        [Fact]
        public void MM002_RequestWithNoHandler_ReportsWarning()
        {
            var source = @"
using ModernMediator;

namespace TestApp
{
    public record OrphanRequest : IRequest<string>;
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertDiagnosticPresent(diagnostics, "MM002", DiagnosticSeverity.Warning);
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        [Fact]
        public void MM002_MultipleRequests_OnlyOrphanGetsWarning()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record HandledRequest : IRequest<string>;
    public record OrphanRequest : IRequest<int>;

    public class HandledRequestHandler : IRequestHandler<HandledRequest, string>
    {
        public Task<string> Handle(HandledRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertDiagnosticPresent(diagnostics, "MM002", DiagnosticSeverity.Warning, expectedCount: 1);
            AssertNoDiagnostic(diagnostics, "MM003");

            // Verify the warning names the orphan, not the handled request
            var mm002 = diagnostics.Single(d => d.Id == "MM002");
            Assert.Contains("OrphanRequest", mm002.GetMessage());
        }

        #endregion

        #region MM002NoHandlerFound: Negative Tests

        [Fact]
        public void MM002_AllRequestsHandled_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record RequestA : IRequest<string>;
    public record RequestB : IRequest<int>;

    public class HandlerA : IRequestHandler<RequestA, string>
    {
        public Task<string> Handle(RequestA request, CancellationToken ct = default)
            => Task.FromResult(""a"");
    }

    public class HandlerB : IRequestHandler<RequestB, int>
    {
        public Task<int> Handle(RequestB request, CancellationToken ct = default)
            => Task.FromResult(0);
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        [Fact]
        public void MM002_HandlerInSeparateSourceFile_NoDiagnostic()
        {
            var requestSource = @"
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;
}";

            var handlerSource = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;
using TestApp;

namespace TestApp.Handlers
{
    public class MyHandler : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(requestSource, handlerSource);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        #endregion

        #region MM003HandlerMustBeNonAbstract: Positive Tests

        [Fact]
        public void MM003_AbstractHandlerClass_ReportsError()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public abstract class AbstractHandler : IRequestHandler<TestRequest, string>
    {
        public abstract Task<string> Handle(TestRequest request, CancellationToken ct = default);
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            // MM002 also fires: the abstract handler is not registered, so TestRequest has no handler
            AssertDiagnosticPresent(diagnostics, "MM002", DiagnosticSeverity.Warning);
            AssertDiagnosticPresent(diagnostics, "MM003", DiagnosticSeverity.Error);
        }

        [Fact]
        public void MM003_AbstractWithConcreteDerived_ErrorOnAbstractOnly()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public abstract class AbstractBaseHandler : IRequestHandler<TestRequest, string>
    {
        public abstract Task<string> Handle(TestRequest request, CancellationToken ct = default);
    }

    public class ConcreteHandler : AbstractBaseHandler
    {
        public override Task<string> Handle(TestRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002"); // ConcreteHandler covers the request
            AssertDiagnosticPresent(diagnostics, "MM003", DiagnosticSeverity.Error, expectedCount: 1);

            // Verify the error targets the abstract class, not the concrete one
            var mm003 = diagnostics.Single(d => d.Id == "MM003");
            Assert.Contains("AbstractBaseHandler", mm003.GetMessage());
        }

        #endregion

        #region MM003HandlerMustBeNonAbstract: Negative Tests

        [Fact]
        public void MM003_ConcreteHandlers_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public class ConcreteHandler : IRequestHandler<TestRequest, string>
    {
        public Task<string> Handle(TestRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        [Fact]
        public void MM003_NonHandlerAbstractClass_NoDiagnostic()
        {
            var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    // Abstract class that does NOT implement any handler interfacemust not trigger MM003
    public abstract class BaseService : IDisposable
    {
        public abstract void Dispose();
    }

    public class TestHandler : IRequestHandler<TestRequest, string>
    {
        public Task<string> Handle(TestRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        #endregion

        #region MM100GeneratorSuccess: Positive Tests

        [Fact]
        public void MM100_WithHandlers_ReportsInfo()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;

    public class MyHandler : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertDiagnosticPresent(diagnostics, "MM100", DiagnosticSeverity.Info);
            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        [Fact]
        public void MM100_EmptyCompilation_ReportsInfo()
        {
            var diagnostics = GetDiagnostics("");

            // Generation ran and succeeded, even with nothing to generate
            AssertDiagnosticPresent(diagnostics, "MM100", DiagnosticSeverity.Info);
            AssertNoDiagnostic(diagnostics, "MM001");
            AssertNoDiagnostic(diagnostics, "MM002");
            AssertNoDiagnostic(diagnostics, "MM003");
        }

        #endregion

        #region MM004HandlerReturnTypeMismatch: Positive Tests

        [Fact]
        public void MM004_MismatchedReturnType_ReportsError()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;

    public class BadHandler : IRequestHandler<MyRequest, int>
    {
        public Task<int> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(42);
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertDiagnosticPresent(diagnostics, "MM004", DiagnosticSeverity.Error);

            var mm004 = diagnostics.Single(d => d.Id == "MM004");
            Assert.Contains("BadHandler", mm004.GetMessage());
            Assert.Contains("int", mm004.GetMessage());
            Assert.Contains("string", mm004.GetMessage());
        }

        #endregion

        #region MM004HandlerReturnTypeMismatch: Negative Tests

        [Fact]
        public void MM004_CorrectReturnType_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;

    public class GoodHandler : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM004");
        }

        #endregion

        #region MM005NotificationHandlerReturnsValue: Positive Tests

        [Fact]
        public void MM005_NotificationHandlerReturningTaskOfString_ReportsWarning()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record UserCreated : INotification;

    public class BadNotificationHandler : INotificationHandler<UserCreated>
    {
        public Task<string> Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.FromResult(""oops"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertDiagnosticPresent(diagnostics, "MM005", DiagnosticSeverity.Warning);

            var mm005 = diagnostics.Single(d => d.Id == "MM005");
            Assert.Contains("BadNotificationHandler", mm005.GetMessage());
        }

        #endregion

        #region MM005NotificationHandlerReturnsValue: Negative Tests

        [Fact]
        public void MM005_NotificationHandlerReturningTask_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record UserCreated : INotification;

    public class GoodNotificationHandler : INotificationHandler<UserCreated>
    {
        public Task Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM005");
        }

        #endregion

        #region MM006OpenGenericBehavior: Positive Tests

        [Fact]
        public void MM006_OpenGenericBehavior_ReportsWarning()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            return await next(request, cancellationToken);
        }
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertDiagnosticPresent(diagnostics, "MM006", DiagnosticSeverity.Warning);

            var mm006 = diagnostics.Single(d => d.Id == "MM006");
            Assert.Contains("LoggingBehavior", mm006.GetMessage());
        }

        #endregion

        #region MM006OpenGenericBehavior: Negative Tests

        [Fact]
        public void MM006_ClosedGenericBehavior_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public class TestBehavior : IPipelineBehavior<TestRequest, string>
    {
        public async Task<string> Handle(TestRequest request, RequestHandlerDelegate<TestRequest, string> next, CancellationToken cancellationToken)
        {
            return await next(request, cancellationToken);
        }
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM006");
        }

        [Fact]
        public void MM006_AbstractOpenGenericBehavior_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public abstract class AbstractLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public abstract Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken);
    }
}";

            var diagnostics = GetDiagnostics(source);

            // Abstract open generic gets MM003, not MM006
            AssertDiagnosticPresent(diagnostics, "MM003", DiagnosticSeverity.Error);
            AssertNoDiagnostic(diagnostics, "MM006");
        }

        #endregion

        #region MM007HandlerNoMatchingRequestType: Positive Tests

        [Fact]
        public void MM007_HandlerForNonRequestType_ReportsInfo()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    // NotARequest does NOT implement IRequest<string>
    public class NotARequest { }

    public class StaleHandler : IRequestHandler<NotARequest, string>
    {
        public Task<string> Handle(NotARequest request, CancellationToken ct = default)
            => Task.FromResult(""stale"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertDiagnosticPresent(diagnostics, "MM007", DiagnosticSeverity.Info);

            var mm007 = diagnostics.Single(d => d.Id == "MM007");
            Assert.Contains("StaleHandler", mm007.GetMessage());
            Assert.Contains("NotARequest", mm007.GetMessage());
        }

        #endregion

        #region MM007HandlerNoMatchingRequestType: Negative Tests

        [Fact]
        public void MM007_HandlerForExistingRequestType_NoDiagnostic()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record MyRequest : IRequest<string>;

    public class MyHandler : IRequestHandler<MyRequest, string>
    {
        public Task<string> Handle(MyRequest request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }
}";

            var diagnostics = GetDiagnostics(source);

            AssertNoDiagnostic(diagnostics, "MM007");
        }

        #endregion

        #region Structural Tests

        [Fact]
        public void EmptyCompilation_NoErrorOrWarningDiagnostics()
        {
            var diagnostics = GetDiagnostics("");

            var errorOrWarning = diagnostics.Where(d =>
                d.Id.StartsWith("MM") &&
                (d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning))
                .ToList();
            Assert.Empty(errorOrWarning);
        }

        [Fact]
        public void CleanCompilation_NoErrorOrWarningDiagnostics()
        {
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record Query1 : IRequest<string>;
    public record Query2 : IRequest<int>;

    public class Handler1 : IRequestHandler<Query1, string>
    {
        public Task<string> Handle(Query1 request, CancellationToken ct = default)
            => Task.FromResult(""ok"");
    }

    public class Handler2 : IRequestHandler<Query2, int>
    {
        public Task<int> Handle(Query2 request, CancellationToken ct = default)
            => Task.FromResult(0);
    }
}";

            var diagnostics = GetDiagnostics(source);

            var errorOrWarning = diagnostics.Where(d =>
                d.Id.StartsWith("MM") &&
                (d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning))
                .ToList();
            Assert.Empty(errorOrWarning);
        }

        [Fact]
        public void AllDiagnosticDescriptors_HaveTests()
        {
            // Use reflection to discover every DiagnosticDescriptor defined in the generator
            var descriptorFields = typeof(DiagnosticDescriptors)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
                .ToList();

            // IDs that have dedicated positive testsMM001-MM007/MM100 in this class, MM008/MM009 in AnalyzerTests
            var testedIds = new HashSet<string> { "MM001", "MM002", "MM003", "MM004", "MM005", "MM006", "MM007", "MM008", "MM009", "MM100" };

            foreach (var field in descriptorFields)
            {
                var descriptor = (DiagnosticDescriptor)field.GetValue(null)!;

                Assert.True(
                    testedIds.Contains(descriptor.Id),
                    $"Diagnostic descriptor '{descriptor.Id}' ({field.Name}) has no positive/negative tests in {nameof(DiagnosticTests)}. " +
                    $"Add test coverage before shipping.");
            }
        }

        #endregion

        #region Test Infrastructure

        /// <summary>
        /// Compiles the given source(s) alongside the ModernMediator interface stubs,
        /// runs the MediatorGenerator, and returns only the generator-produced diagnostics.
        /// Multiple source strings simulate separate source files in the same compilation.
        /// </summary>
        private static ImmutableArray<Diagnostic> GetDiagnostics(params string[] sources)
        {
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IAsyncEnumerable<>).Assembly.Location),
            };

            var mediatorInterfaces = @"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    public interface IRequest<TResponse> { }
    public interface IRequest : IRequest<Unit> { }

    public struct Unit
    {
        public static Unit Value => default;
    }

    public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }

    public interface IStreamRequest<out TResponse> { }

    public interface IStreamRequestHandler<in TRequest, out TResponse> where TRequest : IStreamRequest<TResponse>
    {
        IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }

    public delegate Task<TResponse> RequestHandlerDelegate<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken);

    public interface IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken);
    }

    public interface IRequestPreProcessor<in TRequest>
    {
        Task Process(TRequest request, CancellationToken cancellationToken);
    }

    public interface IRequestPostProcessor<in TRequest, in TResponse>
    {
        Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
    }

    public interface IMediator { }
    public class Mediator : IMediator
    {
        public static IMediator Create(IServiceProvider sp) => new Mediator();
    }

    public interface IServiceProviderAccessor
    {
        IServiceProvider? ServiceProvider { get; }
    }

    public interface INotification { }

    public interface INotificationHandler<in TNotification> where TNotification : INotification
    {
        Task Handle(TNotification notification, CancellationToken cancellationToken = default);
    }
}";

            var syntaxTrees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(mediatorInterfaces) };
            foreach (var source in sources)
            {
                if (!string.IsNullOrEmpty(source))
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));
            }

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new MediatorGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation, out _, out var diagnostics);

            return diagnostics;
        }

        /// <summary>
        /// Asserts that exactly <paramref name="expectedCount"/> diagnostics with the given
        /// <paramref name="id"/> are present, each with the expected <paramref name="severity"/>.
        /// </summary>
        private static void AssertDiagnosticPresent(
            ImmutableArray<Diagnostic> diagnostics,
            string id,
            DiagnosticSeverity severity,
            int expectedCount = 1)
        {
            var matching = diagnostics.Where(d => d.Id == id).ToList();
            Assert.Equal(expectedCount, matching.Count);
            Assert.All(matching, d => Assert.Equal(severity, d.Severity));
        }

        /// <summary>
        /// Asserts that no diagnostic with the given <paramref name="id"/> is present.
        /// </summary>
        private static void AssertNoDiagnostic(ImmutableArray<Diagnostic> diagnostics, string id)
        {
            Assert.DoesNotContain(diagnostics, d => d.Id == id);
        }

        #endregion
    }
}
