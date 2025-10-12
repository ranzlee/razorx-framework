using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Extension methods for registering RxSseBroadcastService with dependency injection.
/// </summary>
internal static class RxSseBroadcastExtensions {
    /// <summary>
    /// Registers RxSseBroadcastService with AOT-compatible JSON serialization and subscription-time filtering.
    /// </summary>
    /// <typeparam name="TModel">The model type to broadcast.</typeparam>
    /// <typeparam name="TMetadata">The metadata type that can be sent with broadcasts.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="modelTypeInfo">Source-generated JsonTypeInfo for the model type (required for AOT).</param>
    /// <param name="metadataTypeInfo">Source-generated JsonTypeInfo for the metadata type (required for AOT with distributed transport).</param>
    /// <param name="configureTransport">Optional transport configuration delegate.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension method simplifies registration of RxSseBroadcastService with proper AOT support
    /// and subscription-time filtering capabilities.
    /// </para>
    /// <para>
    /// <strong>In-Memory Mode (Default):</strong> If configureTransport is null or calls UseInMemory(),
    /// the service operates in single-server mode with no distributed transport.
    /// Subscription-time filtering works perfectly in this mode.
    /// </para>
    /// <para>
    /// <strong>Distributed Mode:</strong> When transport is configured (UseRedis, UseServiceBus, etc.),
    /// broadcasts are automatically distributed across all servers. Broadcaster metadata is serialized
    /// and sent with each broadcast. Each server's subscribers apply their local filters to the
    /// received metadata, enabling perfect distributed filtering.
    /// </para>
    /// <para>
    /// <strong>Metadata Design</strong>: Define any structure you need (tenant, user, role, action, etc.).
    /// No interface implementation required. Use JSON-serializable types (records, classes, structs).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Define metadata type
    /// public record TodoMetadata {
    ///     public string? SubscriberId { get; init; }
    ///     public string? TenantId { get; init; }
    ///     public string? Action { get; init; }
    /// }
    ///
    /// // Create JsonSerializerContext
    /// [JsonSerializable(typeof(TodoModel))]
    /// [JsonSerializable(typeof(TodoMetadata))]
    /// public partial class MyAppJsonContext : JsonSerializerContext { }
    ///
    /// // In-memory mode (single server)
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TodoMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TodoMetadata);
    ///
    /// // Redis distributed mode
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TodoMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TodoMetadata,
    ///     options => options.UseRedis("redis-connection-string"));
    /// </code>
    /// </example>
    internal static IServiceCollection AddRxSseBroadcast<TModel, TMetadata>(
        this IServiceCollection services,
        JsonTypeInfo<TModel> modelTypeInfo,
        JsonTypeInfo<TMetadata> metadataTypeInfo,
        Action<RxBroadcastTransportOptions>? configureTransport = null) {
        ArgumentNullException.ThrowIfNull(modelTypeInfo, nameof(modelTypeInfo));
        ArgumentNullException.ThrowIfNull(metadataTypeInfo, nameof(metadataTypeInfo));
        var options = new RxBroadcastTransportOptions();
        configureTransport?.Invoke(options);
        if (options.TransportFactory != null) {
            services.TryAddSingleton(sp => options.TransportFactory(sp));
        }
        services.AddSingleton(sp => {
            var logger = sp.GetRequiredService<ILogger<RxSseBroadcastService<TModel, TMetadata>>>();
            var transport = sp.GetService<IRxBroadcastTransport>();
            var config = sp.GetService<IConfiguration>();
            return new RxSseBroadcastService<TModel, TMetadata>(logger, transport, modelTypeInfo, metadataTypeInfo, config);
        });
        return services;
    }
}

/// <summary>
/// Configuration options for distributed broadcast transport.
/// </summary>
internal class RxBroadcastTransportOptions {
    internal Func<IServiceProvider, IRxBroadcastTransport>? TransportFactory { get; private set; }

    /// <summary>
    /// Configures the service to use in-memory mode only (no distributed transport).
    /// </summary>
    /// <remarks>
    /// This is the default behavior if no transport is configured.
    /// Broadcasts will only reach subscribers on the local server.
    /// Subscription-time filtering works perfectly in this mode.
    /// </remarks>
    public void UseInMemory() {
        TransportFactory = null;
    }

    /// <summary>
    /// Configures a custom transport implementation.
    /// </summary>
    /// <param name="factory">Factory function to create the transport instance.</param>
    /// <remarks>
    /// <para>
    /// Use this method to integrate custom transport implementations.
    /// The factory receives the IServiceProvider for dependency resolution.
    /// </para>
    /// <para>
    /// <strong>Distributed Filtering</strong>: When using distributed transport,
    /// broadcaster metadata is serialized and transmitted to all servers. Each server's
    /// subscribers apply their local filters to the received metadata. This enables
    /// perfect filtering across all servers!
    /// </para>
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
}
