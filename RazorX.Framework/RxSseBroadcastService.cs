using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Provides a thread-safe broadcast service for distributing SSE updates to multiple connected clients with subscription-time filtering.
/// </summary>
/// <typeparam name="TModel">The model type to broadcast to subscribers.</typeparam>
/// <typeparam name="TMetadata">The metadata type that can be sent with broadcasts for filtering purposes.</typeparam>
/// <remarks>
/// <para>
/// This service enables selective broadcasting based on broadcaster metadata (tenant, user, action, etc.).
/// Each subscriber provides a filter at subscription time that examines incoming broadcast metadata.
/// </para>
/// <para>
/// <strong>Registration:</strong> Register as singleton in dependency injection:
/// <code>
/// builder.Services.AddSingleton(sp => {
///     var logger = sp.GetRequiredService&lt;ILogger&lt;RxSseBroadcastService&lt;MyModel, MyMetadata&gt;&gt;&gt;();
///     return new RxSseBroadcastService&lt;MyModel, MyMetadata&gt;(logger);
/// });
/// </code>
/// </para>
/// <para>
/// <strong>Architecture:</strong> Each subscriber receives an isolated Channel for thread-safe message delivery.
/// Subscriber's filter (if provided) is evaluated against broadcaster metadata for each broadcast.
/// </para>
/// <para>
/// <strong>Filtering Behavior</strong>:
/// - Filter is stored with each subscriber
/// - Evaluated locally when broadcasts arrive (local or distributed)
/// - Works perfectly in distributed mode (metadata serializes, filter stays local)
/// </para>
/// <para>
/// <strong>Memory Management:</strong> Always call Unsubscribe() when done, or use CancellationToken.Register()
/// for automatic cleanup when clients disconnect.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Subscribe with filter
/// public static IResult StreamUpdates(
///     HttpContext context,
///     [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
///     IRxDriver rxDriver,
///     RxSseBroadcastService&lt;TodoModel, TodoMetadata&gt; broadcast,
///     CancellationToken ct)
/// {
///     var myTenantId = context.User.FindFirst("TenantId")!.Value;
///
///     broadcast.Subscribe(
///         rxInstanceId,
///         filter: meta =>
///             meta?.TenantId == myTenantId &amp;&amp;
///             meta?.SubscriberId != rxInstanceId
///     );
///     ct.Register(() => broadcast.Unsubscribe(rxInstanceId));
///
///     return rxDriver
///         .With(context)
///         .RenderSse(
///             broadcast.GetUpdates(rxInstanceId, ct),
///             async (todo, builder) => builder.AddFragment&lt;TodoCard&gt;(todo, "list", Swap),
///             ct
///         );
/// }
///
/// // Broadcast with metadata
/// public static async Task&lt;IResult&gt; CreateTodo(
///     HttpContext context,
///     [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
///     IRxDriver rxDriver,
///     RxSseBroadcastService&lt;TodoModel, TodoMetadata&gt; broadcast,
///     TodoModel todo)
/// {
///     var tenantId = context.User.FindFirst("TenantId")!.Value;
///     await repository.SaveAsync(todo);
///
///     await broadcast.BroadcastUpdate(
///         todo,
///         new TodoMetadata { TenantId = tenantId, SubscriberId = rxInstanceId }
///     );
///
///     return await rxDriver
///         .With(context)
///         .AddFragment&lt;TodoCard&gt;(todo, "list", AppendBeforeEnd)
///         .Render();
/// }
/// </code>
/// </example>
public sealed class RxSseBroadcastService<TModel, TMetadata> : IDisposable {
    private sealed record SubscriberConnection(
        Channel<TModel> Channel,
        Func<TMetadata?, bool>? Filter
    );
    private readonly ConcurrentDictionary<string, SubscriberConnection> localSubscribers = new();
    private readonly IRxBroadcastTransport? transport;
    private readonly JsonTypeInfo<TModel>? modelTypeInfo;
    private readonly JsonTypeInfo<TMetadata>? metadataTypeInfo;
    private readonly string broadcastChannel;
    private readonly string serverId;
    private readonly ILogger<RxSseBroadcastService<TModel, TMetadata>> logger;
    private readonly CancellationTokenSource? transportCts;
    private readonly Task? transportListenerTask;
    private volatile bool disposed = false;

    /// <summary>
    /// Initializes a new instance of RxSseBroadcastService.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="transport">Optional distributed transport for multi-server broadcasts. If null, operates in in-memory mode only.</param>
    /// <param name="modelTypeInfo">Required JsonTypeInfo for AOT-compatible model serialization when using distributed transport.</param>
    /// <param name="metadataTypeInfo">Required JsonTypeInfo for AOT-compatible metadata serialization when using distributed transport.</param>
    /// <param name="config">Optional configuration for server instance ID.</param>
    /// <exception cref="ArgumentException">Thrown when transport is provided but modelTypeInfo or metadataTypeInfo is null.</exception>
    /// <remarks>
    /// <para>
    /// When transport is null (default), the service operates in in-memory mode with no cross-server broadcasting.
    /// </para>
    /// <para>
    /// When transport is provided, both modelTypeInfo and metadataTypeInfo are required for AOT-compatible JSON serialization.
    /// The service will automatically listen for broadcasts from other servers and deliver to local channels.
    /// </para>
    /// <para>
    /// Server instance ID is used to identify the source of broadcasts and prevent echo (delivering own broadcasts back to self).
    /// If not configured, defaults to Environment.MachineName.
    /// </para>
    /// </remarks>
    public RxSseBroadcastService(
        ILogger<RxSseBroadcastService<TModel, TMetadata>> logger,
        IRxBroadcastTransport? transport = null,
        JsonTypeInfo<TModel>? modelTypeInfo = null,
        JsonTypeInfo<TMetadata>? metadataTypeInfo = null,
        IConfiguration? config = null) {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (transport != null) {
            if (modelTypeInfo == null) {
                throw new ArgumentException(
                    $"JsonTypeInfo<{typeof(TModel).Name}> is required for AOT compatibility when using distributed transport. " +
                    "Provide it during service registration with AddRxSseBroadcast().",
                    nameof(modelTypeInfo));
            }
            if (metadataTypeInfo == null) {
                throw new ArgumentException(
                    $"JsonTypeInfo<{typeof(TMetadata).Name}> is required for AOT compatibility when using distributed transport. " +
                    "Provide it during service registration with AddRxSseBroadcast().",
                    nameof(metadataTypeInfo));
            }
        }
        this.transport = transport;
        this.modelTypeInfo = modelTypeInfo;
        this.metadataTypeInfo = metadataTypeInfo;
        broadcastChannel = $"rx-broadcast:{typeof(TModel).FullName}";
        serverId = config?["ServerInstanceId"] ?? Environment.MachineName;
        var modelType = typeof(TModel).FullName ?? typeof(TModel).Name;
        RxTelemetry.RegisterSseSubscriberCountCallback(modelType, GetActiveConnectionCount);
        if (this.transport != null) {
            transportCts = new CancellationTokenSource();
            transportListenerTask = Task.Run(ListenToTransportAsync, transportCts.Token);
        }
    }

    /// <summary>
    /// Subscribes a client with an optional filter for broadcast metadata.
    /// </summary>
    /// <param name="subscriberId">Unique identifier for this subscriber (typically rx-instance-id from client).</param>
    /// <param name="filter">
    /// Optional predicate to filter broadcasts based on broadcaster metadata.
    /// Return true to ACCEPT broadcast, false to REJECT.
    /// If null, ALL broadcasts are accepted.
    /// The filter receives nullable metadata (null when broadcaster sends no metadata).
    /// </param>
    /// <returns>True if the subscriber was added; false if a subscriber with this ID already exists.</returns>
    /// <remarks>
    /// <para>
    /// Each subscriber receives an isolated unbounded channel for message delivery.
    /// The filter is stored with the subscription and evaluated for each incoming broadcast (local or distributed).
    /// </para>
    /// <para>
    /// <strong>Recommended pattern:</strong> Use rx-instance-id from client's sessionStorage:
    /// <code>
    /// var myTenantId = context.User.FindFirst("TenantId")!.Value;
    /// broadcast.Subscribe(
    ///     rxInstanceId,
    ///     filter: meta =>
    ///         meta?.TenantId == myTenantId &amp;&amp;
    ///         meta?.SubscriberId != rxInstanceId
    /// );
    /// </code>
    /// </para>
    /// <para>
    /// Always call Unsubscribe() when done, or use CancellationToken.Register() for automatic cleanup.
    /// </para>
    /// </remarks>
    public bool Subscribe(string subscriberId, Func<TMetadata?, bool>? filter = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriberId, nameof(subscriberId));
        ObjectDisposedException.ThrowIf(disposed, this);
        var channel = Channel.CreateUnbounded<TModel>(new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false
        });
        var connection = new SubscriberConnection(channel, filter);
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
    /// Broadcasts an update to connected subscribers with optional broadcaster metadata.
    /// </summary>
    /// <param name="model">The model to broadcast to subscribers.</param>
    /// <param name="broadcasterMetadata">
    /// Optional metadata about the broadcast source (tenant, user, action, etc.).
    /// Subscribers' filters examine this metadata to decide whether to accept the broadcast.
    /// If null, broadcasts to all subscribers (unless their filter rejects null metadata).
    /// </param>
    /// <remarks>
    /// <para>
    /// Writes to subscriber channels in parallel using Task.WhenAll.
    /// Each subscriber's filter (if provided) is evaluated against the broadcaster metadata.
    /// If a channel fails (subscriber disconnected), the error is caught and doesn't affect other subscribers.
    /// </para>
    /// <para>
    /// <strong>Filtering Behavior</strong>:
    /// - <strong>In-memory mode</strong>: Each subscriber's filter is applied to broadcaster metadata
    /// - <strong>Distributed mode</strong>: Metadata is serialized and transmitted. Each server's subscribers
    ///   apply their local filters to the received metadata. Perfect filtering across all servers!
    /// </para>
    /// <para>
    /// <strong>Common Patterns</strong>:
    /// <code>
    /// // Echo suppression
    /// await broadcast.BroadcastUpdate(
    ///     model,
    ///     new TodoMetadata { SubscriberId = rxInstanceId }
    /// );
    /// // Subscriber filter: meta => meta?.SubscriberId != mySubscriberId
    ///
    /// // Tenant isolation
    /// await broadcast.BroadcastUpdate(
    ///     model,
    ///     new TodoMetadata { TenantId = tenantId, SubscriberId = rxInstanceId }
    /// );
    /// // Subscriber filter: meta => meta?.TenantId == myTenantId
    ///
    /// // Broadcast to all (no metadata)
    /// await broadcast.BroadcastUpdate(model);
    /// // Subscribers with no filter or filter accepting null receive it
    /// </code>
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Safe to call from multiple threads simultaneously.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Completes when ALL matching channels have received the message (parallel write).
    /// </para>
    /// </remarks>
    public async Task BroadcastUpdate(TModel model, TMetadata? broadcasterMetadata = default) {
        ArgumentNullException.ThrowIfNull(model, nameof(model));
        ObjectDisposedException.ThrowIf(disposed, this);
        using var activity = RxTelemetry.ActivitySource.StartActivity("razorx.sse.broadcast");
        activity?.SetTag("model.type", typeof(TModel).Name);
        activity?.SetTag("has.metadata", broadcasterMetadata != null);
        activity?.SetTag("has.transport", transport != null);
        var stopwatch = ValueStopwatch.StartNew();
        var subscriberCount = localSubscribers.Count;
        try {
            await BroadcastToLocalChannels(model, broadcasterMetadata);
            if (transport != null && modelTypeInfo != null && metadataTypeInfo != null) {
                using var transportActivity = RxTelemetry.ActivitySource.StartActivity(
                    "razorx.sse.transport.publish",
                    ActivityKind.Producer);
                transportActivity?.SetTag("channel", broadcastChannel);
                transportActivity?.SetTag("server.id", serverId);
                try {
                    string modelJson = JsonSerializer.Serialize(model, modelTypeInfo);
                    string? metadataJson = broadcasterMetadata != null
                        ? JsonSerializer.Serialize(broadcasterMetadata, metadataTypeInfo)
                        : null;
                    var transportMsg = new TransportMessage(
                        PayloadJson: modelJson,
                        BroadcasterMetadataJson: metadataJson,
                        SourceServerId: serverId,
                        TimestampUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        TraceId: Activity.Current?.TraceId.ToString(),
                        ParentSpanId: Activity.Current?.SpanId.ToString()
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
            RxTelemetry.BroadcastCounter.Add(1,
                new KeyValuePair<string, object?>("model.type", typeof(TModel).Name),
                new KeyValuePair<string, object?>("has.metadata", broadcasterMetadata != null),
                new KeyValuePair<string, object?>("has.transport", transport != null));
            RxTelemetry.BroadcastSubscriberCount.Record(subscriberCount,
                new KeyValuePair<string, object?>("model.type", typeof(TModel).Name));
        } finally {
            RxTelemetry.BroadcastDuration.Record(
                stopwatch.GetElapsedTime().TotalMilliseconds,
                new KeyValuePair<string, object?>("has.transport", transport != null));
        }
    }

    private async Task BroadcastToLocalChannels(TModel model, TMetadata? broadcasterMetadata) {
        if (localSubscribers.IsEmpty) {
            return;
        }
        var tasks = localSubscribers
            .Select(kvp => {
                // Evaluate filter safely
                bool shouldBroadcast;
                try {
                    shouldBroadcast = kvp.Value.Filter == null || kvp.Value.Filter(broadcasterMetadata);
                } catch (Exception ex) {
                    if (logger.IsEnabled(LogLevel.Warning)) {
                        logger.LogWarning(ex,
                            "Filter threw exception for subscriber {SubscriberId}. Skipping broadcast to this subscriber.",
                            kvp.Key);
                    }
                    shouldBroadcast = false;
                }
                return (kvp, shouldBroadcast);
            })
            .Where(x => x.shouldBroadcast)
            .Select(async x => {
                try {
                    await x.kvp.Value.Channel.Writer.WriteAsync(model);
                } catch (ChannelClosedException) {
                    if (logger.IsEnabled(LogLevel.Debug)) {
                        logger.LogDebug("Channel closed for subscriber {SubscriberId} (client disconnected)", x.kvp.Key);
                    }
                } catch (Exception ex) {
                    if (logger.IsEnabled(LogLevel.Warning)) {
                        logger.LogWarning(ex, "Unexpected error writing to channel for subscriber {SubscriberId}", x.kvp.Key);
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
                    ActivityContext parentContext = default;
                    if (transportMsg.TraceId != null && transportMsg.ParentSpanId != null) {
                        parentContext = new ActivityContext(
                            ActivityTraceId.CreateFromString(transportMsg.TraceId),
                            ActivitySpanId.CreateFromString(transportMsg.ParentSpanId),
                            ActivityTraceFlags.Recorded,
                            isRemote: true
                        );
                    }
                    using var activity = RxTelemetry.ActivitySource.StartActivity(
                        "razorx.sse.broadcast.receive",
                        ActivityKind.Consumer,
                        parentContext);
                    activity?.SetTag("source.server", transportMsg.SourceServerId);
                    activity?.SetTag("channel", broadcastChannel);
                    var model = JsonSerializer.Deserialize(
                        transportMsg.PayloadJson,
                        modelTypeInfo!);
                    if (model == null) {
                        if (logger.IsEnabled(LogLevel.Warning)) {
                            logger.LogWarning("Received null model in transport message on channel {Channel}", broadcastChannel);
                        }
                        continue;
                    }
                    TMetadata? broadcasterMetadata = default;
                    if (transportMsg.BroadcasterMetadataJson != null) {
                        broadcasterMetadata = JsonSerializer.Deserialize(
                            transportMsg.BroadcasterMetadataJson,
                            metadataTypeInfo!);
                    }
                    await BroadcastToLocalChannels(model, broadcasterMetadata);
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
    public async IAsyncEnumerable<TModel> GetUpdates(
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
        var modelType = typeof(TModel).FullName ?? typeof(TModel).Name;
        RxTelemetry.UnregisterSseSubscriberCountCallback(modelType);
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
