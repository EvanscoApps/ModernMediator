using BenchmarkDotNet.Attributes;

namespace ModernMediator.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class PipelineBenchmarks
{
    private IMediator _modernMediator = null!;
    private MediatR.IMediator _mediatR = null!;

    [GlobalSetup]
    public void Setup()
    {
        // ModernMediator with one pipeline behavior
        var mmServices = new ServiceCollection();
        mmServices.AddModernMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(typeof(PipelineBenchmarks).Assembly);
            cfg.AddOpenBehavior(typeof(MmLoggingBehavior<,>));
        });
        _modernMediator = mmServices.BuildServiceProvider().GetRequiredService<IMediator>();

        // MediatR with one pipeline behavior
        var mtrServices = new ServiceCollection();
        mtrServices.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(PipelineBenchmarks).Assembly);
            cfg.AddOpenBehavior(typeof(MtrLoggingBehavior<,>));
        });
        _mediatR = mtrServices.BuildServiceProvider()
            .GetRequiredService<MediatR.IMediator>();
    }

    [Benchmark(Baseline = true)]
    public Task<MtrPongResponse> MediatR_SendWithBehavior()
        => _mediatR.Send(new MtrPingRequest("ping"));

    [Benchmark]
    public Task<MmPongResponse> ModernMediator_SendWithBehavior()
        => _modernMediator.Send(new MmPingRequest("ping"));
}
