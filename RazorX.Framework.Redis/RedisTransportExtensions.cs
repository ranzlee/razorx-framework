using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RazorX.Framework.Redis;

/// <summary>
/// Extension methods for configuring Redis transport for RxSseBroadcastService.
/// </summary>
public static class RedisTransportExtensions {
    /// <summary>
    /// Configures the broadcast service to use Redis Pub/Sub for distributed broadcasts.
    /// </summary>
    /// <param name="options">Transport options to configure.</param>
    /// <param name="connectionString">Redis connection string (e.g., "localhost:6379").</param>
    /// <remarks>
    /// <para>
    /// This method creates a new IConnectionMultiplexer internally and registers it as a singleton.
    /// </para>
    /// <para>
    /// <strong>Connection String Format:</strong>
    /// - Simple: "localhost:6379"
    /// - With password: "localhost:6379,password=mypassword"
    /// - With SSL: "myredis.azure.com:6380,ssl=true,password=mykey"
    /// - Multiple endpoints: "server1:6379,server2:6379,server3:6379"
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TenantMetadata,
    ///     options => options.UseRedis("localhost:6379"));
    /// </code>
    /// </example>
    public static void UseRedis(
        this RxBroadcastTransportOptions options,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        options.UseCustomTransport(sp => {
            // Create connection multiplexer
            var redis = ConnectionMultiplexer.Connect(connectionString);

            // Register as singleton for reuse by other services
            sp.GetRequiredService<IServiceCollection>()
                .TryAddSingleton<IConnectionMultiplexer>(redis);

            var logger = sp.GetRequiredService<ILogger<RedisBroadcastTransport>>();
            return new RedisBroadcastTransport(redis, logger);
        });
    }

    /// <summary>
    /// Configures the broadcast service to use an existing IConnectionMultiplexer from DI.
    /// </summary>
    /// <param name="options">Transport options to configure.</param>
    /// <remarks>
    /// Use this overload when you've already registered IConnectionMultiplexer in your DI container.
    /// This is the preferred approach when sharing Redis connections across services.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Register Redis connection
    /// builder.Services.AddSingleton&lt;IConnectionMultiplexer&gt;(sp => {
    ///     var config = sp.GetRequiredService&lt;IConfiguration&gt;();
    ///     return ConnectionMultiplexer.Connect(config["Redis:ConnectionString"]);
    /// });
    ///
    /// // Use existing connection with metadata filtering
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TenantMetadata,
    ///     options => options.UseRedis());
    /// </code>
    /// </example>
    public static void UseRedis(this RxBroadcastTransportOptions options) {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        options.UseCustomTransport(sp => {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisBroadcastTransport>>();
            return new RedisBroadcastTransport(redis, logger);
        });
    }

    /// <summary>
    /// Configures the broadcast service to use Redis with advanced ConfigurationOptions.
    /// </summary>
    /// <param name="options">Transport options to configure.</param>
    /// <param name="configOptions">Redis configuration options.</param>
    /// <remarks>
    /// Use this overload for advanced scenarios requiring fine-grained Redis configuration.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TenantMetadata,
    ///     options => {
    ///         var redisConfig = new ConfigurationOptions {
    ///             EndPoints = { "server1:6379", "server2:6379" },
    ///             Password = "mypassword",
    ///             Ssl = true,
    ///             ConnectRetry = 5,
    ///             ConnectTimeout = 10000,
    ///             SyncTimeout = 5000,
    ///             AbortOnConnectFail = false
    ///         };
    ///         options.UseRedis(redisConfig);
    ///     });
    /// </code>
    /// </example>
    public static void UseRedis(
        this RxBroadcastTransportOptions options,
        ConfigurationOptions configOptions)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(configOptions, nameof(configOptions));

        options.UseCustomTransport(sp => {
            var redis = ConnectionMultiplexer.Connect(configOptions);

            // Register as singleton
            sp.GetRequiredService<IServiceCollection>()
                .TryAddSingleton<IConnectionMultiplexer>(redis);

            var logger = sp.GetRequiredService<ILogger<RedisBroadcastTransport>>();
            return new RedisBroadcastTransport(redis, logger);
        });
    }
}
