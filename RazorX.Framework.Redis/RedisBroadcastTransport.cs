using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RazorX.Framework.Redis;

/// <summary>
/// Redis Pub/Sub implementation of IRxBroadcastTransport for distributed broadcasts.
/// </summary>
/// <remarks>
/// <para>
/// This transport uses Redis Pub/Sub to distribute SSE broadcasts across multiple servers.
/// Each server instance subscribes to channels and receives messages published by ALL servers.
/// </para>
/// <para>
/// <strong>Architecture:</strong>
/// - Publishes use Redis PUBLISH command (fire-and-forget)
/// - Subscriptions use Redis SUBSCRIBE with automatic reconnection
/// - Messages are delivered at-most-once (no persistence/replay)
/// </para>
/// <para>
/// <strong>Performance Characteristics:</strong>
/// - Publish latency: ~1-5ms (Redis network + serialization)
/// - Subscribe latency: ~5-15ms (Redis → app delivery)
/// - Throughput: 10,000+ msg/sec per Redis instance
/// - Scales horizontally with Redis Cluster
/// </para>
/// <para>
/// <strong>AOT Compatibility:</strong> Fully AOT-compatible with no reflection or dynamic code.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Registration with metadata filtering
/// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
///     MyAppJsonContext.Default.TodoModel,
///     MyAppJsonContext.Default.TenantMetadata,
///     options => options.UseRedis("redis-connection-string"));
///
/// // With existing IConnectionMultiplexer
/// builder.Services.AddSingleton&lt;IConnectionMultiplexer&gt;(
///     ConnectionMultiplexer.Connect("localhost:6379"));
/// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
///     MyAppJsonContext.Default.TodoModel,
///     MyAppJsonContext.Default.TenantMetadata,
///     options => options.UseRedis());
/// </code>
/// </example>
public sealed class RedisBroadcastTransport : IRxBroadcastTransport {
    private readonly IConnectionMultiplexer redis;
    private readonly ILogger<RedisBroadcastTransport> logger;
    private readonly ConcurrentDictionary<string, SubscriptionState> subscriptions = new();
    private volatile bool disposed;

    /// <summary>
    /// Initializes a new RedisBroadcastTransport.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer (typically registered as singleton).</param>
    /// <param name="logger">Logger for diagnostics and errors.</param>
    public RedisBroadcastTransport(
        IConnectionMultiplexer redis,
        ILogger<RedisBroadcastTransport> logger)
    {
        ArgumentNullException.ThrowIfNull(redis, nameof(redis));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        this.redis = redis;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(string channel, string jsonMessage, CancellationToken ct = default) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RedisBroadcastTransport));

        try {
            var subscriber = redis.GetSubscriber();
            var subscribers = await subscriber.PublishAsync(RedisChannel.Literal(channel), jsonMessage);

            if (logger.IsEnabled(LogLevel.Debug)) {
                logger.LogDebug(
                    "Published to Redis channel {Channel}, reached {SubscriberCount} servers",
                    channel,
                    subscribers);
            }
        } catch (RedisConnectionException ex) {
            logger.LogError(ex,
                "Redis connection failed during publish to {Channel}. Check Redis server availability.",
                channel);
            throw;
        } catch (Exception ex) {
            logger.LogError(ex, "Failed to publish to Redis channel {Channel}", channel);
            throw;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> SubscribeAsync(
        string channel,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, nameof(RedisBroadcastTransport));

        // Create unbounded channel for message buffering
        var messageChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions {
            SingleReader = true,   // Only the async enumerable reads
            SingleWriter = false   // Redis callback + unsubscribe can write
        });

        var state = new SubscriptionState(messageChannel);
        subscriptions.TryAdd(channel, state);

        var subscriber = redis.GetSubscriber();
        var subscribed = false;

        try {
            // Subscribe to Redis Pub/Sub channel
            await subscriber.SubscribeAsync(RedisChannel.Literal(channel), (redisChannel, message) => {
                if (!message.IsNullOrEmpty) {
                    // Fire-and-forget write (non-blocking)
                    messageChannel.Writer.TryWrite(message!);
                }
            });

            subscribed = true;
            logger.LogInformation("Subscribed to Redis channel {Channel}", channel);

            // Register cleanup on cancellation
            ct.Register(() => {
                try {
                    subscriber.Unsubscribe(RedisChannel.Literal(channel));
                    messageChannel.Writer.Complete();
                    subscriptions.TryRemove(channel, out _);
                    logger.LogInformation("Unsubscribed from Redis channel {Channel}", channel);
                } catch (Exception ex) {
                    logger.LogWarning(ex, "Error during unsubscribe from {Channel}", channel);
                }
            });
        } catch (RedisConnectionException ex) {
            logger.LogError(ex,
                "Redis connection failed during subscription to {Channel}. Check Redis server availability.",
                channel);
            if (!subscribed) {
                subscriptions.TryRemove(channel, out _);
                messageChannel.Writer.Complete();
            }
            throw;
        } catch (Exception ex) {
            logger.LogError(ex, "Error in Redis subscription to {Channel}", channel);
            if (!subscribed) {
                subscriptions.TryRemove(channel, out _);
                messageChannel.Writer.Complete();
            }
            throw;
        }

        // Yield messages outside of try-catch
        await foreach (var msg in messageChannel.Reader.ReadAllAsync(ct)) {
            yield return msg;
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (disposed) {
            return;
        }

        // Unsubscribe from all channels and complete message channels
        foreach (var (channel, state) in subscriptions) {
            try {
                redis.GetSubscriber().Unsubscribe(RedisChannel.Literal(channel));
                state.MessageChannel.Writer.Complete();
            } catch (Exception ex) {
                logger.LogWarning(ex, "Error unsubscribing from {Channel} during disposal", channel);
            }
        }
        subscriptions.Clear();

        // Note: DO NOT dispose redis - it's typically a singleton shared across services
        disposed = true;
    }

    private sealed record SubscriptionState(Channel<string> MessageChannel);
}
