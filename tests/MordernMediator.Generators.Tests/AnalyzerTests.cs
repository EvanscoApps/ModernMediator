using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace ModernMediator.Generators.Tests
{
    /// <summary>
    /// Tests for DiagnosticAnalyzer-based diagnostics (as opposed to IIncrementalGenerator diagnostics).
    /// Uses CompilationWithAnalyzers instead of CSharpGeneratorDriver.
    /// </summary>
    public class AnalyzerTests
    {
        #region MM008 — WeakLambdaSubscription: Positive Tests

        [Fact]
        public async Task MM008_LambdaWithDefaultWeak_ReportsWarning()
        {
            var source = @"
using System;
using ModernMediator;

namespace TestApp
{
    public class TestClass
    {
        public void Test(IMediator mediator)
        {
            mediator.Subscribe<int>(x => { });
        }
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            AssertDiagnosticPresent(diagnostics, "MM008", DiagnosticSeverity.Warning);
        }

        [Fact]
        public async Task MM008_LambdaWithExplicitWeakTrue_ReportsWarning()
        {
            var source = @"
using System;
using ModernMediator;

namespace TestApp
{
    public class TestClass
    {
        public void Test(IMediator mediator)
        {
            mediator.Subscribe<int>(x => { }, weak: true);
        }
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            AssertDiagnosticPresent(diagnostics, "MM008", DiagnosticSeverity.Warning);
        }

        [Fact]
        public async Task MM008_SubscribeAsyncLambdaWithDefaultWeak_ReportsWarning()
        {
            var source = @"
using System;
using System.Threading.Tasks;
using ModernMediator;

namespace TestApp
{
    public class TestClass
    {
        public void Test(IMediator mediator)
        {
            mediator.SubscribeAsync<int>(x => Task.CompletedTask);
        }
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            AssertDiagnosticPresent(diagnostics, "MM008", DiagnosticSeverity.Warning);
        }

        #endregion

        #region MM008 — WeakLambdaSubscription: Negative Tests

        [Fact]
        public async Task MM008_LambdaWithExplicitWeakFalse_NoDiagnostic()
        {
            var source = @"
using System;
using ModernMediator;

namespace TestApp
{
    public class TestClass
    {
        public void Test(IMediator mediator)
        {
            mediator.Subscribe<int>(x => { }, weak: false);
        }
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            AssertNoDiagnostic(diagnostics, "MM008");
        }

        [Fact]
        public async Task MM008_MethodGroup_DefaultWeak_NoDiagnostic()
        {
            var source = @"
using System;
using ModernMediator;

namespace TestApp
{
    public class TestClass
    {
        public void Test(IMediator mediator)
        {
            mediator.Subscribe<int>(HandleMessage);
        }

        private void HandleMessage(int message) { }
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            AssertNoDiagnostic(diagnostics, "MM008");
        }

        [Fact]
        public async Task MM008_MethodGroup_ExplicitWeakTrue_NoDiagnostic()
        {
            var source = @"
using System;
using ModernMediator;

namespace TestApp
{
    public class TestClass
    {
        public void Test(IMediator mediator)
        {
            mediator.Subscribe<int>(HandleMessage, weak: true);
        }

        private void HandleMessage(int message) { }
    }
}";

            var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

            AssertNoDiagnostic(diagnostics, "MM008");
        }

        #endregion

        #region Test Infrastructure

        /// <summary>
        /// Compiles the given source alongside ModernMediator interface stubs (with Subscribe methods),
        /// runs the WeakLambdaSubscriptionAnalyzer, and returns analyzer-produced diagnostics.
        /// This is separate from the generator test infrastructure which uses CSharpGeneratorDriver.
        /// </summary>
        private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(params string[] sources)
        {
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            };

            // Add System.Runtime for core types like Predicate<T>, Action<T>, Func<T,TResult>
            var systemRuntimePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                "System.Runtime.dll");
            if (System.IO.File.Exists(systemRuntimePath))
                references.Add(MetadataReference.CreateFromFile(systemRuntimePath));

            var mediatorInterfaces = @"
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernMediator
{
    public interface IMediator
    {
        IDisposable Subscribe<T>(Action<T> handler, bool weak = true, Predicate<T>? filter = null);
        IDisposable SubscribeAsync<T>(Func<T, Task> handler, bool weak = true, Predicate<T>? filter = null);
        IDisposable SubscribeOnMainThread<T>(Action<T> handler, bool weak = true, Predicate<T>? filter = null);
        IDisposable SubscribeAsyncOnMainThread<T>(Func<T, Task> handler, bool weak = true, Predicate<T>? filter = null);
        IDisposable Subscribe<T>(string key, Action<T> handler, bool weak = true, Predicate<T>? filter = null);
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

            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new WeakLambdaSubscriptionAnalyzer());
            var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

            var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(CancellationToken.None);
            return diagnostics;
        }

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

        private static void AssertNoDiagnostic(ImmutableArray<Diagnostic> diagnostics, string id)
        {
            Assert.DoesNotContain(diagnostics, d => d.Id == id);
        }

        #endregion
    }
}
