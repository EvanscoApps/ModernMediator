using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ModernMediator.Generators.Tests
{
    public class MediatorGeneratorTests
    {
        [Fact]
        public void Generator_DiscoversRequestHandler()
        {
            // Arrange
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record GetUserQuery(int Id) : IRequest<UserDto>;
    public record UserDto(int Id, string Name);

    public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
    {
        public Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UserDto(request.Id, ""Test""));
        }
    }
}";

            // Act
            var (diagnostics, output) = RunGenerator(source);

            // Assert
            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("GetUserHandler", output);
            Assert.Contains("IRequestHandler<TestApp.GetUserQuery, TestApp.UserDto>", output);
        }

        [Fact]
        public void Generator_DiscoversStreamHandler()
        {
            // Arrange
            var source = @"
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using ModernMediator;

namespace TestApp
{
    public record GetUsersRequest : IStreamRequest<UserDto>;
    public record UserDto(int Id, string Name);

    public class GetUsersHandler : IStreamRequestHandler<GetUsersRequest, UserDto>
    {
        public async IAsyncEnumerable<UserDto> Handle(
            GetUsersRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new UserDto(1, ""Test"");
        }
    }
}";

            // Act
            var (diagnostics, output) = RunGenerator(source);

            // Assert
            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("GetUsersHandler", output);
            Assert.Contains("IStreamRequestHandler", output);
        }

        [Fact]
        public void Generator_DiscoversPipelineBehavior()
        {
            // Arrange
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public class LoggingBehavior : IPipelineBehavior<TestRequest, string>
    {
        public async Task<string> Handle(TestRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            return await next();
        }
    }
}";

            // Act
            var (diagnostics, output) = RunGenerator(source);

            // Assert
            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("LoggingBehavior", output);
            Assert.Contains("IPipelineBehavior", output);
        }

        [Fact]
        public void Generator_DiscoversPreProcessor()
        {
            // Arrange
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public class ValidationPreProcessor : IRequestPreProcessor<TestRequest>
    {
        public Task Process(TestRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}";

            // Act
            var (diagnostics, output) = RunGenerator(source);

            // Assert
            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("ValidationPreProcessor", output);
            Assert.Contains("IRequestPreProcessor", output);
        }

        [Fact]
        public void Generator_DiscoversPostProcessor()
        {
            // Arrange
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public class CachingPostProcessor : IRequestPostProcessor<TestRequest, string>
    {
        public Task Process(TestRequest request, string response, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}";

            // Act
            var (diagnostics, output) = RunGenerator(source);

            // Assert
            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            Assert.Contains("CachingPostProcessor", output);
            Assert.Contains("IRequestPostProcessor", output);
        }

        [Fact]
        public void Generator_ReportsDuplicateHandlers()
        {
            // Arrange
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record GetUserQuery(int Id) : IRequest<string>;

    public class GetUserHandler1 : IRequestHandler<GetUserQuery, string>
    {
        public Task<string> Handle(GetUserQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(""Handler1"");
    }

    public class GetUserHandler2 : IRequestHandler<GetUserQuery, string>
    {
        public Task<string> Handle(GetUserQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(""Handler2"");
    }
}";

            // Act
            var (diagnostics, _) = RunGenerator(source);

            // Assert
            Assert.Contains(diagnostics, d => d.Id == "MM001");
        }

        [Fact]
        public void Generator_IgnoresAbstractClasses()
        {
            // Arrange
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record TestRequest : IRequest<string>;

    public abstract class AbstractHandler : IRequestHandler<TestRequest, string>
    {
        public abstract Task<string> Handle(TestRequest request, CancellationToken cancellationToken = default);
    }
}";

            // Act
            var (diagnostics, output) = RunGenerator(source);

            // Assert
            Assert.DoesNotContain("AbstractHandler", output);
        }

        [Fact]
        public void Generator_GeneratesStronglyTypedSendExtensions()
        {
            // Arrange
            var source = @"
using System.Threading;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public record GetUserQuery(int Id) : IRequest<UserDto>;
    public record UserDto(int Id, string Name);

    public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
    {
        public Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UserDto(request.Id, ""Test""));
        }
    }
}";

            // Act
            var (_, output) = RunGenerator(source, "ModernMediator.SendExtensions.g.cs");

            // Assert
            Assert.Contains("public static async Task<TestApp.UserDto> Send(this IMediator mediator, TestApp.GetUserQuery request", output);
        }

        [Fact]
        public void Generator_GeneratesSummary()
        {
            // Arrange
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
        public Task<string> Handle(Query1 request, CancellationToken ct = default) => Task.FromResult("""");
    }

    public class Handler2 : IRequestHandler<Query2, int>
    {
        public Task<int> Handle(Query2 request, CancellationToken ct = default) => Task.FromResult(0);
    }
}";

            // Act
            var (_, output) = RunGenerator(source);

            // Assert
            Assert.Contains("Handlers: 2", output);
        }

        private static (ImmutableArray<Diagnostic> Diagnostics, string Output) RunGenerator(string source, string? outputFileName = null)
        {
            outputFileName ??= "ModernMediator.Generated.g.cs";

            // Create compilation with ModernMediator types
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IAsyncEnumerable<>).Assembly.Location),
            };

            // Add ModernMediator interfaces as source
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

    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

    public interface IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
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
}";

            var syntaxTrees = new[]
            {
                CSharpSyntaxTree.ParseText(mediatorInterfaces),
                CSharpSyntaxTree.ParseText(source)
            };

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Run generator
            var generator = new MediatorGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Get generated source
            var runResult = driver.GetRunResult();
            var generatedSource = runResult.GeneratedTrees
                .FirstOrDefault(t => t.FilePath.EndsWith(outputFileName))
                ?.GetText()
                .ToString() ?? "";

            return (diagnostics, generatedSource);
        }
    }
}