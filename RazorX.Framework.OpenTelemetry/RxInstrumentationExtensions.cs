using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace RazorX.Framework.OpenTelemetry;

/// <summary>
/// Extension methods for adding RazorX.Framework instrumentation to OpenTelemetry.
/// </summary>
/// <remarks>
/// <para>
/// These extensions register RazorX.Framework's ActivitySource and Meter with OpenTelemetry,
/// enabling distributed tracing and metrics collection for RazorX operations.
/// </para>
/// <para>
/// Instrumentation is always present in RazorX.Framework (using System.Diagnostics.DiagnosticSource)
/// but only active when these extensions are called to register listeners.
/// </para>
/// </remarks>
public static class RxInstrumentationExtensions {
    /// <summary>
    /// Adds RazorX.Framework tracing instrumentation to the OpenTelemetry TracerProvider.
    /// </summary>
    /// <param name="builder">The TracerProviderBuilder to configure.</param>
    /// <param name="configure">Optional configuration action for tracing options.</param>
    /// <returns>The TracerProviderBuilder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension registers the RazorX.Framework ActivitySource with OpenTelemetry,
    /// enabling distributed tracing for:
    /// - Page renders (razorx.page.render)
    /// - Response builds (razorx.response.build)
    /// - SSE broadcasts (razorx.sse.broadcast)
    /// - SSE transport operations (razorx.sse.transport.publish, razorx.sse.broadcast.receive)
    /// - SSE streams (razorx.sse.stream)
    /// </para>
    /// <para>
    /// All spans automatically inherit the W3C TraceId from Activity.Current, ensuring
    /// correlation between RazorX operations, ASP.NET Core requests, and application logs.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .WithTracing(tracing => tracing
    ///         .AddRazorXInstrumentation()
    ///         .AddAspNetCoreInstrumentation()
    ///         .AddOtlpExporter());
    /// </code>
    /// </example>
    public static TracerProviderBuilder AddRazorXInstrumentation(
        this TracerProviderBuilder builder,
        Action<RxTracingOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new RxTracingOptions();
        configure?.Invoke(options);
        builder.AddSource("RazorX.Framework");
        builder.ConfigureServices(services => {
            services.AddSingleton(options);
        });
        return builder;
    }
    /// <summary>
    /// Adds RazorX.Framework metrics instrumentation to the OpenTelemetry MeterProvider.
    /// </summary>
    /// <param name="builder">The MeterProviderBuilder to configure.</param>
    /// <param name="configure">Optional configuration action for metrics options.</param>
    /// <returns>The MeterProviderBuilder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension registers the RazorX.Framework Meter with OpenTelemetry,
    /// enabling metrics collection for:
    /// </para>
    /// <para>
    /// <strong>Counters:</strong>
    /// - razorx.request.count (requests by operation type)
    /// - razorx.sse.broadcast.count (broadcasts by model type)
    /// - razorx.antiforgery.validation (success/failure)
    /// - razorx.memory.pool.rent (buffer allocations - disabled by default)
    /// - razorx.memory.pool.return (buffer returns - disabled by default)
    /// </para>
    /// <para>
    /// <strong>Histograms:</strong>
    /// - razorx.render.duration (render times by operation)
    /// - razorx.fragment.count (fragments per request)
    /// - razorx.sse.broadcast.duration (broadcast latency)
    /// - razorx.sse.broadcast.subscriber.count (subscribers per broadcast)
    /// </para>
    /// <para>
    /// <strong>Gauges:</strong>
    /// - razorx.sse.subscriber.count (current subscribers by model type)
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .WithMetrics(metrics => metrics
    ///         .AddRazorXInstrumentation(options => {
    ///             options.EnableMemoryPoolMetrics = false; // High volume
    ///         })
    ///         .AddAspNetCoreInstrumentation()
    ///         .AddOtlpExporter());
    /// </code>
    /// </example>
    public static MeterProviderBuilder AddRazorXInstrumentation(
        this MeterProviderBuilder builder,
        Action<RxMetricsOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new RxMetricsOptions();
        configure?.Invoke(options);
        builder.AddMeter("RazorX.Framework");
        builder.ConfigureServices(services => {
            services.AddSingleton(options);
        });
        return builder;
    }
}
