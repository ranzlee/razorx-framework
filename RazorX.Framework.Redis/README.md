# RazorX.Framework.Redis

Redis Pub/Sub transport for distributed RazorX.Framework SSE broadcasts across multiple servers.

## Features

- ✅ Low-latency real-time broadcasts (1-10ms)
- ✅ At-most-once delivery (fire-and-forget)
- ✅ High throughput (10,000+ msg/sec)
- ✅ Horizontal scaling with Redis Cluster
- ✅ 100% Native AOT compatible
- ✅ OpenTelemetry distributed tracing support

## Installation

```bash
dotnet add package RazorX.Framework.Redis
```

## Quick Start

```csharp
// Program.cs
builder.Services.AddRxSseBroadcast<TodoModel, TenantMetadata>(
    MyAppJsonContext.Default.TodoModel,
    MyAppJsonContext.Default.TenantMetadata,
    options => options.UseRedis("localhost:6379"));
```

## Configuration

### Simple Connection String

```csharp
options => options.UseRedis("localhost:6379")
```

### With Authentication

```csharp
options => options.UseRedis("localhost:6379,password=mypassword")
```

### With SSL

```csharp
options => options.UseRedis("myredis.azure.com:6380,ssl=true,password=mykey")
```

### Advanced Configuration

```csharp
options => {
    var redisConfig = new ConfigurationOptions {
        EndPoints = { "server1:6379", "server2:6379" },
        Password = "mypassword",
        Ssl = true,
        ConnectRetry = 5,
        ConnectTimeout = 10000,
        AbortOnConnectFail = false
    };
    options.UseRedis(redisConfig);
}
```

### Using Existing Connection

```csharp
// Register Redis connection as singleton
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    return ConnectionMultiplexer.Connect(config["Redis:ConnectionString"]);
});

// Use existing connection
builder.Services.AddRxSseBroadcast<TodoModel, TenantMetadata>(
    MyAppJsonContext.Default.TodoModel,
    MyAppJsonContext.Default.TenantMetadata,
    options => options.UseRedis());
```

## Performance Characteristics

| Metric | Value |
|--------|-------|
| Publish latency (P50) | 1-3ms |
| Publish latency (P99) | 5-10ms |
| Subscribe delivery (P50) | 5-10ms |
| Subscribe delivery (P99) | 15-25ms |
| Throughput (single Redis) | 10,000+ msg/sec |
| Memory overhead per subscription | ~5KB |

## When to Use

Choose Redis transport when:
- ✅ Real-time, low-latency is critical
- ✅ Messages are ephemeral (no replay needed)
- ✅ Cost optimization is important
- ✅ You need high throughput

Consider Azure Service Bus when:
- ⚠️ Guaranteed delivery is required
- ⚠️ Message persistence/replay is needed
- ⚠️ Enterprise compliance requirements

## Production Deployment

### High Availability

```yaml
# Use Redis Sentinel for automatic failover
redis-sentinel:
  - sentinel monitor mymaster redis-1 6379 2
  - sentinel down-after-milliseconds mymaster 5000
  - sentinel parallel-syncs mymaster 1
  - sentinel failover-timeout mymaster 10000
```

### Scaling

- Use Redis Cluster for horizontal scaling
- Shard channels across nodes if needed
- Monitor Pub/Sub channel utilization

### Monitoring

Monitor these metrics:
- Connection pool health
- Publish/subscribe latency
- Message throughput
- Memory usage

## Documentation

For complete documentation, see [RazorX.Framework Wiki](https://github.com/ranzlee/razorx-framework/wiki).

## License

MIT - See [LICENSE](https://github.com/ranzlee/razorx-framework/blob/main/LICENSE)
