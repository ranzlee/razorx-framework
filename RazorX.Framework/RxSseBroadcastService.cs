using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;

namespace RazorX.Framework;

/// <summary>
/// Provides a thread-safe broadcast service for distributing SSE updates to multiple connected clients.
/// </summary>
/// <typeparam name="T">The model type to broadcast to subscribers.</typeparam>
/// <remarks>
/// <para>
/// This is an optional utility for scenarios where multiple clients should receive the same real-time updates,
/// such as live dashboards, notifications, collaborative editing, or activity feeds.
/// </para>
/// <para>
/// <strong>Registration:</strong> Register as singleton in dependency injection:
/// <code>builder.Services.AddSingleton&lt;RxSseBroadcastService&lt;MyModel&gt;&gt;();</code>
/// </para>
/// <para>
/// <strong>Architecture:</strong> Each subscriber receives an isolated Channel for thread-safe message delivery.
/// When BroadcastUpdate() is called, the update is written to ALL subscriber channels in parallel.
/// </para>
/// <para>
/// <strong>Limitations:</strong> This is a simple in-memory broadcast utility with no authorization,
/// user-scoping, or filtering. For advanced scenarios (user-specific channels, role-based access,
/// topic routing), wrap this service or implement a custom broadcast service.
/// </para>
/// <para>
/// <strong>Memory Management:</strong> Always call Unsubscribe() when done, or use CancellationToken.Register()
/// for automatic cleanup when clients disconnect.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Initialize subscriber ID in initial page load (session auto-commits at request end)
/// public static async Task&lt;IResult&gt; GetPage(HttpContext context, IRxDriver rxDriver) {
///     var subscriberId = context.Session.GetString("SubscriberId");
///     if (string.IsNullOrWhiteSpace(subscriberId)) {
///         subscriberId = Guid.NewGuid().ToString();
///         context.Session.SetString("SubscriberId", subscriberId);
///     }
///     return await rxDriver.RenderPage&lt;Layout, Page&gt;(context);
/// }
///
/// // In SSE handler - Subscribe using session-based ID
/// public static IResult StreamUpdates(
///     HttpContext context,
///     IRxDriver rxDriver,
///     RxSseBroadcastService&lt;TodoModel&gt; broadcast,
///     CancellationToken ct)
/// {
///     var subscriberId = context.Session.GetString("SubscriberId")!;
///
///     if (!broadcast.HasSubscriber(subscriberId)) {
///         broadcast.Subscribe(subscriberId);
///         ct.Register(() => broadcast.Unsubscribe(subscriberId));
///     }
///
///     return rxDriver
///         .With(context)
///         .RenderSse(
///             broadcast.GetUpdates(subscriberId, ct),
///             async (todo, builder) => builder.AddFragment&lt;TodoCard&gt;(todo, "todo-list", Beforeend),
///             ct
///         );
/// }
///
/// // In regular handler - Broadcast with echo suppression
/// public static async Task&lt;IResult&gt; UpdateTodo(
///     HttpContext context,
///     IRxDriver rxDriver,
///     RxSseBroadcastService&lt;TodoModel&gt; broadcast,
///     TodoModel todo)
/// {
///     await repository.SaveAsync(todo);
///
///     // Exclude triggering client to prevent echo
///     var excludeId = context.Session.GetString("SubscriberId");
///     await broadcast.BroadcastUpdate(todo, excludeId);
///
///     return await rxDriver
///         .With(context)
///         .AddFragment&lt;TodoCard&gt;(todo, $"todo-{todo.Id}", Swap)
///         .Render();
/// }
/// </code>
/// </example>
public sealed class RxSseBroadcastService<T> : IDisposable {
    private readonly ConcurrentDictionary<string, Channel<T>> _localSubscribers = new();
    private readonly IRxBroadcastTransport? _transport;
    private readonly JsonTypeInfo<T>? _modelTypeInfo;
    private readonly string _broadcastChannel;
    private readonly string _serverId;
    private CancellationTokenSource? _transportCts;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of RxSseBroadcastService.
    /// </summary>
    /// <param name="transport">Optional distributed transport for multi-server broadcasts. If null, operates in in-memory mode only.</param>
    /// <param name="modelTypeInfo">Required JsonTypeInfo for AOT-compatible serialization when using distributed transport.</param>
    /// <param name="config">Optional configuration for server instance ID.</param>
    /// <exception cref="ArgumentException">Thrown when transport is provided but modelTypeInfo is null.</exception>
    /// <remarks>
    /// <para>
    /// When transport is null (default), the service operates in in-memory mode with no cross-server broadcasting.
    /// This is the backward-compatible behavior.
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
        IRxBroadcastTransport? transport = null,
        JsonTypeInfo<T>? modelTypeInfo = null,
        IConfiguration? config = null)
    {
        // Require JsonTypeInfo when using distributed transport for AOT compatibility
        if (transport != null && modelTypeInfo == null) {
            throw new ArgumentException(
                $"JsonTypeInfo<{typeof(T).Name}> is required for AOT compatibility when using distributed transport. " +
                "Provide it during service registration with AddRxSseBroadcast().",
                nameof(modelTypeInfo));
        }

        _transport = transport;
        _modelTypeInfo = modelTypeInfo;
        _broadcastChannel = $"rx-broadcast:{typeof(T).FullName}";
        _serverId = config?["ServerInstanceId"] ?? Environment.MachineName;

        // Start listening for distributed broadcasts if transport is configured
        if (_transport != null) {
            _transportCts = new CancellationTokenSource();
            _ = ListenToTransportAsync();
        }
    }

    /// <summary>
    /// Subscribes a client with the specified subscriber ID.
    /// </summary>
    /// <param name="subscriberId">The subscriber ID to register (typically from session, user ID, or application-generated identifier).</param>
    /// <returns>True if the subscriber was added; false if a subscriber with this ID already exists.</returns>
    /// <remarks>
    /// <para>
    /// Each subscriber receives an isolated unbounded channel for message delivery.
    /// The subscriber ID is application-provided (not generated by this service).
    /// </para>
    /// <para>
    /// <strong>Recommended pattern:</strong> Use session-based IDs initialized in RenderPage:
    /// <code>
    /// // In RenderPage (session auto-commits at request end)
    /// var id = context.Session.GetString("SubscriberId");
    /// if (string.IsNullOrWhiteSpace(id)) {
    ///     id = Guid.NewGuid().ToString();
    ///     context.Session.SetString("SubscriberId", id);
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Always call Unsubscribe() when done, or use CancellationToken.Register() for automatic cleanup.
    /// </para>
    /// </remarks>
    public bool Subscribe(string subscriberId) {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RxSseBroadcastService<>));
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions {
            SingleReader = true,   // Each channel has one reader (the SSE stream)
            SingleWriter = false   // Multiple writers (broadcast can write to all channels)
        });
        return _localSubscribers.TryAdd(subscriberId, channel);
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
        ObjectDisposedException.ThrowIf(_disposed, nameof(RxSseBroadcastService<>));
        if (_localSubscribers.TryRemove(subscriberId, out var channel)) {
            try {
                channel.Writer.Complete();
            } catch {
                // Channel may already be completed - ignore
            }
        }
    }

    /// <summary>
    /// Broadcasts an update to ALL connected subscribers in parallel.
    /// </summary>
    /// <param name="model">The model to broadcast to all subscribers.</param>
    /// <param name="excludeSubscriberId">Optional subscriber ID to exclude from the broadcast (typically the client that triggered the update).</param>
    /// <remarks>
    /// <para>
    /// Writes to all subscriber channels in parallel using Task.WhenAll.
    /// If a channel fails (subscriber disconnected), the error is caught and doesn't affect other subscribers.
    /// </para>
    /// <para>
    /// <strong>Echo Suppression:</strong> Use excludeSubscriberId to prevent the triggering client from receiving
    /// their own update via SSE (they already received it via the AJAX response). This is the recommended pattern
    /// for avoiding duplicate updates.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong> Safe to call from multiple threads simultaneously.
    /// </para>
    /// <para>
    /// <strong>Performance:</strong> Completes when ALL channels have received the message (parallel write).
    /// </para>
    /// </remarks>
    public async Task BroadcastUpdate(T model, string? excludeSubscriberId = null) {
        ArgumentNullException.ThrowIfNull(model);
        ObjectDisposedException.ThrowIf(_disposed, nameof(RxSseBroadcastService<>));

        // 1. ALWAYS deliver to local channels first for low latency
        await BroadcastToLocalChannels(model, excludeSubscriberId);

        // 2. Publish to distributed transport for other servers (if configured)
        if (_transport != null && _modelTypeInfo != null) {
            try {
                // Double serialization pattern for AOT safety:
                // Step 1: Serialize user model with user-provided JsonTypeInfo
                string modelJson = JsonSerializer.Serialize(model, _modelTypeInfo);

                // Step 2: Wrap in transport message with metadata
                var transportMsg = new TransportMessage(
                    PayloadJson: modelJson,
                    ExcludeSubscriberId: excludeSubscriberId,
                    SourceServerId: _serverId,
                    TimestampUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                );

                // Step 3: Serialize transport message with framework's JsonSerializerContext
                string transportJson = JsonSerializer.Serialize(
                    transportMsg,
                    RxJsonSerializerContext.Default.TransportMessage);

                // Step 4: Publish to transport (non-blocking)
                await _transport.PublishAsync(_broadcastChannel, transportJson);
            } catch (Exception ex) {
                // Log transport errors but don't fail the local broadcast
                Console.WriteLine($"Transport publish error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Delivers a broadcast to all local subscribers on this server.
    /// </summary>
    private async Task BroadcastToLocalChannels(T model, string? excludeSubscriberId) {
        if (_localSubscribers.IsEmpty) {
            return;  // No local subscribers
        }

        var tasks = _localSubscribers
            .Where(kvp => excludeSubscriberId == null || kvp.Key != excludeSubscriberId)
            .Select(async kvp => {
                try {
                    await kvp.Value.Writer.WriteAsync(model);
                } catch (ChannelClosedException) {
                    // Channel was completed/closed - subscriber disconnected
                    // This is expected and normal, don't propagate exception
                } catch {
                    // Unexpected error - don't fail entire broadcast
                    // Individual channel errors don't affect other subscribers
                }
            });
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Background task that listens for broadcasts from other servers via the transport.
    /// </summary>
    private async Task ListenToTransportAsync() {
        try {
            await foreach (var transportJson in _transport!.SubscribeAsync(
                _broadcastChannel,
                _transportCts!.Token))
            {
                try {
                    // Step 1: Deserialize transport message with framework's context
                    var transportMsg = JsonSerializer.Deserialize(
                        transportJson,
                        RxJsonSerializerContext.Default.TransportMessage);

                    if (transportMsg == null) {
                        continue;
                    }

                    // Skip our own messages (already delivered locally for lower latency)
                    if (transportMsg.SourceServerId == _serverId) {
                        continue;
                    }

                    // Step 2: Deserialize user model with user-provided JsonTypeInfo
                    var model = JsonSerializer.Deserialize(
                        transportMsg.PayloadJson,
                        _modelTypeInfo!);

                    if (model != null) {
                        // Deliver to THIS server's connected clients
                        await BroadcastToLocalChannels(model, transportMsg.ExcludeSubscriberId);
                    }
                } catch (JsonException ex) {
                    // Log deserialization errors but continue processing
                    Console.WriteLine($"Failed to deserialize transport message: {ex.Message}");
                } catch (Exception ex) {
                    // Log unexpected errors but keep listening
                    Console.WriteLine($"Error processing transport message: {ex.Message}");
                }
            }
        } catch (OperationCanceledException) {
            // Normal shutdown via cancellation token
        } catch (Exception ex) {
            // Critical transport error - log but don't crash the service
            Console.WriteLine($"Transport listener error: {ex.Message}");
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
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RxSseBroadcastService<>));
        if (!_localSubscribers.TryGetValue(subscriberId, out var channel)) {
            yield break;  // Subscriber not found
        }
        await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken)) {
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
        ObjectDisposedException.ThrowIf(_disposed, nameof(RxSseBroadcastService<>));
        return _localSubscribers.Count;
    }

    /// <summary>
    /// Gets a list of all active subscriber IDs.
    /// </summary>
    /// <returns>Read-only list of subscriber IDs.</returns>
    /// <remarks>
    /// Useful for diagnostics and debugging. The list is a snapshot at the time of the call.
    /// </remarks>
    public IReadOnlyList<string> GetActiveSubscribers() {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RxSseBroadcastService<>));
        return [.. _localSubscribers.Keys];
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
        ObjectDisposedException.ThrowIf(_disposed, nameof(RxSseBroadcastService<>));
        return _localSubscribers.ContainsKey(subscriberId);
    }

    /// <summary>
    /// Disposes the broadcast service and completes all subscriber channels.
    /// </summary>
    /// <remarks>
    /// Called automatically when the application shuts down (registered as singleton).
    /// Completes all channels gracefully, allowing pending reads to finish.
    /// </remarks>
    public void Dispose() {
        if (_disposed) {
            return;
        }

        // Stop listening to transport
        try {
            _transportCts?.Cancel();
            _transportCts?.Dispose();
        } catch {
            // Ignore cancellation errors during disposal
        }

        // Complete all local channels
        foreach (var channel in _localSubscribers.Values) {
            try {
                channel.Writer.Complete();
            } catch {
                // Ignore errors during disposal
            }
        }
        _localSubscribers.Clear();

        // Dispose transport
        try {
            _transport?.Dispose();
        } catch {
            // Ignore transport disposal errors
        }

        _disposed = true;
    }
}
