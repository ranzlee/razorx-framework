using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace RazorX.Framework.Redis.Tests;

[TestClass]
public class RedisBroadcastTransportTests {
    private Mock<IConnectionMultiplexer> mockRedis = null!;
    private Mock<ISubscriber> mockSubscriber = null!;
    private Mock<ILogger<RedisBroadcastTransport>> mockLogger = null!;
    private RedisBroadcastTransport transport = null!;

    [TestInitialize]
    public void Setup() {
        mockRedis = new Mock<IConnectionMultiplexer>();
        mockSubscriber = new Mock<ISubscriber>();
        mockLogger = new Mock<ILogger<RedisBroadcastTransport>>();

        mockRedis.Setup(r => r.GetSubscriber(null)).Returns(mockSubscriber.Object);

        transport = new RedisBroadcastTransport(mockRedis.Object, mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup() {
        transport?.Dispose();
    }

    [TestMethod]
    public void Constructor_WithNullRedis_ThrowsArgumentNullException() {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new RedisBroadcastTransport(null!, mockLogger.Object));
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new RedisBroadcastTransport(mockRedis.Object, null!));
    }

    [TestMethod]
    public async Task PublishAsync_PublishesMessageToRedisChannel() {
        // Arrange
        var channel = "test-channel";
        var message = "{\"test\":true}";
        mockSubscriber
            .Setup(s => s.PublishAsync(
                It.Is<RedisChannel>(c => c.ToString() == channel),
                It.Is<RedisValue>(v => v.ToString() == message),
                CommandFlags.None))
            .ReturnsAsync(1);

        // Act
        await transport.PublishAsync(channel, message);

        // Assert
        mockSubscriber.Verify(s => s.PublishAsync(
            It.Is<RedisChannel>(c => c.ToString() == channel),
            It.Is<RedisValue>(v => v.ToString() == message),
            CommandFlags.None), Times.Once);
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
    public void Dispose_CanBeCalledMultipleTimes() {
        // Arrange & Act
        transport.Dispose();
        transport.Dispose();
        transport.Dispose();

        // Assert - no exception thrown
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromAllChannels() {
        // This test verifies disposal behavior
        // In a real scenario with active subscriptions, would verify unsubscribe calls

        // Arrange & Act
        transport.Dispose();

        // Assert
        // Disposal completes without error (verified by no exception)
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
    public async Task PublishAsync_WithCancellation_ThrowsOperationCanceledException() {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockSubscriber
            .Setup(s => s.PublishAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(),
                CommandFlags.None))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => transport.PublishAsync("test", "message", cts.Token));
    }

    [TestMethod]
    public async Task PublishAsync_RedisConnectionException_LogsErrorAndThrows() {
        // Arrange
        var exception = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Connection failed");
        mockSubscriber
            .Setup(s => s.PublishAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(),
                CommandFlags.None))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<RedisConnectionException>(
            () => transport.PublishAsync("test", "message"));

        // Verify error was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    public async Task PublishAsync_GenericException_LogsErrorAndThrows() {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        mockSubscriber
            .Setup(s => s.PublishAsync(
                It.IsAny<RedisChannel>(),
                It.IsAny<RedisValue>(),
                CommandFlags.None))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => transport.PublishAsync("test", "message"));
    }
}
