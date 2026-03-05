using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ModernMediator.Tests
{
    public class TelemetryTests : IDisposable
    {
        private readonly ActivityListener _activityListener;
        private readonly MeterListener _meterListener;
        private readonly List<Activity> _activities = new();
        private readonly List<(string Name, long Value)> _counterMeasurements = new();
        private readonly List<(string Name, double Value)> _histogramMeasurements = new();

        public TelemetryTests()
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == MediatorTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity => _activities.Add(activity)
            };
            ActivitySource.AddActivityListener(_activityListener);

            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MediatorTelemetry.SourceName)
                    listener.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            {
                _counterMeasurements.Add((instrument.Name, measurement));
            });
            _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
            {
                _histogramMeasurements.Add((instrument.Name, measurement));
            });
            _meterListener.Start();
        }

        public void Dispose()
        {
            _activityListener.Dispose();
            _meterListener.Dispose();
        }

        [Fact]
        public void ActivitySource_Name_IsModernMediator()
        {
            Assert.Equal("ModernMediator", MediatorTelemetry.ActivitySource.Name);
        }

        [Fact]
        public void Meter_Name_IsModernMediator()
        {
            Assert.Equal("ModernMediator", MediatorTelemetry.Meter.Name);
        }

        [Fact]
        public void RequestCounter_Increments_WhenRequestDispatched()
        {
            // Simulate generated dispatch code
            MediatorTelemetry.RequestCounter.Add(1);
            _meterListener.RecordObservableInstruments();

            Assert.Contains(_counterMeasurements, m => m.Name == "modernmediator.requests" && m.Value == 1);
        }

        [Fact]
        public void RequestDuration_RecordsPositiveValue_AfterDispatch()
        {
            // Simulate generated dispatch code
            var sw = Stopwatch.StartNew();
            Thread.Sleep(5);
            sw.Stop();
            MediatorTelemetry.RequestDuration.Record(sw.Elapsed.TotalMilliseconds);
            _meterListener.RecordObservableInstruments();

            Assert.Contains(_histogramMeasurements, m => m.Name == "modernmediator.request_duration" && m.Value > 0);
        }

        [Fact]
        public void ActivityListener_ObservesActivityName_MatchingRequestTypeName()
        {
            // Simulate generated dispatch code: activity name is typeof(TRequest).Name
            using var activity = MediatorTelemetry.ActivitySource.StartActivity("TelemetryTestRequest");

            Assert.NotNull(activity);
            Assert.Contains(_activities, a => a.OperationName == "TelemetryTestRequest");
        }

        [Fact]
        public void ExceptionPath_SetsActivityStatusToError_AndPropagates()
        {
            Activity? capturedActivity = null;
            var expectedException = new InvalidOperationException("Handler failed");

            // Simulate generated dispatch code with exception
            Action throwingAction = () =>
            {
                var activity = MediatorTelemetry.ActivitySource.StartActivity("FailingRequest");
                var sw = Stopwatch.StartNew();
                try
                {
                    MediatorTelemetry.RequestCounter.Add(1);
                    throw expectedException;
                }
                catch (Exception caughtEx)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, caughtEx.Message);
                    capturedActivity = activity;
                    throw;
                }
                finally
                {
                    sw.Stop();
                    MediatorTelemetry.RequestDuration.Record(sw.Elapsed.TotalMilliseconds);
                    activity?.Dispose();
                }
            };
            var ex = Assert.Throws<InvalidOperationException>(throwingAction);

            Assert.Same(expectedException, ex);
            Assert.NotNull(capturedActivity);
            Assert.Equal(ActivityStatusCode.Error, capturedActivity!.Status);
            Assert.Equal("Handler failed", capturedActivity.StatusDescription);
        }

        [Fact]
        public void AddTelemetry_WithCustomConfigure_AppliesOptions()
        {
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.AddTelemetry(opts =>
                {
                    opts.Enabled = false;
                });
            });

            var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<TelemetryOptions>();
            Assert.False(options.Enabled);
        }

        [Fact]
        public void AddTelemetry_WithDefaultOptions_EnabledIsTrue()
        {
            var services = new ServiceCollection();
            services.AddModernMediator(config =>
            {
                config.AddTelemetry();
            });

            var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<TelemetryOptions>();
            Assert.True(options.Enabled);
        }
    }
}
