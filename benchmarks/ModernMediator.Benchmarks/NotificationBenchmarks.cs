using BenchmarkDotNet.Attributes;

namespace ModernMediator.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(BenchmarkConfig))]
public class NotificationBenchmarks
{
    private IMediator _modernMediator = null!;
    private MediatR.IMediator _mediatR = null!;

    [GlobalSetup]
    public void Setup()
    {
        // ModernMediator — notification handlers registered via Subscribe
        var mmServices = new ServiceCollection();
        mmServices.AddModernMediator(cfg =>
            cfg.RegisterServicesFromAssemblies(typeof(NotificationBenchmarks).Assembly));
        _modernMediator = mmServices.BuildServiceProvider().GetRequiredService<IMediator>();

        // Bridge INotificationHandler instances to the Subscribe-based pub/sub system
        var handler1 = new MmNotificationHandler1();
        var handler2 = new MmNotificationHandler2();
        var handler3 = new MmNotificationHandler3();
        _modernMediator.Subscribe<MmPingNotification>(
            n => handler1.Handle(n, CancellationToken.None), weak: false);
        _modernMediator.Subscribe<MmPingNotification>(
            n => handler2.Handle(n, CancellationToken.None), weak: false);
        _modernMediator.Subscribe<MmPingNotification>(
            n => handler3.Handle(n, CancellationToken.None), weak: false);

        // MediatR — notification handlers auto-registered via DI scanning
        var mtrServices = new ServiceCollection();
        mtrServices.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(NotificationBenchmarks).Assembly));
        _mediatR = mtrServices.BuildServiceProvider()
            .GetRequiredService<MediatR.IMediator>();
    }

    [Benchmark(Baseline = true)]
    public Task MediatR_Publish()
        => _mediatR.Publish(new MtrPingNotification("ping"));

    [Benchmark]
    public bool ModernMediator_Publish()
        => _modernMediator.Publish(new MmPingNotification("ping"));
}
