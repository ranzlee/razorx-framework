namespace RazorX.Framework.OpenTelemetry;

/// <summary>
/// Configuration options for RazorX.Framework distributed tracing instrumentation.
/// </summary>
/// <remarks>
/// <para>
/// These options control which OpenTelemetry spans are created by RazorX.Framework.
/// All options default to true for maximum observability, but can be disabled
/// to reduce tracing overhead or cardinality in high-throughput scenarios.
/// </para>
/// <para>
/// <strong>Note:</strong> RazorX spans are child spans of ASP.NET Core's HTTP request span.
/// The W3C TraceId flows automatically through all spans, ensuring complete correlation
/// between RazorX operations, HTTP requests, and application logs.
/// </para>
/// </remarks>
public sealed class RxTracingOptions {
    /// <summary>
    /// Record spans for full page renders (RenderPage operations).
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Span: razorx.page.render
    /// </para>
    /// <para>
    /// Attributes:
    /// - component.root (root component type)
    /// - component.head (head component type, if present)
    /// - component.page (page component type)
    /// </para>
    /// </remarks>
    public bool RecordPageRenders { get; set; } = true;
    /// <summary>
    /// Record spans for SSE broadcast operations.
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Span: razorx.sse.broadcast
    /// </para>
    /// <para>
    /// Attributes:
    /// - model.type (model being broadcast)
    /// - has.metadata (whether broadcaster metadata included)
    /// - has.transport (whether using distributed transport)
    /// </para>
    /// </remarks>
    public bool RecordBroadcasts { get; set; } = true;
    /// <summary>
    /// Record spans for SSE stream setup operations.
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Span: razorx.sse.stream
    /// </para>
    /// <para>
    /// Attributes:
    /// - event.type (SSE event type name)
    /// - heartbeat.interval (heartbeat interval in seconds, or "none")
    /// </para>
    /// <para>
    /// Note: This span covers SSE stream initialization, not the entire connection lifetime.
    /// The full connection is covered by ASP.NET Core's HTTP request span.
    /// </para>
    /// </remarks>
    public bool RecordSseStreams { get; set; } = true;
    /// <summary>
    /// Record spans for distributed transport publish operations.
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Span: razorx.sse.transport.publish (child of razorx.sse.broadcast)
    /// </para>
    /// <para>
    /// Attributes:
    /// - channel (broadcast channel name)
    /// - server.id (source server ID)
    /// </para>
    /// <para>
    /// Only relevant when using distributed SSE with transport (Redis, Azure Service Bus).
    /// Enables tracing broadcasts across server boundaries.
    /// </para>
    /// </remarks>
    public bool RecordTransportPublish { get; set; } = true;
    /// <summary>
    /// Record spans for distributed transport receive operations.
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Span: razorx.sse.broadcast.receive
    /// </para>
    /// <para>
    /// Attributes:
    /// - source.server (originating server ID)
    /// - channel (broadcast channel name)
    /// </para>
    /// <para>
    /// This span recreates the distributed trace context from remote servers,
    /// linking the receiving operation back to the original broadcast trace.
    /// Only created when receiving broadcasts from distributed transport.
    /// </para>
    /// </remarks>
    public bool RecordTransportReceive { get; set; } = true;
    /// <summary>
    /// Record spans for response build operations.
    /// </summary>
    /// <remarks>
    /// <para>Default: true</para>
    /// <para>
    /// Span: razorx.response.build
    /// </para>
    /// <para>
    /// Attributes:
    /// - fragment.count (number of fragments in response)
    /// - trigger.count (number of triggers in response)
    /// </para>
    /// <para>
    /// This span includes the time to await all fragment render tasks (Task.WhenAll)
    /// and assemble the final response with headers.
    /// </para>
    /// </remarks>
    public bool RecordResponseBuilds { get; set; } = true;
}
