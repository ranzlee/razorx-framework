using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework.Azure;

/// <summary>
/// Extension methods for configuring Azure Service Bus transport for RxSseBroadcastService.
/// </summary>
public static class ServiceBusTransportExtensions {
    /// <summary>
    /// Configures the broadcast service to use Azure Service Bus for distributed broadcasts.
    /// </summary>
    /// <param name="options">Transport options to configure.</param>
    /// <param name="connectionString">Service Bus namespace connection string.</param>
    /// <param name="topicNamePrefix">Optional prefix for topic names (default: "rx-broadcast").</param>
    /// <remarks>
    /// <para>
    /// <strong>Connection String Format:</strong>
    /// Endpoint=sb://NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=POLICY;SharedAccessKey=KEY
    /// </para>
    /// <para>
    /// Get from Azure Portal: Service Bus Namespace → Shared access policies → RootManageSharedAccessKey
    /// </para>
    /// <para>
    /// <strong>Required Permissions:</strong>
    /// - Send (publish messages)
    /// - Listen (subscribe to messages)
    /// - Manage (create topics/subscriptions if needed)
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TenantMetadata,
    ///     options => options.UseServiceBus(
    ///         builder.Configuration["Azure:ServiceBus:ConnectionString"]));
    /// </code>
    /// </example>
    public static void UseServiceBus(
        this RxBroadcastTransportOptions options,
        string connectionString,
        string? topicNamePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        options.UseCustomTransport(sp => {
            // Create Service Bus client
            var client = new ServiceBusClient(connectionString);

            // Register as singleton for reuse
            sp.GetRequiredService<IServiceCollection>()
                .TryAddSingleton(client);

            var logger = sp.GetRequiredService<ILogger<ServiceBusBroadcastTransport>>();
            return new ServiceBusBroadcastTransport(client, logger, topicNamePrefix);
        });
    }

    /// <summary>
    /// Configures the broadcast service to use an existing ServiceBusClient from DI.
    /// </summary>
    /// <param name="options">Transport options to configure.</param>
    /// <param name="topicNamePrefix">Optional prefix for topic names.</param>
    /// <remarks>
    /// Use this overload when you've already registered ServiceBusClient in your DI container.
    /// This is the preferred approach when sharing Service Bus connections across services.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Register Service Bus client
    /// builder.Services.AddAzureClients(clients => {
    ///     clients.AddServiceBusClient(
    ///         builder.Configuration["Azure:ServiceBus:ConnectionString"]);
    /// });
    ///
    /// // Use existing client with metadata filtering
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TenantMetadata,
    ///     options => options.UseServiceBus());
    /// </code>
    /// </example>
    public static void UseServiceBus(
        this RxBroadcastTransportOptions options,
        string? topicNamePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        options.UseCustomTransport(sp => {
            var client = sp.GetRequiredService<ServiceBusClient>();
            var logger = sp.GetRequiredService<ILogger<ServiceBusBroadcastTransport>>();
            return new ServiceBusBroadcastTransport(client, logger, topicNamePrefix);
        });
    }

    /// <summary>
    /// Configures the broadcast service to use Azure Service Bus with Managed Identity.
    /// </summary>
    /// <param name="options">Transport options to configure.</param>
    /// <param name="fullyQualifiedNamespace">The Service Bus namespace (e.g., "myns.servicebus.windows.net").</param>
    /// <param name="topicNamePrefix">Optional prefix for topic names.</param>
    /// <remarks>
    /// <para>
    /// This method uses DefaultAzureCredential for authentication, supporting:
    /// - Managed Identity (Azure VM, App Service, Functions, AKS)
    /// - Visual Studio credentials (local development)
    /// - Azure CLI credentials (local development)
    /// - Environment variables
    /// </para>
    /// <para>
    /// <strong>Required Azure RBAC Roles:</strong>
    /// - Azure Service Bus Data Sender
    /// - Azure Service Bus Data Receiver
    /// </para>
    /// <para>
    /// <strong>Recommended for Production:</strong> Use Managed Identity instead of connection strings.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddRxSseBroadcast&lt;TodoModel, TenantMetadata&gt;(
    ///     MyAppJsonContext.Default.TodoModel,
    ///     MyAppJsonContext.Default.TenantMetadata,
    ///     options => options.UseServiceBusWithManagedIdentity(
    ///         "myns.servicebus.windows.net"));
    /// </code>
    /// </example>
    public static void UseServiceBusWithManagedIdentity(
        this RxBroadcastTransportOptions options,
        string fullyQualifiedNamespace,
        string? topicNamePrefix = null)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedNamespace, nameof(fullyQualifiedNamespace));

        options.UseCustomTransport(sp => {
            // Use DefaultAzureCredential for Managed Identity authentication
            var client = new ServiceBusClient(
                fullyQualifiedNamespace,
                new DefaultAzureCredential());

            // Register as singleton
            sp.GetRequiredService<IServiceCollection>()
                .TryAddSingleton(client);

            var logger = sp.GetRequiredService<ILogger<ServiceBusBroadcastTransport>>();
            return new ServiceBusBroadcastTransport(client, logger, topicNamePrefix);
        });
    }
}
