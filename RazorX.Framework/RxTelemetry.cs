using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RazorX.Framework;

/// <summary>
/// Low-allocation stopwatch for timing operations without heap allocation.
/// </summary>
/// <remarks>
/// This struct provides high-resolution timing using Stopwatch.GetTimestamp()
/// with zero heap allocations. Elapsed time is calculated on-demand rather than
/// requiring a Stop() call, simplifying usage and preventing forgotten Stop() bugs.
/// This is the same pattern used throughout ASP.NET Core for performance-critical timing.
/// </remarks>
internal readonly struct ValueStopwatch {
    private readonly long _startTimestamp;
    private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;
    public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());
    public TimeSpan GetElapsedTime() {
        var end = Stopwatch.GetTimestamp();
        var tickFrequency = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        var ticks = (long)((end - _startTimestamp) * tickFrequency);
        return new TimeSpan(ticks);
    }
}

/// <summary>
/// Provides OpenTelemetry instrumentation for RazorX.Framework operations.
/// </summary>
/// <remarks>
/// <para>
/// This class defines the ActivitySource and Meter used for tracing and metrics throughout
/// the framework. Instrumentation is always present but only active when an OpenTelemetry
/// listener is registered via the RazorX.Framework.OpenTelemetry package.
/// </para>
/// <para>
/// <strong>Zero Overhead:</strong> When no listener is registered, ActivitySource.StartActivity()
/// and Meter.CreateXxx() calls return null with near-zero overhead (~1-2ns per call).
/// </para>
/// <para>
/// <strong>W3C Trace Context:</strong> TraceId returned by GetCorrelationId() is the same
/// TraceId used by OpenTelemetry spans, ensuring automatic correlation between logs and traces.
/// </para>
/// </remarks>
internal static class RxTelemetry {
    /// <summary>
    /// ActivitySource for distributed tracing spans.
    /// </summary>
    /// <remarks>
    /// Name: "RazorX.Framework"
    /// Version: "1.0.0"
    /// <para>
    /// Register with OpenTelemetry via RazorX.Framework.OpenTelemetry package:
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .WithTracing(tracing => tracing.AddRxInstrumentation());
    /// </code>
    /// </para>
    /// </remarks>
    public static readonly ActivitySource ActivitySource = new("RazorX.Framework", "1.0.0");
    /// <summary>
    /// Meter for metrics collection.
    /// </summary>
    /// <remarks>
    /// Name: "RazorX.Framework"
    /// Version: "1.0.0"
    /// <para>
    /// Register with OpenTelemetry via RazorX.Framework.OpenTelemetry package:
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .WithMetrics(metrics => metrics.AddRxInstrumentation());
    /// </code>
    /// </para>
    /// </remarks>
    public static readonly Meter Meter = new("RazorX.Framework", "1.0.0");
    /// <summary>
    /// Counter for total requests processed by RazorX.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.request.count
    /// Unit: requests
    /// Attributes: operation (page, fragment, sse)
    /// </remarks>
    public static readonly Counter<long> RequestCounter = Meter.CreateCounter<long>(
        "razorx.request.count",
        unit: "requests",
        description: "Total requests processed by RazorX");
    /// <summary>
    /// Counter for total SSE broadcasts sent.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.sse.broadcast.count
    /// Unit: broadcasts
    /// Attributes: model.type, has.metadata, has.transport
    /// </remarks>
    public static readonly Counter<long> BroadcastCounter = Meter.CreateCounter<long>(
        "razorx.sse.broadcast.count",
        unit: "broadcasts",
        description: "Total SSE broadcasts sent");
    /// <summary>
    /// Counter for CSRF validation results.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.antiforgery.validation
    /// Unit: validations
    /// Attributes: result (success, failure)
    /// </remarks>
    public static readonly Counter<long> AntiforgeryValidationCounter = Meter.CreateCounter<long>(
        "razorx.antiforgery.validation",
        unit: "validations",
        description: "CSRF validation results");
    /// <summary>
    /// Counter for memory pool rent operations.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.memory.pool.rent
    /// Unit: operations
    /// Attributes: buffer.size
    /// <para>
    /// WARNING: High volume metric. Disabled by default in RxMetricsOptions.
    /// </para>
    /// </remarks>
    public static readonly Counter<long> MemoryPoolRentCounter = Meter.CreateCounter<long>(
        "razorx.memory.pool.rent",
        unit: "operations",
        description: "Memory pool rent operations");
    /// <summary>
    /// Counter for memory pool return operations.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.memory.pool.return
    /// Unit: operations
    /// Attributes: buffer.size
    /// <para>
    /// WARNING: High volume metric. Disabled by default in RxMetricsOptions.
    /// </para>
    /// </remarks>
    public static readonly Counter<long> MemoryPoolReturnCounter = Meter.CreateCounter<long>(
        "razorx.memory.pool.return",
        unit: "operations",
        description: "Memory pool return operations");
    /// <summary>
    /// Histogram for render operation duration.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.render.duration
    /// Unit: ms
    /// Attributes: operation (page, fragment, response)
    /// </remarks>
    public static readonly Histogram<double> RenderDuration = Meter.CreateHistogram<double>(
        "razorx.render.duration",
        unit: "ms",
        description: "Render operation duration");
    /// <summary>
    /// Histogram for fragments per request.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.fragment.count
    /// Unit: fragments
    /// Attributes: None
    /// </remarks>
    public static readonly Histogram<long> FragmentCount = Meter.CreateHistogram<long>(
        "razorx.fragment.count",
        unit: "fragments",
        description: "Fragments per request");
    /// <summary>
    /// Histogram for SSE broadcast operation duration.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.sse.broadcast.duration
    /// Unit: ms
    /// Attributes: has.transport
    /// </remarks>
    public static readonly Histogram<double> BroadcastDuration = Meter.CreateHistogram<double>(
        "razorx.sse.broadcast.duration",
        unit: "ms",
        description: "SSE broadcast operation duration");
    /// <summary>
    /// Histogram for subscribers per broadcast.
    /// </summary>
    /// <remarks>
    /// Metric: razorx.sse.broadcast.subscriber.count
    /// Unit: subscribers
    /// Attributes: model.type
    /// </remarks>
    public static readonly Histogram<long> BroadcastSubscriberCount = Meter.CreateHistogram<long>(
        "razorx.sse.broadcast.subscriber.count",
        unit: "subscribers",
        description: "Subscribers per broadcast");
    private static readonly Dictionary<string, Func<int>> SseSubscriberCountCallbacks = new();
    private static readonly object SseCallbackLock = new();
    static RxTelemetry() {
        Meter.CreateObservableGauge(
            "razorx.sse.subscriber.count",
            () => {
                lock (SseCallbackLock) {
                    return SseSubscriberCountCallbacks
                        .Select(kvp => new Measurement<int>(
                            kvp.Value(),
                            new KeyValuePair<string, object?>("model.type", kvp.Key)))
                        .ToArray();
                }
            },
            unit: "connections",
            description: "Current SSE subscriber count by model type");
    }
    /// <summary>
    /// Registers a callback to report SSE subscriber count for a specific model type.
    /// </summary>
    /// <param name="modelType">The fully qualified model type name.</param>
    /// <param name="callback">Function that returns the current subscriber count.</param>
    /// <remarks>
    /// Called by RxSseBroadcastService constructor to enable observable gauge metric.
    /// Unregister via UnregisterSseSubscriberCountCallback() in Dispose().
    /// </remarks>
    internal static void RegisterSseSubscriberCountCallback(string modelType, Func<int> callback) {
        lock (SseCallbackLock) {
            SseSubscriberCountCallbacks[modelType] = callback;
        }
    }
    /// <summary>
    /// Unregisters the SSE subscriber count callback for a specific model type.
    /// </summary>
    /// <param name="modelType">The fully qualified model type name.</param>
    /// <remarks>
    /// Called by RxSseBroadcastService Dispose() to clean up metric callback.
    /// </remarks>
    internal static void UnregisterSseSubscriberCountCallback(string modelType) {
        lock (SseCallbackLock) {
            SseSubscriberCountCallbacks.Remove(modelType);
        }
    }
}
