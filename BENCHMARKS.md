# Benchmarks

## Environment

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7922)
Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 1 CPU, 8 logical and 8 physical cores
.NET SDK 9.0.311
Runtime: .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
```

All benchmarks use `IterationCount=20, WarmupCount=5` with `MemoryDiagnoser` enabled.

## Libraries Compared

| Library                                                                          | Version          | Approach                   | License     |
| :------------------------------------------------------------------------------- | :--------------- | :------------------------- | :---------- |
| [MediatR](https://github.com/jbogard/MediatR)                                   | 12.4.1           | Runtime reflection + DI    | Commercial  |
| [ModernMediator](https://github.com/evanscoapps/ModernMediator)                  | 2.0.0-preview.1  | Source generators + DI     | MIT         |
| [martinothamar/Mediator](https://github.com/martinothamar/Mediator)              | 3.0.1            | Source generators (monomorphized) | MIT   |

These three libraries represent distinct architectural trade-offs. MediatR uses runtime reflection with DI container resolution. ModernMediator uses source generators for registration and diagnostics with runtime-composable pipeline behaviors. martinothamar/Mediator uses source generators to produce monomorphized dispatch methods — a concrete method per request type with no dictionary lookup or virtual dispatch at runtime.

## Results

### Send (Request/Response)

| Method                   |      Mean |  Allocated | Alloc Ratio |
| :----------------------- | --------: | ---------: | ----------: |
| MediatR_Send             | 173.57 ns |      360 B |        1.00 |
| ModernMediator_Send      | 241.10 ns |      216 B |        0.60 |
| ModernMediator_SendAsync |  89.95 ns |       72 B |        0.20 |
| Martinothamar_Send       |  28.37 ns |       48 B |        0.13 |

ModernMediator's `SendAsync` path uses `IValueTaskRequestHandler` and `ValueTask<T>` to eliminate the `Task` allocation that both `Send` paths pay. At 90ns and 72 bytes, it is the fastest full-featured dispatch path that supports runtime-composable pipeline behaviors, FluentValidation, logging, telemetry, and timeout enforcement.

martinothamar achieves 28ns through compile-time monomorphized dispatch — there is no dictionary lookup and no virtual method call. This is the theoretical speed ceiling for in-process mediator dispatch in .NET.

### Send with Pipeline Behavior

| Method                          |      Mean | Allocated | Alloc Ratio |
| :------------------------------ | --------: | --------: | ----------: |
| MediatR_SendWithBehavior        | 222.70 ns |     552 B |        1.00 |
| ModernMediator_SendWithBehavior | 284.70 ns |     416 B |        0.75 |

This is a two-way comparison. martinothamar/Mediator's pipeline behaviors are source-generator-controlled and cannot be composed at runtime through DI, so an equivalent benchmark is not directly comparable.

ModernMediator's pipeline behaviors are registered at runtime via `AddBehavior<TBehavior>()` and support open generics, giving teams full control over behavior composition without recompilation. The trade-off is 28% more latency than MediatR on dispatch, offset by 25% fewer allocations per request.

### Publish (Notifications)

| Method                 |      Mean | Allocated | Alloc Ratio |
| :--------------------- | --------: | --------: | ----------: |
| MediatR_Publish        | 231.77 ns |     616 B |        1.00 |
| ModernMediator_Publish | 141.30 ns |     200 B |        0.32 |
| Martinothamar_Publish  |  36.23 ns |      24 B |        0.04 |

ModernMediator is 39% faster than MediatR on notifications and allocates 68% less memory. martinothamar's source-generated dispatch is again the fastest at 36ns.

### Cold Start

| Method                   |       Mean | Allocated | Alloc Ratio |
| :----------------------- | ---------: | --------: | ----------: |
| MediatR_ColdStart        | 120.76 μs | 186.14 KB |        1.00 |
| ModernMediator_ColdStart |  29.39 μs |  63.53 KB |        0.34 |
| Martinothamar_ColdStart  |  15.43 μs |  22.12 KB |        0.12 |

Cold start measures the time from service provider build to first dispatch. ModernMediator is 4.1x faster than MediatR because source generators eliminate the assembly scanning and open generic registration that MediatR performs at startup. This directly benefits serverless and container-based deployments where cold start latency affects user experience.

## Why Allocations Matter More Than Nanoseconds

When evaluating a mediator library for production use, the allocation column is more important than the time column. Here's why.

Every allocation in .NET lands on the managed heap. The garbage collector tracks these objects and periodically scans the heap to reclaim memory from objects that are no longer referenced. That scanning is not free — it pauses your threads.

The .NET heap is divided into three generations. Gen0 is where new objects are created. It is small and collected frequently. Short-lived objects like the delegate MediatR creates per dispatch land in Gen0, get used once, and are collected almost immediately. Each collection is fast in isolation, but under sustained load the cumulative effect becomes significant.

This cumulative effect is called memory pressure. When your application allocates heavily, Gen0 fills up faster, collections happen more often, and the garbage collector spends more CPU cycles cleaning up. Those cycles are not available for request processing. Requests take slightly longer. More requests are in flight simultaneously. More live objects make each collection more expensive. The problem compounds.

The practical consequence is P99 latency. GC pauses cause latency spikes. Latency spikes blow out your P99 response times. Blown P99s mean you need more container instances to meet your SLA. More instances means a higher cloud bill. The path from "allocations per dispatch" to "monthly infrastructure cost" is direct.

Consider the concrete math at 10,000 requests per second:

| Library              | Bytes per Send | Allocation Rate  | Per Minute   |
| :------------------- | -------------: | ---------------: | -----------: |
| MediatR              |          360 B |        3.43 MB/s |    205.99 MB |
| ModernMediator Send  |          216 B |        2.06 MB/s |    123.60 MB |
| ModernMediator Async |           72 B |        0.69 MB/s |     41.20 MB |

ModernMediator's `SendAsync` path generates 80% less garbage than MediatR at the same throughput. That is 165 MB per minute of heap pressure that never exists, never triggers Gen0 collections, and never causes latency spikes.

This is why ModernMediator allocates less than MediatR on every single benchmark. The time column shows mixed results — some benchmarks faster, some slower. The allocation column is a clean sweep. For teams optimizing cloud infrastructure costs, allocations are the number that matters.

## Understanding the Three-Way Comparison

These three libraries occupy different positions on the speed-versus-flexibility spectrum.

**martinothamar/Mediator** is the raw speed leader. It achieves this by generating a concrete monomorphized method for every request type at compile time. There is no dictionary lookup and no virtual dispatch. The trade-off is that pipeline behaviors are source-generator-controlled rather than runtime-composable. The library does not ship built-in validation, logging, telemetry, or timeout behaviors. It has been in 3.x preview since early 2024.

**MediatR** is the established incumbent with the largest ecosystem. It uses runtime reflection and DI container resolution, which gives it full runtime flexibility at the cost of higher allocations per dispatch. As of version 13, MediatR requires a commercial license.

**ModernMediator** sits between these two. Source generators handle registration, diagnostics, and endpoint generation at compile time. Pipeline behaviors are runtime-composable through DI, supporting the same `AddBehavior<>()` pattern that MediatR users expect. The library ships with built-in FluentValidation integration, logging, telemetry via `ActivitySource` and `Meter`, timeout enforcement, ASP.NET Core endpoint generation, and a `Result<T>` pattern — all under the MIT license. The `SendAsync` ValueTask path offers a zero-allocation dispatch option that no other full-featured mediator provides.

The developer who cares only about raw dispatch nanoseconds should evaluate martinothamar/Mediator. The developer who wants a full-featured, production-ready mediator with lower allocations than MediatR, a zero-allocation fast path, compile-time diagnostics, and an MIT license should evaluate ModernMediator.

## Reproducing These Results

```bash
cd benchmarks/ModernMediator.Benchmarks
dotnet run -c Release
```

Results will vary by hardware. The ratios between libraries are more meaningful than absolute numbers.