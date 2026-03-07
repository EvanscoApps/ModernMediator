// ──────────────────────────────────────────────────────────────
// ModernMediator Benchmarks
//
// Must run in Release mode:  dotnet run -c Release
// Results are written to:    BenchmarkDotNet.Artifacts/
//
// MediatR 12.4.1 is the last MIT-licensed version of MediatR.
// This is the fair comparison baseline.
//
// Do not run under the debugger — it disables JIT optimizations
// and produces misleading results.
// ──────────────────────────────────────────────────────────────

using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll();
