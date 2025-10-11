using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Provides a thread-safe broadcast service for distributing SSE updates to multiple connected clients with metadata-based filtering.
/// </summary>
/// <typeparam name="T">The model type to broadcast to subscribers.</typeparam>
/// <typeparam name="TMetadata">The metadata type for subscriber filtering (must implement IMetadataProvider).</typeparam>
/// <remarks>
/// <para>
/// This service enables selective broadcasting based on subscriber metadata (tenant, role, permissions, etc.).
/// Each subscriber provides metadata at connection time, which is used for filtering broadcasts.
/// </para>
/// <para>
/// <strong>Registration:</strong> Register as singleton in dependency injection:
/// <code>
/// builder.Services.AddRxSseBroadcast&lt;MyModel, MyMetadata&gt;(
///     MyAppJsonContext.Default.MyModel
/// );
/// </code>
/// </para>
/// <para>
/// <strong>Architecture:</strong> Each subscriber receives an isolated Channel for thread-safe message delivery.
/// When BroadcastUpdate() is called, the filter predicate (if provided) determines which subscribers receive the update.
/// </para>
/// <para>
/// <strong>Filtering Behavior</strong>:
/// - Single-server mode: Filter applies to all local subscribers
/// - Distributed mode: Filter applies to local subscribers only (remote servers receive all broadcasts)
/// </para>
/// <para>
/// <strong>Memory Management:</strong> Always call Unsubscribe() when done, or use CancellationToken.Register()
/// for automatic cleanup when clients disconnect.
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
/// // In SSE handler - Subscribe with metadata
/// public static IResult StreamUpdates(
///     HttpContext context,
///     [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
///     IRxDriver rxDriver,
///     RxSseBroadcastService&lt;TodoModel, TenantMetadata&gt; broadcast,
///     CancellationToken ct)
/// {
///     var metadata = new TenantMetadata(
///         SubscriberId: rxInstanceId,
///         TenantId: context.User.FindFirst("TenantId")!.Value,
///         Role: context.User.FindFirst(ClaimTypes.Role)!.Value
///     );
///
///     broadcast.Subscribe(metadata);
///     ct.Register(() => broadcast.Unsubscribe(rxInstanceId));
///
///     return rxDriver
///         .With(context)
///         .RenderSse(
///             broadcast.GetUpdates(rxInstanceId, ct),
///             async (todo, builder) => builder.AddFragment&lt;TodoCard&gt;(todo, "todo-list", Beforeend),
///             ct
///         );
/// }
///
/// // In regular handler - Broadcast with filtering
/// public static async Task&lt;IResult&gt; UpdateTodo(
///     HttpContext context,
///     [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
///     IRxDriver rxDriver,
///     RxSseBroadcastService&lt;TodoModel, TenantMetadata&gt; broadcast,
///     TodoModel todo)
/// {
///     var tenantId = context.User.FindFirst("TenantId")!.Value;
///     await repository.SaveAsync(todo);
///
///     // Filter by tenant and exclude triggering client
///     await broadcast.BroadcastUpdate(
///         todo,
///         filter: meta =>
///             meta.TenantId == tenantId &amp;&amp;
///             meta.SubscriberId != rxInstanceId
///     );
///
///     return await rxDriver
///         .With(context)
///         .AddFragment&lt;TodoCard&gt;(todo, $"todo-{todo.Id}", Swap)
///         .Render();
/// }
/// </code>
/// </example>
public sealed class RxSseBroadcastService<T, TMetadata> : IDisposable
    where TMetadata : ISseMetadataProvider {
    private sealed record SubscriberConnection(
        Channel<T> Channel,
        TMetadata Metadata
    );
    private readonly ConcurrentDictionary<string, SubscriberConnection> localSubscribers = new();
    private readonly IRxBroadcastTransport? transport;
    private readonly JsonTypeInfo<T>? modelTypeInfo;
    private readonly string broadcastChannel;
    private readonly string serverId;
    private readonly ILogger<RxSseBroadcastService<T, TMetadata>> logger;
    private readonly CancellationTokenSource? transportCts;
    private readonly Task? transportListenerTask;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of RxSseBroadcastService.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="transport">Optional distributed transport for multi-server broadcasts. If null, operates in in-memory mode only.</param>
    /// <param name="modelTypeInfo">Required JsonTypeInfo for AOT-compatible serialization when using distributed transport.</param>
    /// <param name="config">Optional configuration for server instance ID.</param>
    /// <exception cref="ArgumentException">Thrown when transport is provided but modelTypeInfo is null.</exception>
    /// <remarks>
    /// <para>
    /// When transport is null (default), the service operates in in-memory mode with no cross-server broadcasting.
    /// </para>
    /// <para>
    /// When transport is provided, modelTypeInfo is required for AOT-compatible JSON serialization.
    /// The service will automatically listen for broadcasts from other servers and deliver to local channels.
    /// </para>
    /// <para>
    /// Server instance ID is used to identify the source of broadcasts and prevent echo (delivering own broadcasts back to self).
    /// If not configured, defaults to Environment.MachineName.
    /// </para>
    /// </remarks>
    public RxSseBroadcastService(
        ILogger<RxSseBroadcastService<T, TMetadata>> logger,
        IRxBroadcastTransport? transport = null,
        JsonTypeInfo<T>? modelTypeInfo = null,
        IConfiguration? config = null) {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (transport != null && modelTypeInfo == null) {
            throw new ArgumentException(
                $"JsonTypeInfo<{typeof(T).Name}> is required for AOT compatibility when using distributed transport. " +
                "Provide it during service registration with AddRxSseBroadcast().",
                nameof(modelTypeInfo));
        }
        this.transport = transport;
        this.modelTypeInfo = modelTypeInfo;
        broadcastChannel = $"rx-broadcast:{typeof(T).FullName}";
        serverId = config?["ServerInstanceId"] ?? Environment.MachineName;
        if (this.transport != null) {
            transportCts = new CancellationTokenSource();
            transportListenerTask = Task.Run(ListenToTransportAsync, transportCts.Token);
        }
    }

    /// <summary>
    /// Subscribes a client with associated metadata.
    /// </summary>
    /// <param name="metadata">Metadata for this subscriber, including the unique SubscriberId.</param>
    /// <returns>True if the subscriber was added; false if a subscriber with this ID already exists.</returns>
    /// <remarks>
    /// <para>
    /// The subscriber ID is extracted from metadata.SubscriberId and must be unique.
    /// Each subscriber receives an isolated unbounded channel for message delivery.
    /// </para>
    /// <para>
    /// <strong>Recommended pattern:</strong> Use rx-instance-id from client's sessionStorage:
    /// <code>
    /// var metadata = new MyMetadata(
    ///     SubscriberId: rxInstanceId,  // From query parameter
    ///     TenantId: context.User.FindFirst("TenantId")!.Value,
    ///     Role: context.User.FindFirst(ClaimTypes.Role)!.Value
    /// );
    /// broadcast.Subscribe(metadata);
    /// </code>
    /// </para>
    /// <para>
    /// Always call Unsubscribe() when done, or use CancellationToken.Register() for automatic cleanup.
    /// </para>
    /// </remarks>
    public bool Subscribe(TMetadata metadata) {
        ArgumentNullException.ThrowIfNull(metadata, nameof(metadata));
        ObjectDisposedException.ThrowIf(disposed, this);
        var subscriberId = metadata.SubscriberId;
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriberId, nameof(metadata.SubscriberId));
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false
        });
        var connection = new SubscriberConnection(channel, metadata);
        if (!localSubscribers.TryAdd(subscriberId, connection)) {
            if (logger.IsEnabled(LogLevel.Warning)) {
                logger.LogWarning(
                    "Duplicate subscription attempt for subscriber {SubscriberId}. " +
                    "A subscription with this ID already exists. Ignoring duplicate.",
                    subscriberId
                );
            }
            return false;
        }
        if (logger.IsEnabled(LogLevel.Debug)) {
            logger.LogDebug(
                "Subscriber {SubscriberId} registered successfully. Total subscribers: {Count}",
                subscriberId,
                localSubscribers.Count
            );
        }
        return true;
    }

    /// <summary>
    /// Unsubscribes a client and completes their channel.
    /// </summary>
    /// <param name="subscriberId">The subscriber ID to unsubscribe.</param>
    /// <remarks>
    /// This method is idempotent - calling it multiple times with the same ID is safe.
    /// The channel is completed gracefully, allowing any pending reads to finish.
    /// </remarks>
    public void Unsubscribe(string subscriberId) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (localSubscribers.TryRemove(subscriberId, out var connection)) {
            try {
                connection.Channel.Writer.Complete();
                if (logger.IsEnabled(LogLevel.Debug)) {
                    logger.LogDebug(
                        "Subscriber {SubscriberId} unsubscribed. Total subscribers: {Count}",
                        subscriberId,
                        localSubscribers.Count
                    );
                }
            } catch (Exception ex) {
                if (logger.IsEnabled(LogLevel.Debug)) {
                    logger.LogDebug(ex, "Channel already completed for subscriber {SubscriberId}", subscriberId);
                }
            }
        } else {
            if (logger.IsEnabled(LogLevel.Debug)) {
                logger.LogDebug(
                    "Unsubscribe called for non-existent subscriber {SubscriberId}. Already unsubscribed or never subscribed.",
                    subscriberId
                );
            }
        }
    }

    /// <summary>
    /// Broadcasts an update to connected subscribers with optional metadata-based filtering.
    /// </summary>
    /// <param name="model">The model to broadcast to subscribers.</param>
    /// <param name="filter">
    /// Optional predicate to filter subscribers based on their metadata.
    /// Return true to INCLUDE subscriber in the broadcast.
    /// If null, ALL subscribers receive the broadcast.
    /// </param>
    /// <remarks>
    /// <para>
    /// Writes to filtered subscriber channels in parallel using Task.WhenAll.
    /// If a channel fails (subscriber disconnected), the error is caught and doesn't affect other subscribers.
    /// </para>
    /// <para>
    /// <strong>Filtering Behavior</strong>:
    /// - <strong>Single-server mode</strong>: Filter is applied to all local subscribers
    /// - <strong>Distributed mode</strong>: Filter is applied to local subscribers only. Remote servers
    ///   receive the broadcast for ALL their subscribers without filtering.
    /// </para>
    /// <para>
    /// <strong>Why distributed filtering is limited</strong>: Predicates (lambdas) cannot be serialized
    /// and sent across servers. This is a fundamental constraint of distributed systems.
    /// </para>
    /// <para>
    /// <strong>Common Patterns</strong>:
    /// <code>
    /// // Echo suppression (most common)
    /// await broadcast.BroadcastUpdate(
    ///     model,
    ///     filter: meta => meta.SubscriberId != rxInstanceId
    /// );
    ///
    /// // Tenant isolation + echo suppression
    /// await broadcast.BroadcastUpdate(
    ///     model,
    ///     filter: meta =>
    ///         meta.TenantId == tenantId &amp;&amp;
    ///         meta.SubscriberId != rxInstanceId
    /// );
    ///
    /// // Role-based filtering
    /// await broadcast.BroadcastUpdate(
    ///     model,
    ///     filter: meta => meta.Role == "Admin"
    /// );
    ///
    /// // Broadcast to all (no filter)
    /// await broadcast.BroadcastUpdate(model);
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Safe to call from multiple threads simultaneously.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Completes when ALL filtered channels have received the message (parallel write).
    /// </para>
    /// </remarks>
    public async Task BroadcastUpdate(T model, Func<TMetadata, bool>? filter = null) {
        ArgumentNullException.ThrowIfNull(model, nameof(model));
        ObjectDisposedException.ThrowIf(disposed, this);
        await BroadcastToLocalChannels(model, filter);
        if (transport != null && modelTypeInfo != null) {
            try {
                string modelJson = JsonSerializer.Serialize(model, modelTypeInfo);
                var transportMsg = new TransportMessage(
                    PayloadJson: modelJson,
                    SourceServerId: serverId,
                    TimestampUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                );
                string transportJson = JsonSerializer.Serialize(
                    transportMsg,
                    RxJsonSerializerContext.Default.TransportMessage);
                await transport.PublishAsync(broadcastChannel, transportJson);
                if (logger.IsEnabled(LogLevel.Debug)) {
                    logger.LogDebug("Published broadcast to transport channel {Channel}", broadcastChannel);
                }
            } catch (Exception ex) {
                if (logger.IsEnabled(LogLevel.Error)) {
                    logger.LogError(ex, "Failed to publish broadcast to transport on channel {Channel}", broadcastChannel);
                }
            }
        }
    }

    private async Task BroadcastToLocalChannels(T model, Func<TMetadata, bool>? filter) {
        if (localSubscribers.IsEmpty) {
            return;
        }
        var tasks = localSubscribers
            .Where(kvp => filter == null || filter(kvp.Value.Metadata))
            .Select(async kvp => {
                try {
                    await kvp.Value.Channel.Writer.WriteAsync(model);
                } catch (ChannelClosedException) {
                    if (logger.IsEnabled(LogLevel.Debug)) {
                        logger.LogDebug("Channel closed for subscriber {SubscriberId} (client disconnected)", kvp.Key);
                    }
                } catch (Exception ex) {
                    if (logger.IsEnabled(LogLevel.Warning)) {
                        logger.LogWarning(ex, "Unexpected error writing to channel for subscriber {SubscriberId}", kvp.Key);
                    }
                }
            });
        await Task.WhenAll(tasks);
    }

    private async Task ListenToTransportAsync() {
        try {
            await foreach (var transportJson in transport!.SubscribeAsync(
                broadcastChannel,
                transportCts!.Token)) {
                try {
                    var transportMsg = JsonSerializer.Deserialize(
                        transportJson,
                        RxJsonSerializerContext.Default.TransportMessage);
                    if (transportMsg == null) {
                        if (logger.IsEnabled(LogLevel.Warning)) {
                            logger.LogWarning("Received null transport message on channel {Channel}", broadcastChannel);
                        }
                        continue;
                    }
                    if (transportMsg.SourceServerId == serverId) {
                        continue;
                    }
                    var model = JsonSerializer.Deserialize(
                        transportMsg.PayloadJson,
                        modelTypeInfo!);
                    if (model != null) {
                        await BroadcastToLocalChannels(model, filter: null);
                    }
                } catch (JsonException ex) {
                    if (logger.IsEnabled(LogLevel.Warning)) {
                        logger.LogWarning(ex, "Failed to deserialize transport message on channel {Channel}", broadcastChannel);
                    }
                } catch (Exception ex) {
                    if (logger.IsEnabled(LogLevel.Warning)) {
                        logger.LogWarning(ex, "Error processing transport message on channel {Channel}", broadcastChannel);
                    }
                }
            }
        } catch (OperationCanceledException) {
            if (logger.IsEnabled(LogLevel.Debug)) {
                logger.LogDebug("Transport listener cancelled on channel {Channel}", broadcastChannel);
            }
        } catch (Exception ex) {
            if (logger.IsEnabled(LogLevel.Error)) {
                logger.LogError(ex, "Transport listener error on channel {Channel}", broadcastChannel);
            }
        }
    }

    /// <summary>
    /// Gets an async stream of updates for a specific subscriber.
    /// </summary>
    /// <param name="subscriberId">The subscriber ID that was registered with Subscribe().</param>
    /// <param name="cancellationToken">Cancellation token to stop the stream (typically HttpContext.RequestAborted).</param>
    /// <returns>An async enumerable of updates for this subscriber.</returns>
    /// <remarks>
    /// <para>
    /// This method is typically called within RenderSse() to provide the model stream.
    /// The stream continues until the channel is completed or the cancellation token is triggered.
    /// </para>
    /// <para>
    /// If the subscriber ID is not found (invalid or not yet subscribed), the stream ends immediately (yield break).
    /// </para>
    /// </remarks>
    public async IAsyncEnumerable<T> GetUpdates(
        string subscriberId,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!localSubscribers.TryGetValue(subscriberId, out var connection)) {
            yield break;
        }
        await foreach (var update in connection.Channel.Reader.ReadAllAsync(cancellationToken)) {
            yield return update;
        }
    }

    /// <summary>
    /// Gets the count of currently active SSE connections.
    /// </summary>
    /// <returns>The number of active subscribers.</returns>
    /// <remarks>
    /// Useful for monitoring and diagnostics. The count includes only active subscribers
    /// that have called Subscribe() but not yet Unsubscribe().
    /// </remarks>
    public int GetActiveConnectionCount() {
        ObjectDisposedException.ThrowIf(disposed, this);
        return localSubscribers.Count;
    }

    /// <summary>
    /// Gets a list of all active subscriber IDs.
    /// </summary>
    /// <returns>Read-only list of subscriber IDs.</returns>
    /// <remarks>
    /// Useful for diagnostics and debugging. The list is a snapshot at the time of the call.
    /// </remarks>
    public IReadOnlyList<string> GetActiveSubscribers() {
        ObjectDisposedException.ThrowIf(disposed, this);
        return [.. localSubscribers.Keys];
    }

    /// <summary>
    /// Checks if a subscriber with the specified ID is currently subscribed.
    /// </summary>
    /// <param name="subscriberId">The subscriber ID to check.</param>
    /// <returns>True if a subscriber with this ID exists; otherwise false.</returns>
    /// <remarks>
    /// Useful for checking if a subscriber already exists before calling Subscribe() to avoid duplicate subscriptions.
    /// </remarks>
    public bool HasSubscriber(string subscriberId) {
        ObjectDisposedException.ThrowIf(disposed, this);
        return localSubscribers.ContainsKey(subscriberId);
    }

    /// <summary>
    /// Gets the metadata for a specific subscriber.
    /// </summary>
    /// <param name="subscriberId">The subscriber ID to query.</param>
    /// <returns>The subscriber's metadata if found; otherwise null.</returns>
    /// <remarks>
    /// Useful for diagnostics and debugging. Returns null if subscriber doesn't exist.
    /// </remarks>
    public TMetadata? GetSubscriberMetadata(string subscriberId) {
        ObjectDisposedException.ThrowIf(disposed, this);
        return localSubscribers.TryGetValue(subscriberId, out var connection)
            ? connection.Metadata
            : default;
    }

    /// <summary>
    /// Gets all active subscribers with their metadata.
    /// </summary>
    /// <returns>Dictionary of subscriber IDs to their metadata.</returns>
    /// <remarks>
    /// Useful for monitoring and diagnostics. Returns a snapshot at the time of the call.
    /// </remarks>
    public IReadOnlyDictionary<string, TMetadata> GetAllSubscriberMetadata() {
        ObjectDisposedException.ThrowIf(disposed, this);
        return localSubscribers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Metadata
        );
    }

    /// <summary>
    /// Disposes the broadcast service and completes all subscriber channels.
    /// </summary>
    /// <remarks>
    /// Called automatically when the application shuts down (registered as singleton).
    /// Completes all channels gracefully, allowing pending reads to finish.
    /// </remarks>
    public void Dispose() {
        if (disposed) {
            return;
        }
        try {
            transportCts?.Cancel();
            if (transportListenerTask != null) {
                try {
                    transportListenerTask.Wait(TimeSpan.FromSeconds(5));
                } catch (AggregateException ae) {
                    foreach (var ex in ae.InnerExceptions) {
                        if (ex is not OperationCanceledException) {
                            if (logger.IsEnabled(LogLevel.Error)) {
                                logger.LogError(ex, "Unhandled exception in transport listener task");
                            }
                        }
                    }
                }
            }
            transportCts?.Dispose();
        } catch (Exception ex) {
            if (logger.IsEnabled(LogLevel.Debug)) {
                logger.LogDebug(ex, "Error during transport cancellation cleanup");
            }
        }
        foreach (var connection in localSubscribers.Values) {
            try {
                connection.Channel.Writer.Complete();
            } catch (Exception ex) {
                if (logger.IsEnabled(LogLevel.Debug)) {
                    logger.LogDebug(ex, "Error completing channel during disposal");
                }
            }
        }
        localSubscribers.Clear();
        try {
            transport?.Dispose();
        } catch (Exception ex) {
            if (logger.IsEnabled(LogLevel.Warning)) {
                logger.LogWarning(ex, "Error disposing transport for channel {Channel}", broadcastChannel);
            }
        }
        disposed = true;
    }
}
