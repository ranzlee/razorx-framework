using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RazorX.Framework.Tests.Core;

[TestClass]
public class RxTelemetryTests {
    [TestMethod]
    public void ActivitySource_HasCorrectNameAndVersion() {
        Assert.AreEqual("RazorX.Framework", RxTelemetry.ActivitySource.Name);
        Assert.AreEqual("1.0.0", RxTelemetry.ActivitySource.Version);
    }
    [TestMethod]
    public void Meter_HasCorrectNameAndVersion() {
        Assert.AreEqual("RazorX.Framework", RxTelemetry.Meter.Name);
        Assert.AreEqual("1.0.0", RxTelemetry.Meter.Version);
    }
    [TestMethod]
    public void StartActivity_ReturnsNull_WhenNoListenerRegistered() {
        using var activity = RxTelemetry.ActivitySource.StartActivity("test-span");
        Assert.IsNull(activity, "Activity should be null when no listener is registered");
    }
    [TestMethod]
    public void StartActivity_CreatesActivity_WhenListenerRegistered() {
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == "RazorX.Framework",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = _ => { }
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = RxTelemetry.ActivitySource.StartActivity("test-span");
        Assert.IsNotNull(activity, "Activity should be created when listener is registered");
        Assert.AreEqual("test-span", activity.DisplayName);
        Assert.AreEqual("RazorX.Framework", activity.Source.Name);
    }
    [TestMethod]
    public void StartActivity_WithAttributes_SetsTagsCorrectly() {
        var capturedActivity = default(Activity);
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == "RazorX.Framework",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = RxTelemetry.ActivitySource.StartActivity("razorx.page.render");
        activity?.SetTag("component.root", "App");
        activity?.SetTag("component.page", "HomePage");
        Assert.IsNotNull(capturedActivity);
        Assert.AreEqual("razorx.page.render", capturedActivity.DisplayName);
        var tags = capturedActivity.TagObjects.ToList();
        Assert.IsTrue(tags.Any(t => t.Key == "component.root" && (string?)t.Value == "App"));
        Assert.IsTrue(tags.Any(t => t.Key == "component.page" && (string?)t.Value == "HomePage"));
    }
    [TestMethod]
    public void StartActivity_WithActivityKind_CreatesCorrectKind() {
        var capturedActivity = default(Activity);
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == "RazorX.Framework",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = RxTelemetry.ActivitySource.StartActivity(
            "razorx.sse.transport.publish",
            ActivityKind.Producer);
        Assert.IsNotNull(capturedActivity);
        Assert.AreEqual(ActivityKind.Producer, capturedActivity.Kind);
    }
    [TestMethod]
    public void StartActivity_WithParentContext_LinksToRemoteParent() {
        var capturedActivity = default(Activity);
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == "RazorX.Framework",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);
        var remoteTraceId = ActivityTraceId.CreateRandom();
        var remoteSpanId = ActivitySpanId.CreateRandom();
        var parentContext = new ActivityContext(
            remoteTraceId,
            remoteSpanId,
            ActivityTraceFlags.Recorded,
            isRemote: true
        );
        using var activity = RxTelemetry.ActivitySource.StartActivity(
            "razorx.sse.broadcast.receive",
            ActivityKind.Consumer,
            parentContext);
        Assert.IsNotNull(capturedActivity);
        Assert.AreEqual(remoteTraceId, capturedActivity.TraceId, "TraceId should match remote parent");
        Assert.AreEqual(remoteSpanId, capturedActivity.ParentSpanId, "ParentSpanId should match remote parent");
        Assert.AreEqual(ActivityKind.Consumer, capturedActivity.Kind);
    }
    [TestMethod]
    public void Metrics_AreCreated_WithCorrectNamesAndUnits() {
        Assert.IsNotNull(RxTelemetry.RequestCounter);
        Assert.IsNotNull(RxTelemetry.BroadcastCounter);
        Assert.IsNotNull(RxTelemetry.AntiforgeryValidationCounter);
        Assert.IsNotNull(RxTelemetry.MemoryPoolRentCounter);
        Assert.IsNotNull(RxTelemetry.MemoryPoolReturnCounter);
        Assert.IsNotNull(RxTelemetry.RenderDuration);
        Assert.IsNotNull(RxTelemetry.FragmentCount);
        Assert.IsNotNull(RxTelemetry.BroadcastDuration);
        Assert.IsNotNull(RxTelemetry.BroadcastSubscriberCount);
    }
    [TestMethod]
    public void Counter_Add_DoesNotThrow_WhenNoListenerRegistered() {
        RxTelemetry.RequestCounter.Add(1,
            new KeyValuePair<string, object?>("operation", "page"));
    }
    [TestMethod]
    public void Histogram_Record_DoesNotThrow_WhenNoListenerRegistered() {
        RxTelemetry.RenderDuration.Record(123.45,
            new KeyValuePair<string, object?>("operation", "page"));
        RxTelemetry.FragmentCount.Record(5);
    }
    [TestMethod]
    public void Counter_Add_RecordsMeasurement_WhenListenerRegistered() {
        var measurements = new List<(Instrument Instrument, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = new MeterListener {
            InstrumentPublished = (instrument, listener) => {
                if (instrument.Meter.Name == "RazorX.Framework") {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => {
            measurements.Add((instrument, measurement, tags.ToArray()));
        });
        meterListener.Start();
        RxTelemetry.RequestCounter.Add(1,
            new KeyValuePair<string, object?>("operation", "page"));
        meterListener.RecordObservableInstruments();
        var counterMeasurements = measurements.Where(m => m.Instrument.Name == "razorx.request.count").ToList();
        Assert.IsTrue(counterMeasurements.Count > 0, "Should have recorded at least one measurement");
        var measurement = counterMeasurements[0];
        Assert.AreEqual(1, measurement.Value);
        Assert.IsTrue(measurement.Tags.Any(t => t.Key == "operation" && (string?)t.Value == "page"));
    }
    [TestMethod]
    public void Histogram_Record_RecordsMeasurement_WhenListenerRegistered() {
        var measurements = new List<(Instrument Instrument, double Value)>();
        using var meterListener = new MeterListener {
            InstrumentPublished = (instrument, listener) => {
                if (instrument.Meter.Name == "RazorX.Framework") {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => {
            measurements.Add((instrument, measurement));
        });
        meterListener.Start();
        RxTelemetry.RenderDuration.Record(123.45,
            new KeyValuePair<string, object?>("operation", "page"));
        meterListener.RecordObservableInstruments();
        var histogramMeasurements = measurements.Where(m => m.Instrument.Name == "razorx.render.duration").ToList();
        Assert.IsTrue(histogramMeasurements.Count > 0);
        Assert.AreEqual(123.45, histogramMeasurements[0].Value);
    }
    [TestMethod]
    public void RegisterSseSubscriberCountCallback_EnablesObservableGauge() {
        var callbackInvoked = false;
        var callbackValue = 42;
        RxTelemetry.RegisterSseSubscriberCountCallback("TestModel", () => {
            callbackInvoked = true;
            return callbackValue;
        });
        var measurements = new List<(Instrument Instrument, int Value, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = new MeterListener {
            InstrumentPublished = (instrument, listener) => {
                if (instrument.Meter.Name == "RazorX.Framework") {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) => {
            measurements.Add((instrument, measurement, tags.ToArray()));
        });
        meterListener.Start();
        meterListener.RecordObservableInstruments();
        Assert.IsTrue(callbackInvoked, "Callback should be invoked when observables are recorded");
        var gaugeMeasurements = measurements.Where(m => m.Instrument.Name == "razorx.sse.subscriber.count").ToList();
        Assert.IsTrue(gaugeMeasurements.Count > 0);
        var testModelMeasurement = gaugeMeasurements.FirstOrDefault(m =>
            m.Tags.Any(t => t.Key == "model.type" && (string?)t.Value == "TestModel"));
        Assert.AreEqual(42, testModelMeasurement.Value);
        RxTelemetry.UnregisterSseSubscriberCountCallback("TestModel");
    }
    [TestMethod]
    public void UnregisterSseSubscriberCountCallback_RemovesCallback() {
        RxTelemetry.RegisterSseSubscriberCountCallback("TestModel2", () => 99);
        RxTelemetry.UnregisterSseSubscriberCountCallback("TestModel2");
        var measurements = new List<(Instrument Instrument, int Value, KeyValuePair<string, object?>[] Tags)>();
        using var meterListener = new MeterListener {
            InstrumentPublished = (instrument, listener) => {
                if (instrument.Meter.Name == "RazorX.Framework") {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) => {
            measurements.Add((instrument, measurement, tags.ToArray()));
        });
        meterListener.Start();
        meterListener.RecordObservableInstruments();
        var testModel2Measurements = measurements
            .Where(m => m.Instrument.Name == "razorx.sse.subscriber.count")
            .Where(m => m.Tags.Any(t => t.Key == "model.type" && (string?)t.Value == "TestModel2"))
            .ToList();
        Assert.AreEqual(0, testModel2Measurements.Count, "Unregistered callback should not produce measurements");
    }
}
