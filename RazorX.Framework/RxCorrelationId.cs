using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace RazorX.Framework;

/// <summary>
/// Middleware that manages correlation IDs for request tracking across distributed systems.
/// </summary>
/// <remarks>
/// This middleware generates or extracts a unique correlation ID for each request,
/// making it available throughout the request pipeline for logging and tracing.
/// The ID is included in response headers for client-side correlation.
/// </remarks>
public sealed class RxCorrelationIdMiddleware(RequestDelegate next, ILogger<RxCorrelationIdMiddleware>? logger = null) {
    private const string CorrelationIdKey = "RxCorrelationId";
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string RequestIdHeader = "X-Request-Id";
    private readonly RequestDelegate next = next;
    private readonly ILogger<RxCorrelationIdMiddleware>? logger = logger;

    /// <summary>
    /// Processes the HTTP request to set up correlation ID tracking.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context) {
        var correlationId = GetOrCreateCorrelationId(context);
        // Store in HttpContext.Items for access throughout the request
        context.Items[CorrelationIdKey] = correlationId;
        // Add to response headers for client tracking
        context.Response.OnStarting(() => {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader)) {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            }
            return Task.CompletedTask;
        });
        // Set up Activity for distributed tracing integration
        using var activity = Activity.Current?.AddBaggage(CorrelationIdKey, correlationId);
        // Add to logging scope
        using var scope = logger?.BeginScope(new Dictionary<string, object> {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.ToString(),
            ["RequestMethod"] = context.Request.Method
        });
        if (logger?.IsEnabled(LogLevel.Debug) == true) {
            logger.LogDebug(
                "Processing request with CorrelationId: {CorrelationId}, Method: {Method}, Path: {Path}",
                correlationId,
                context.Request.Method,
                context.Request.Path);
        }
        try {
            await next(context);
        }
        finally {
            if (logger?.IsEnabled(LogLevel.Debug) == true) {
                logger.LogDebug(
                    "Completed request with CorrelationId: {CorrelationId}, Status: {StatusCode}",
                    correlationId,
                    context.Response.StatusCode);
            }
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context) {
        // Try to get from standard headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId)) {
            return correlationId.ToString();
        }
        if (context.Request.Headers.TryGetValue(RequestIdHeader, out var requestId) &&
            !string.IsNullOrWhiteSpace(requestId)) {
            return requestId.ToString();
        }
        // Try W3C Trace Context (if available)
        if (Activity.Current?.Id != null) {
            // Extract the trace ID portion (first 32 chars after version)
            var traceId = Activity.Current.TraceId.ToString();
            if (!string.IsNullOrWhiteSpace(traceId)) {
                return traceId;
            }
        }
        // Generate new ID
        var newId = Guid.NewGuid().ToString();
        if (logger?.IsEnabled(LogLevel.Debug) == true) {
            logger.LogDebug("Generated new CorrelationId: {CorrelationId}", newId);
        }
        return newId;
    }
}

/// <summary>
/// Extension methods for correlation ID functionality.
/// </summary>
public static class RxCorrelationIdExtensions {
    private const string CorrelationIdKey = "RxCorrelationId";

    /// <summary>
    /// Adds the correlation ID middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for method chaining.</returns>
    /// <remarks>
    /// This middleware should be added early in the pipeline to ensure
    /// correlation IDs are available for all subsequent middleware and handlers.
    /// </remarks>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseRxCorrelationId();  // Add early in pipeline
    /// app.UseRxAntiforgeryCookie();
    /// app.MapRoutes();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseRxCorrelationId(this IApplicationBuilder app) {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<RxCorrelationIdMiddleware>();
    }

    /// <summary>
    /// Gets the correlation ID for the current request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The correlation ID, or null if not available.</returns>
    /// <remarks>
    /// Returns the correlation ID that was generated or extracted by the middleware.
    /// Will return null if the middleware hasn't run or isn't configured.
    /// </remarks>
    public static string? GetCorrelationId(this HttpContext context) {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(CorrelationIdKey, out var id)
            ? id?.ToString()
            : null;
    }

    /// <summary>
    /// Sets a custom correlation ID for the current request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="correlationId">The correlation ID to set.</param>
    /// <remarks>
    /// This method allows manually setting a correlation ID, overriding
    /// any value set by the middleware. Useful for testing or custom scenarios.
    /// </remarks>
    public static void SetCorrelationId(this HttpContext context, string correlationId) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        context.Items[CorrelationIdKey] = correlationId;
    }

    /// <summary>
    /// Creates a logging scope with the correlation ID included.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="context">The HTTP context containing the correlation ID.</param>
    /// <returns>A disposable logging scope, or null if no correlation ID is available.</returns>
    /// <remarks>
    /// Use this to include correlation ID in log entries without the middleware.
    /// Useful for background tasks or non-HTTP contexts.
    /// </remarks>
    public static IDisposable? BeginCorrelationScope(this ILogger logger, HttpContext context) {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);
        var correlationId = context.GetCorrelationId();
        if (correlationId == null) {
            return null;
        }
        return logger.BeginScope(new Dictionary<string, object> {
            ["CorrelationId"] = correlationId
        });
    }
}