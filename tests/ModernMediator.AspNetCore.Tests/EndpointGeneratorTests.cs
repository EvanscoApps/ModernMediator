using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernMediator.AspNetCore.Generators;
using Xunit;

namespace ModernMediator.AspNetCore.Tests
{
    public class EndpointGeneratorTests
    {
        [Fact]
        public void PostEndpoint_GeneratesMapPost()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    [Endpoint(""/items"", ""POST"")]
    public record CreateItemRequest(string Name) : IRequest<string>;
}";

            var (diagnostics, output) = RunGenerator(source);

            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("MapPost", output);
            Assert.Contains("/items", output);
            Assert.Contains("TestApp.CreateItemRequest", output);
            Assert.Contains("[FromBody]", output);
        }

        [Fact]
        public void GetEndpoint_GeneratesMapGet()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    [Endpoint(""/items"", ""GET"")]
    public record GetItemsRequest : IRequest<string>;
}";

            var (diagnostics, output) = RunGenerator(source);

            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("MapGet", output);
            Assert.Contains("/items", output);
            Assert.Contains("[AsParameters]", output);
        }

        [Fact]
        public void DeleteEndpoint_GeneratesMapDelete()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    [Endpoint(""/items"", ""DELETE"")]
    public record DeleteItemRequest(int Id) : IRequest<bool>;
}";

            var (diagnostics, output) = RunGenerator(source);

            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("MapDelete", output);
            Assert.Contains("/items", output);
        }

        [Fact]
        public void PutEndpoint_GeneratesMapPut()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    [Endpoint(""/items"", ""PUT"")]
    public record UpdateItemRequest(int Id, string Name) : IRequest<string>;
}";

            var (diagnostics, output) = RunGenerator(source);

            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("MapPut", output);
        }

        [Fact]
        public void PatchEndpoint_GeneratesMapPatch()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    [Endpoint(""/items"", ""PATCH"")]
    public record PatchItemRequest(int Id, string Name) : IRequest<string>;
}";

            var (diagnostics, output) = RunGenerator(source);

            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("MapPatch", output);
        }

        [Fact]
        public void InvalidHttpMethod_ReportsMM200()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    [Endpoint(""/items"", ""BADVERB"")]
    public record BadRequest : IRequest<string>;
}";

            var (diagnostics, _) = RunGenerator(source);

            var mm200 = diagnostics.Where(d => d.Id == "MM200").ToList();
            Assert.Single(mm200);
            Assert.Equal(DiagnosticSeverity.Error, mm200[0].Severity);
            Assert.Contains("BADVERB", mm200[0].GetMessage());
        }

        [Fact]
        public void NoEndpointAttribute_NoOutput()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    public record PlainRequest : IRequest<string>;
}";

            var (diagnostics, output) = RunGenerator(source);

            Assert.DoesNotContain(diagnostics, d => d.Id == "MM200");
            Assert.Empty(output);
        }

        [Fact]
        public void DefaultMethod_IsPOST()
        {
            var source = @"
using ModernMediator;
using ModernMediator.AspNetCore;

namespace TestApp
{
    [Endpoint(""/items"")]
    public record CreateItemRequest(string Name) : IRequest<string>;
}";

            var (diagnostics, output) = RunGenerator(source);

            Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("MapPost", output);
        }

        #region Test Infrastructure

        private static (ImmutableArray<Diagnostic> Diagnostics, string Output) RunGenerator(string source)
        {
            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            };

            var stubs = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    public interface IRequest<TResponse> { }
    public interface IRequest : IRequest<Unit> { }
    public struct Unit { public static Unit Value => default; }
    public interface IMediator
    {
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    }
}

namespace ModernMediator.AspNetCore
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class EndpointAttribute : Attribute
    {
        public string Route { get; }
        public string Method { get; }
        public EndpointAttribute(string route, string method = ""POST"")
        {
            Route = route;
            Method = method;
        }
    }
}";

            var syntaxTrees = new[]
            {
                CSharpSyntaxTree.ParseText(stubs),
                CSharpSyntaxTree.ParseText(source)
            };

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new EndpointGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation, out _, out var diagnostics);

            var runResult = driver.GetRunResult();
            var generatedSource = runResult.GeneratedTrees
                .FirstOrDefault(t => t.FilePath.EndsWith("MediatorEndpoints.g.cs"))
                ?.GetText()
                .ToString() ?? "";

            return (diagnostics, generatedSource);
        }

        #endregion
    }
}
