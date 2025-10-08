using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RazorX.Framework;

/// <summary>
/// Extension methods for registering RxSseBroadcastService with dependency injection.
/// </summary>
public static class RxSseBroadcastExtensions {
    /// <summary>
    /// Registers RxSseBroadcastService with AOT-compatible JSON serialization.
    /// </summary>
    /// <typeparam name="T">The model type to broadcast.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="modelTypeInfo">Source-generated JsonTypeInfo for the model type (required for AOT).</param>
    /// <param name="configureTransport">Optional transport configuration delegate.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension method simplifies registration of RxSseBroadcastService with proper AOT support.
    /// </para>
    /// <para>
    /// <strong>In-Memory Mode (Default):</strong> If configureTransport is null or calls UseInMemory(),
    /// the service operates in single-server mode with no distributed transport.
    /// </para>
    /// <para>
    /// <strong>Distributed Mode:</strong> When transport is configured (UseRedis, UseServiceBus, etc.),
    /// broadcasts are automatically distributed across all servers.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In-memory mode (single server)
    /// builder.Services.AddRxSseBroadcast(
    ///     MyAppJsonContext.Default.TodoModel);
    ///
    /// // Redis distributed mode
    /// builder.Services.AddRxSseBroadcast(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     options => options.UseRedis("redis-connection-string"));
    ///
    /// // Service Bus distributed mode
    /// builder.Services.AddRxSseBroadcast(
    ///     MyAppJsonContext.Default.NotificationModel,
    ///     options => options.UseServiceBus("servicebus-connection-string"));
    /// </code>
    /// </example>
    public static IServiceCollection AddRxSseBroadcast<T>(
        this IServiceCollection services,
        JsonTypeInfo<T> modelTypeInfo,
        Action<RxBroadcastTransportOptions>? configureTransport = null)
    {
        ArgumentNullException.ThrowIfNull(modelTypeInfo, nameof(modelTypeInfo));

        var options = new RxBroadcastTransportOptions();
        configureTransport?.Invoke(options);

        // Register transport if configured
        if (options.TransportFactory != null) {
            services.TryAddSingleton(sp => options.TransportFactory(sp));
        }

        // Register broadcast service with dependencies
        services.AddSingleton(sp => {
            var transport = sp.GetService<IRxBroadcastTransport>();
            var config = sp.GetService<IConfiguration>();
            return new RxSseBroadcastService<T>(transport, modelTypeInfo, config);
        });

        return services;
    }
}

/// <summary>
/// Configuration options for distributed broadcast transport.
/// </summary>
public class RxBroadcastTransportOptions {
    internal Func<IServiceProvider, IRxBroadcastTransport>? TransportFactory { get; private set; }

    /// <summary>
    /// Configures the service to use in-memory mode only (no distributed transport).
    /// </summary>
    /// <remarks>
    /// This is the default behavior if no transport is configured.
    /// Broadcasts will only reach subscribers on the local server.
    /// </remarks>
    public void UseInMemory() {
        TransportFactory = null;
    }

    /// <summary>
    /// Configures a custom transport implementation.
    /// </summary>
    /// <param name="factory">Factory function to create the transport instance.</param>
    /// <remarks>
    /// Use this method to integrate custom transport implementations.
    /// The factory receives the IServiceProvider for dependency resolution.
    /// </remarks>
    /// <example>
    /// <code>
    /// options.UseCustomTransport(sp => {
    ///     var config = sp.GetRequiredService&lt;IConfiguration&gt;();
    ///     var logger = sp.GetRequiredService&lt;ILogger&lt;MyTransport&gt;&gt;();
    ///     return new MyTransport(config, logger);
    /// });
    /// </code>
    /// </example>
    public void UseCustomTransport(Func<IServiceProvider, IRxBroadcastTransport> factory) {
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));
        TransportFactory = factory;
    }

    // Note: UseRedis() and UseServiceBus() will be implemented in separate NuGet packages
    // (RazorX.Framework.Redis and RazorX.Framework.Azure) to avoid mandatory dependencies
}
