using BenchmarkDotNet.Attributes;

namespace ModernMediator.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class SendBenchmarks
{
    private IMediator _modernMediator = null!;
    private MediatR.IMediator _mediatR = null!;
    private global::Mediator.IMediator _sgMediator = null!;

    private ISender _modernSender = null!;

    [GlobalSetup]
    public void Setup()
    {
        // ModernMediator setup
        var mmServices = new ServiceCollection();
        mmServices.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblies(typeof(SendBenchmarks).Assembly));
        var mmProvider = mmServices.BuildServiceProvider();
        _modernMediator = mmProvider.GetRequiredService<IMediator>();
        _modernSender = mmProvider.GetRequiredService<ISender>();

        // MediatR setup
        var mtrServices = new ServiceCollection();
        mtrServices.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SendBenchmarks).Assembly));
        _mediatR = mtrServices.BuildServiceProvider()
            .GetRequiredService<MediatR.IMediator>();

        // martinothamar/Mediator (source-generated) setup
        var sgServices = new ServiceCollection();
        sgServices.AddMediator();
        _sgMediator = sgServices.BuildServiceProvider()
            .GetRequiredService<global::Mediator.IMediator>();
    }

    [Benchmark(Baseline = true)]
    public Task<MtrPongResponse> MediatR_Send()
        => _mediatR.Send(new MtrPingRequest("ping"));

    [Benchmark]
    public Task<MmPongResponse> ModernMediator_Send()
        => _modernMediator.Send(new MmPingRequest("ping"));

    [Benchmark]
    public ValueTask<MmPongResponse> ModernMediator_SendAsync()
        => _modernSender.SendAsync(new MmPingRequest("ping"));

    [Benchmark]
    public ValueTask<SgPongResponse> Martinothamar_Send()
        => _sgMediator.Send(new SgPingRequest("ping"));
}
