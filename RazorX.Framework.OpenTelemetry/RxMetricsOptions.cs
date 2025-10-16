namespace RazorX.Framework.OpenTelemetry;

/// <summary>
/// Configuration options for RazorX.Framework metrics instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// These options control which OpenTelemetry metrics are collected by RazorX.Framework.
/// Options default to sensible values balancing observability with performance.
/// </para>
/// <para>
/// <strong>Cardinality Considerations:</strong> Some metrics can generate high cardinality
/// (many unique time series) in production. Memory pool metrics are disabled by default
/// due to high volume. Enable only for short-term diagnostics.
/// </para>
/// </remarks>
public sealed class RxMetricsOptions {
    /// <summary>
    /// Enable metrics for render operations (duration, count).
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Metrics enabled:
    /// - razorx.request.count (counter, attributes: operation)
    /// - razorx.render.duration (histogram, attributes: operation)
    /// - razorx.fragment.count (histogram)
    /// </para>
    /// <para>
    /// Tracks page renders, fragment updates, and response builds.
    /// Essential for understanding RazorX request patterns and performance.
    /// </para>
    /// </remarks>
    public bool EnableRenderMetrics { get; set; } = true;
    /// <summary>
    /// Enable metrics for SSE operations (subscriber count, broadcast count/duration).
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Metrics enabled:
    /// - razorx.sse.subscriber.count (observable gauge, attributes: model.type)
    /// - razorx.sse.broadcast.count (counter, attributes: model.type, has.metadata, has.transport)
    /// - razorx.sse.broadcast.duration (histogram, attributes: has.transport)
    /// - razorx.sse.broadcast.subscriber.count (histogram, attributes: model.type)
    /// </para>
    /// <para>
    /// Tracks SSE connection health, broadcast patterns, and distribution efficiency.
    /// Critical for monitoring real-time features.
    /// </para>
    /// </remarks>
    public bool EnableSseMetrics { get; set; } = true;
    /// <summary>
    /// Enable metrics for memory pool operations (rent/return count).
    /// </summary>
    /// <remarks>
    /// <para>Default: false</para>
    /// <para>
    /// Metrics enabled:
    /// - razorx.memory.pool.rent (counter, attributes: buffer.size)
    /// - razorx.memory.pool.return (counter, attributes: buffer.size)
    /// </para>
    /// <para>
    /// <strong>WARNING: HIGH VOLUME</strong> - Memory pool operations can occur thousands
    /// of times per second under load. Recording every operation significantly impacts
    /// metric storage and query performance.
    /// </para>
    /// <para>
    /// Only enable for short-term diagnostics when investigating memory issues.
    /// Consider sampling if enabling in production.
    /// </para>
    /// </remarks>
    public bool EnableMemoryPoolMetrics { get; set; } = false;
    /// <summary>
    /// Enable metrics for antiforgery validation operations.
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Metrics enabled:
    /// - razorx.antiforgery.validation (counter, attributes: result)
    /// </para>
    /// <para>
    /// Tracks CSRF validation success/failure rates.
    /// Useful for security monitoring and detecting potential attacks.
    /// </para>
    /// </remarks>
    public bool EnableAntiforgeryMetrics { get; set; } = true;
    /// <summary>
    /// Enable histogram metrics (can be expensive in high-volume scenarios).
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// When disabled, only counters and gauges are recorded.
    /// Histograms require bucketing and aggregation, which can be expensive at scale.
    /// </para>
    /// <para>
    /// Affected metrics:
    /// - razorx.render.duration
    /// - razorx.fragment.count
    /// - razorx.sse.broadcast.duration
    /// - razorx.sse.broadcast.subscriber.count
    /// </para>
    /// <para>
    /// Histograms provide percentile analysis (P50, P95, P99) essential for SLO tracking.
    /// Disable only if metric volume is overwhelming your backend.
    /// </para>
    /// </remarks>
    public bool EnableHistograms { get; set; } = true;
}
