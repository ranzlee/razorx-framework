namespace RazorX.Framework;

/// <summary>
/// Abstraction for distributed broadcast transport mechanisms (AOT-compatible).
/// </summary>
/// <remarks>
/// <para>
/// This interface defines the contract for transporting broadcast messages across multiple
/// servers in a distributed deployment. Implementations handle the underlying messaging
/// infrastructure (Redis, Azure Service Bus, RabbitMQ, etc.).
/// </para>
/// <para>
/// <strong>Design for AOT Compatibility:</strong> This interface works with serialized JSON strings
/// rather than generic types, ensuring compatibility with .NET Native AOT compilation.
/// The RxSseBroadcastService handles type-safe serialization/deserialization using
/// user-provided JsonTypeInfo&lt;T&gt;.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Implementations must be thread-safe as they are registered
/// as singletons and shared across all broadcast services.
/// </para>
/// <para>
/// <strong>Error Handling:</strong> Transport failures should not crash the application.
/// Implementations should log errors and implement retry logic where appropriate.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Example Redis implementation
/// public class RedisBroadcastTransport : IRxBroadcastTransport {
///     private readonly IConnectionMultiplexer _redis;
///
///     public async Task PublishAsync(string channel, string jsonMessage, CancellationToken ct) {
///         await _redis.GetSubscriber().PublishAsync(channel, jsonMessage);
///     }
///
///     public async IAsyncEnumerable&lt;string&gt; SubscribeAsync(
///         string channel,
///         [EnumeratorCancellation] CancellationToken ct)
///     {
///         var messageChannel = Channel.CreateUnbounded&lt;string&gt;();
///         await _redis.GetSubscriber().SubscribeAsync(channel, (ch, msg) => {
///             messageChannel.Writer.TryWrite(msg!);
///         });
///         await foreach (var msg in messageChannel.Reader.ReadAllAsync(ct)) {
///             yield return msg;
///         }
///     }
/// }
/// </code>
/// </example>
public interface IRxBroadcastTransport : IDisposable {
    /// <summary>
    /// Publishes a JSON-serialized message to all servers listening on the specified channel.
    /// </summary>
    /// <param name="channel">The channel name to publish to (typically "rx-broadcast:{TypeName}").</param>
    /// <param name="jsonMessage">The JSON-serialized TransportMessage containing the payload and metadata.</param>
    /// <param name="ct">Cancellation token for the publish operation.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <remarks>
    /// <para>
    /// The jsonMessage parameter contains a serialized TransportMessage which wraps:
    /// - The user model payload (already JSON-serialized)
    /// - Metadata (source server ID, exclude subscriber ID, timestamp)
    /// </para>
    /// <para>
    /// Implementations should:
    /// - Be idempotent (multiple publishes of same message should be safe)
    /// - Handle transient failures with retries
    /// - Log errors but not throw unless critical
    /// - Support cancellation via the CancellationToken
    /// </para>
    /// </remarks>
    Task PublishAsync(string channel, string jsonMessage, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to JSON-serialized messages from all servers on the specified channel.
    /// </summary>
    /// <param name="channel">The channel name to subscribe to.</param>
    /// <param name="ct">Cancellation token to stop receiving messages.</param>
    /// <returns>An async enumerable stream of JSON-serialized TransportMessage instances.</returns>
    /// <remarks>
    /// <para>
    /// This method establishes a long-lived subscription to the transport. Messages are
    /// yielded as they arrive from ANY server publishing to the channel (including the local server).
    /// </para>
    /// <para>
    /// The RxSseBroadcastService filters out messages from the local server to avoid
    /// duplicate delivery (local channels already received the message immediately).
    /// </para>
    /// <para>
    /// Implementations should:
    /// - Automatically reconnect on transient failures
    /// - Clean up resources when cancellation is requested
    /// - Preserve message ordering where possible (transport-dependent)
    /// - Handle backpressure appropriately
    /// </para>
    /// <para>
    /// <strong>Message Flow:</strong>
    /// 1. Server A calls BroadcastUpdate()
    /// 2. Server A delivers to local channels immediately
    /// 3. Server A publishes to transport
    /// 4. Servers B, C, D receive via SubscribeAsync()
    /// 5. Servers B, C, D deliver to their local channels
    /// </para>
    /// </remarks>
    IAsyncEnumerable<string> SubscribeAsync(string channel, CancellationToken ct);
}

/// <summary>
/// Internal transport message envelope for distributed broadcasts.
/// </summary>
/// <param name="PayloadJson">The user model serialized to JSON using user-provided JsonTypeInfo.</param>
/// <param name="SourceServerId">The ID of the server that originated this broadcast.</param>
/// <param name="TimestampUnixMs">Unix timestamp in milliseconds when the broadcast was sent.</param>
/// <remarks>
/// <para>
/// This record is serialized using the framework's RxJsonSerializerContext, ensuring AOT compatibility.
/// The PayloadJson field contains the pre-serialized user model, achieving a double-serialization
/// pattern that maintains type safety while supporting source generation.
/// </para>
/// <para>
/// <strong>Why double serialization?</strong>
/// - User model: Serialized with user's JsonSerializerContext (has their types)
/// - TransportMessage: Serialized with framework's RxJsonSerializerContext (has this type)
/// - This separation maintains AOT compatibility without exposing internal types to users
/// </para>
/// <para>
/// <strong>Filtering Note</strong>: This message contains no filter criteria. Filtering is handled
/// locally on each server using predicate functions. Remote servers receive all broadcasts
/// and deliver to all their local subscribers. This is a fundamental limitation of distributed
/// predicate filtering (lambdas cannot be serialized across process boundaries).
/// </para>
/// </remarks>
internal sealed record TransportMessage(
    string PayloadJson,
    string SourceServerId,
    long TimestampUnixMs
);
