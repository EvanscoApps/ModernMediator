using BenchmarkDotNet.Attributes;

namespace ModernMediator.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class ColdStartBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task MediatR_ColdStart()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ColdStartBenchmarks).Assembly));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<MediatR.IMediator>();
        await mediator.Send(new MtrPingRequest("ping"));
    }

    [Benchmark]
    public async Task ModernMediator_ColdStart()
    {
        var services = new ServiceCollection();
        services.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblies(typeof(ColdStartBenchmarks).Assembly));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        await mediator.Send(new MmPingRequest("ping"));
    }
}
