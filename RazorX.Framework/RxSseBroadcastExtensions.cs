using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Extension methods for registering RxSseBroadcastService with dependency injection.
/// </summary>
public static class RxSseBroadcastExtensions {
    /// <summary>
    /// Registers RxSseBroadcastService with AOT-compatible JSON serialization and metadata-based filtering.
    /// </summary>
    /// <typeparam name="T">The model type to broadcast.</typeparam>
    /// <typeparam name="TMetadata">The metadata type for subscriber filtering (must implement IMetadataProvider).</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="modelTypeInfo">Source-generated JsonTypeInfo for the model type (required for AOT).</param>
    /// <param name="configureTransport">Optional transport configuration delegate.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension method simplifies registration of RxSseBroadcastService with proper AOT support
    /// and metadata-based filtering capabilities.
    /// </para>
    /// <para>
    /// <strong>In-Memory Mode (Default):</strong> If configureTransport is null or calls UseInMemory(),
    /// the service operates in single-server mode with no distributed transport.
    /// Metadata filtering works fully in this mode.
    /// </para>
    /// <para>
    /// <strong>Distributed Mode:</strong> When transport is configured (UseRedis, UseServiceBus, etc.),
    /// broadcasts are automatically distributed across all servers. Note that metadata filtering
    /// only applies to local subscribers on each server (predicates cannot be serialized).
    /// </para>
    /// <para>
    /// <strong>Metadata Requirements</strong>: Your metadata type must implement IMetadataProvider
    /// which requires a SubscriberId property for subscriber identification and filtering.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Define metadata type
    /// public record TenantMetadata(
    ///     string SubscriberId,
    ///     string TenantId,
    ///     string Role
    /// ) : IMetadataProvider {
    ///     public IReadOnlyDictionary&lt;string, string&gt; ToSerializableDictionary() {
    ///         return new Dictionary&lt;string, string&gt; {
    ///             [nameof(SubscriberId)] = SubscriberId,
    ///             [nameof(TenantId)] = TenantId,
    ///             [nameof(Role)] = Role
    ///         };
    ///     }
    /// }
    ///
    /// // In-memory mode (single server)
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel);
    ///
    /// // Redis distributed mode
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     options => options.UseRedis("redis-connection-string"));
    ///
    /// // Service Bus distributed mode
    /// builder.Services.AddRxSseBroadcast&lt;NotificationModel, UserMetadata&gt;(
    ///     MyAppJsonContext.Default.NotificationModel,
    ///     options => options.UseServiceBus("servicebus-connection-string"));
    /// </code>
    /// </example>
    public static IServiceCollection AddRxSseBroadcast<T, TMetadata>(
        this IServiceCollection services,
        JsonTypeInfo<T> modelTypeInfo,
        Action<RxBroadcastTransportOptions>? configureTransport = null)
        where TMetadata : ISseMetadataProvider {
        ArgumentNullException.ThrowIfNull(modelTypeInfo, nameof(modelTypeInfo));
        var options = new RxBroadcastTransportOptions();
        configureTransport?.Invoke(options);
        if (options.TransportFactory != null) {
            services.TryAddSingleton(sp => options.TransportFactory(sp));
        }
        services.AddSingleton(sp => {
            var logger = sp.GetRequiredService<ILogger<RxSseBroadcastService<T, TMetadata>>>();
            var transport = sp.GetService<IRxBroadcastTransport>();
            var config = sp.GetService<IConfiguration>();
            return new RxSseBroadcastService<T, TMetadata>(logger, transport, modelTypeInfo, config);
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
    /// Metadata filtering works fully in this mode.
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
    /// <strong>Distributed Filtering Limitation</strong>: When using distributed transport,
    /// metadata filtering only applies to local subscribers on each server. Remote servers
    /// receive broadcasts for all their subscribers without filtering (predicates cannot
    /// be serialized across process boundaries).
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
