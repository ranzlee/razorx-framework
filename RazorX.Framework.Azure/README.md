# RazorX.Framework.Azure

Azure Service Bus transport for distributed RazorX.Framework SSE broadcasts with enterprise-grade messaging features.

## Features

- ✅ Guaranteed delivery (at-least-once)
- ✅ Message persistence (up to 80 days)
- ✅ Dead-letter queue support
- ✅ Managed Identity authentication
- ✅ Message ordering (with sessions)
- ✅ Duplicate detection
- ✅ 100% Native AOT compatible
- ✅ OpenTelemetry distributed tracing support

## Installation

```bash
dotnet add package RazorX.Framework.Azure
```

## Quick Start

### Connection String

```csharp
// Program.cs
builder.Services.AddRxSseBroadcast<TodoModel, TenantMetadata>(
    MyAppJsonContext.Default.TodoModel,
    MyAppJsonContext.Default.TenantMetadata,
    options => options.UseServiceBus(
        builder.Configuration["Azure:ServiceBus:ConnectionString"]));
```

### Managed Identity (Recommended for Production)

```csharp
builder.Services.AddRxSseBroadcast<TodoModel, TenantMetadata>(
    MyAppJsonContext.Default.TodoModel,
    MyAppJsonContext.Default.TenantMetadata,
    options => options.UseServiceBusWithManagedIdentity(
        "myns.servicebus.windows.net"));
```

## Configuration

### Using Existing ServiceBusClient

```csharp
// Register Service Bus client
builder.Services.AddAzureClients(clients => {
    clients.AddServiceBusClient(
        builder.Configuration["Azure:ServiceBus:ConnectionString"]);
});

// Use existing client
builder.Services.AddRxSseBroadcast<TodoModel, TenantMetadata>(
    MyAppJsonContext.Default.TodoModel,
    MyAppJsonContext.Default.TenantMetadata,
    options => options.UseServiceBus());
```

### Custom Topic Prefix

```csharp
options => options.UseServiceBus(
    connectionString,
    topicNamePrefix: "myapp-broadcast")
```

## Azure Setup

### Create Service Bus Namespace (Azure CLI)

```bash
# Create resource group
az group create --name razorx-rg --location eastus

# Create Service Bus namespace (Standard tier)
az servicebus namespace create \
  --name razorx-servicebus \
  --resource-group razorx-rg \
  --location eastus \
  --sku Standard

# Get connection string
az servicebus namespace authorization-rule keys list \
  --resource-group razorx-rg \
  --namespace-name razorx-servicebus \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

### Configure Managed Identity (Recommended)

```bash
# Assign RBAC roles to your App Service / Function
az role assignment create \
  --assignee <APP_SERVICE_PRINCIPAL_ID> \
  --role "Azure Service Bus Data Sender" \
  --scope /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/razorx-rg/providers/Microsoft.ServiceBus/namespaces/razorx-servicebus

az role assignment create \
  --assignee <APP_SERVICE_PRINCIPAL_ID> \
  --role "Azure Service Bus Data Receiver" \
  --scope /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/razorx-rg/providers/Microsoft.ServiceBus/namespaces/razorx-servicebus
```

## Performance Characteristics

| Metric | Standard Tier | Premium Tier |
|--------|---------------|--------------|
| Publish latency (P50) | 10-20ms | 5-15ms |
| Publish latency (P99) | 30-60ms | 20-40ms |
| Subscribe delivery (P50) | 20-40ms | 10-30ms |
| Subscribe delivery (P99) | 50-100ms | 30-70ms |
| Throughput | 2,000 msg/sec | 100,000+ msg/sec |
| Message size limit | 256KB | 1MB |

## Cost Estimation

**Standard Tier:**
- Base cost: ~$10/month
- Operations: $0.05 per million
- Example: 10M msgs/month = ~$10.50/month

**Premium Tier:**
- Dedicated resources: ~$670/month (1 messaging unit)
- Unlimited operations (flat rate)
- Recommended for >100M messages/month

## When to Use

Choose Azure Service Bus when:
- ✅ Guaranteed delivery is required
- ✅ Message persistence/replay is needed
- ✅ Enterprise compliance requirements
- ✅ Dead-letter queue support needed
- ✅ Message ordering is important

Consider Redis when:
- ⚠️ Real-time, low-latency is critical (<10ms)
- ⚠️ Messages are ephemeral
- ⚠️ Cost optimization is important

## Production Recommendations

### Security

- ✅ Use Managed Identity (avoid connection strings in code)
- ✅ Store connection strings in Azure Key Vault
- ✅ Enable SSL/TLS (automatic with Azure SDK)
- ✅ Configure network rules/private endpoints

### Monitoring

Monitor these metrics in Azure Portal:
- Active connections
- Incoming/outgoing messages
- Message throughput
- Dead-letter queue depth
- Server errors

### Scaling

- Use Premium tier for production (dedicated resources)
- Scale up messaging units for higher throughput
- Monitor queue depth and latency
- Set up auto-scaling rules

## Documentation

For complete documentation, see [RazorX.Framework Wiki](https://github.com/ranzlee/razorx-framework/wiki).

## License

MIT - See [LICENSE](https://github.com/ranzlee/razorx-framework/blob/main/LICENSE)
