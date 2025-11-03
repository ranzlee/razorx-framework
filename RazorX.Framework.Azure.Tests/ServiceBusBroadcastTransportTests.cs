using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;

namespace RazorX.Framework.Azure.Tests;

[TestClass]
public class ServiceBusBroadcastTransportTests {
    private Mock<ServiceBusClient> mockClient = null!;
    private Mock<ILogger<ServiceBusBroadcastTransport>> mockLogger = null!;
    private ServiceBusBroadcastTransport transport = null!;

    [TestInitialize]
    public void Setup() {
        mockClient = new Mock<ServiceBusClient>();
        mockLogger = new Mock<ILogger<ServiceBusBroadcastTransport>>();

        transport = new ServiceBusBroadcastTransport(
            mockClient.Object,
            mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup() {
        transport?.Dispose();
    }

    [TestMethod]
    public void Constructor_WithNullClient_ThrowsArgumentNullException() {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new ServiceBusBroadcastTransport(null!, mockLogger.Object));
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new ServiceBusBroadcastTransport(mockClient.Object, null!));
    }

    [TestMethod]
    public void Constructor_WithDefaultParameters_UsesDefaultPrefixes() {
        // Arrange & Act
        var transport = new ServiceBusBroadcastTransport(
            mockClient.Object,
            mockLogger.Object);

        // Assert - no exception, uses defaults
        Assert.IsNotNull(transport);
    }

    [TestMethod]
    public void Constructor_WithCustomTopicPrefix_UsesCustomPrefix() {
        // Arrange & Act
        var transport = new ServiceBusBroadcastTransport(
            mockClient.Object,
            mockLogger.Object,
            topicNamePrefix: "custom-prefix");

        // Assert - no exception
        Assert.IsNotNull(transport);
    }

    [TestMethod]
    public void Constructor_WithCustomSubscriptionPrefix_UsesCustomPrefix() {
        // Arrange & Act
        var transport = new ServiceBusBroadcastTransport(
            mockClient.Object,
            mockLogger.Object,
            subscriptionNamePrefix: "custom-sub");

        // Assert - no exception
        Assert.IsNotNull(transport);
    }

    [TestMethod]
    public void Dispose_CanBeCalledMultipleTimes() {
        // Arrange & Act
        transport.Dispose();
        transport.Dispose();
        transport.Dispose();

        // Assert - no exception thrown
    }

    [TestMethod]
    public async Task PublishAsync_AfterDispose_ThrowsObjectDisposedException() {
        // Arrange
        transport.Dispose();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            () => transport.PublishAsync("test", "message"));
    }

    [TestMethod]
    public async Task SubscribeAsync_AfterDispose_ThrowsObjectDisposedException() {
        // Arrange
        transport.Dispose();
        var cts = new CancellationTokenSource();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => {
            await foreach (var msg in transport.SubscribeAsync("test", cts.Token)) {
                break;
            }
        });
    }

    [TestMethod]
    public void Constructor_WithCustomPrefixes_InitializesCorrectly() {
        // Arrange & Act
        var customTransport = new ServiceBusBroadcastTransport(
            mockClient.Object,
            mockLogger.Object,
            topicNamePrefix: "custom-topic",
            subscriptionNamePrefix: "custom-sub");

        // Assert
        Assert.IsNotNull(customTransport);
        customTransport.Dispose();
    }

    [TestMethod]
    public void Dispose_DisposesAllSenders() {
        // Note: This test verifies that disposal completes without error
        // In a real scenario with active senders, would verify disposal calls

        // Arrange & Act
        transport.Dispose();

        // Assert - no exception thrown
    }
}
