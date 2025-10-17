# RazorX.Framework.OpenTelemetry

OpenTelemetry integration for RazorX.Framework providing comprehensive distributed tracing and metrics collection.

## Features

- **Distributed Tracing** - End-to-end visibility across RazorX operations
- **Metrics Collection** - Counters, histograms, and gauges for performance monitoring
- **W3C Trace Context** - Automatic correlation with ASP.NET Core requests and application logs
- **AOT Compatible** - Full Native AOT support with zero trimming warnings
- **Zero Overhead** - Instrumentation always present but only active when listeners registered
- **Configurable** - Fine-grained control over what gets instrumented

## Installation

```bash
dotnet add package RazorX.Framework.OpenTelemetry
```

## Quick Start

```csharp
using RazorX.Framework;
using RazorX.Framework.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Add RazorX as usual
builder.Services.AddRxDriver();

// Add OpenTelemetry with RazorX instrumentation
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("MyApp", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddRxInstrumentation()  // 👈 Add RazorX tracing
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddRxInstrumentation()  // 👈 Add RazorX metrics
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();
// ... rest of configuration
```

## Distributed Tracing

### Spans Captured

| Span Name | Description | Parent | Attributes |
|-----------|-------------|--------|------------|
| `razorx.page.render` | Full page render | HTTP request | component.root, component.page, component.head |
| `razorx.response.build` | Response assembly | HTTP request | fragment.count, trigger.count |
| `razorx.sse.stream` | SSE stream setup | HTTP request | event.type, heartbeat.interval |
| `razorx.sse.broadcast` | SSE broadcast | HTTP request | model.type, has.metadata, has.transport |
| `razorx.sse.transport.publish` | Transport publish | sse.broadcast | channel, server.id |
| `razorx.sse.broadcast.receive` | Transport receive | Remote trace | source.server, channel |

### Trace Context Propagation

The correlation ID returned by `context.GetCorrelationId()` **IS** the OpenTelemetry TraceId:

```csharp
public static async Task<IResult> MyHandler(HttpContext context, IRxDriver rxDriver) {
    var correlationId = context.GetCorrelationId();
    // correlationId == Activity.Current.TraceId.ToString()
    // Same value appears in:
    // - All OpenTelemetry spans
    // - All structured logs
    // - Distributed traces across servers
}
```

## Metrics Collected

### Counters
- `razorx.request.count` - Requests by operation (page, fragment, sse)
- `razorx.sse.broadcast.count` - Broadcasts by model type
- `razorx.antiforgery.validation` - CSRF validations (success/failure)
- `razorx.memory.pool.rent` - Buffer allocations (disabled by default)
- `razorx.memory.pool.return` - Buffer returns (disabled by default)

### Histograms
- `razorx.render.duration` - Render times by operation
- `razorx.fragment.count` - Fragments per request
- `razorx.sse.broadcast.duration` - Broadcast latency
- `razorx.sse.broadcast.subscriber.count` - Subscribers per broadcast

### Gauges
- `razorx.sse.subscriber.count` - Current SSE connections by model type

## Configuration

### Tracing Options

```csharp
.WithTracing(tracing => tracing
    .AddRxInstrumentation(options => {
        options.RecordPageRenders = true;
        options.RecordBroadcasts = true;
        options.RecordSseStreams = true;
        options.RecordTransportPublish = true;
        options.RecordTransportReceive = true;
        options.RecordResponseBuilds = true;
    }))
```

### Metrics Options

```csharp
.WithMetrics(metrics => metrics
    .AddRxInstrumentation(options => {
        options.EnableRenderMetrics = true;
        options.EnableSseMetrics = true;
        options.EnableAntiforgeryMetrics = true;
        options.EnableMemoryPoolMetrics = false; // High volume!
        options.EnableHistograms = true;
    }))
```

## Distributed Trace Propagation

RazorX automatically propagates trace context across servers when using distributed SSE:

```
[Server A] POST /todos
  └─ [Server A] razorx.sse.broadcast (TraceId: abc123)
     └─ [Server A] razorx.sse.transport.publish
        ↓ (Redis/Service Bus carries TraceId)
        └─ [Server B] razorx.sse.broadcast.receive (Links to TraceId: abc123)
           └─ [Server B] Delivers to SSE clients
```

All operations share the **same W3C TraceId**, enabling end-to-end observability.

## Performance Impact

### When OpenTelemetry NOT Configured
- ActivitySource.StartActivity(): **~1-2ns** (returns null immediately)
- Meter operations: **~1-2ns** (noop when no listener)
- ValueStopwatch timing: **~20ns** (zero allocations)
- **Total overhead: ~20ns per request** (imperceptible)

### When OpenTelemetry Enabled
- Span creation/disposal: **~500-1000ns** per span
- Metric recording: **~100-400ns** per sample
- ValueStopwatch timing: **~20ns** (zero allocations)
- **Total overhead: ~5-10μs per request** (very low)

**Performance Note:** RazorX uses `ValueStopwatch` (struct-based, zero-allocation timing) following ASP.NET Core's pattern. This is 50% faster than `Stopwatch` class with zero heap allocations.

### Memory Pools
- Memory pool metrics: **~10,000-50,000 samples/sec** under load
- **Disabled by default** to prevent metric explosion
- Enable only for short-term memory diagnostics

## Example: Jaeger Integration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddRxInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddJaegerExporter(options => {
            options.AgentHost = "localhost";
            options.AgentPort = 6831;
        }));
```

Then run Jaeger locally:
```bash
docker run -d --name jaeger \
  -p 6831:6831/udp \
  -p 16686:16686 \
  jaegertracing/all-in-one:latest
```

Visit http://localhost:16686 to explore traces.

## Example: OTLP/gRPC (Production)

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddRxInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter(options => {
            options.Endpoint = new Uri("https://otel-collector:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddRxInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

## Compatibility

- **.NET 10** - Required (uses latest OpenTelemetry features)
- **RazorX.Framework** - 1.0.0-alpha or higher
- **OpenTelemetry** - 1.10.0 or higher
- **Native AOT** - Fully supported

## Resources

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [RazorX.Framework Documentation](https://github.com/ranzlee/razorx-framework)
- [W3C Trace Context Specification](https://www.w3.org/TR/trace-context/)

## License

MIT License - See LICENSE file in repository root
