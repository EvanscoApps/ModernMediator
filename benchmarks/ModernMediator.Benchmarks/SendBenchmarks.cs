using BenchmarkDotNet.Attributes;

namespace ModernMediator.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class SendBenchmarks
{
    private IMediator _modernMediator = null!;
    private MediatR.IMediator _mediatR = null!;

    [GlobalSetup]
    public void Setup()
    {
        // ModernMediator setup
        var mmServices = new ServiceCollection();
        mmServices.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblies(typeof(SendBenchmarks).Assembly));
        _modernMediator = mmServices.BuildServiceProvider().GetRequiredService<IMediator>();

        // MediatR setup
        var mtrServices = new ServiceCollection();
        mtrServices.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SendBenchmarks).Assembly));
        _mediatR = mtrServices.BuildServiceProvider()
            .GetRequiredService<MediatR.IMediator>();
    }

    [Benchmark(Baseline = true)]
    public Task<MtrPongResponse> MediatR_Send()
        => _mediatR.Send(new MtrPingRequest("ping"));

    [Benchmark]
    public Task<MmPongResponse> ModernMediator_Send()
        => _modernMediator.Send(new MmPingRequest("ping"));
}
