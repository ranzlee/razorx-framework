using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RazorX.Framework.Tests.Mocks;

namespace RazorX.Framework.Tests.Core;

[TestClass]
public class RxSseBroadcastMetadataTests {
    private TestLogger<RxSseBroadcastService<BroadcastTestModel, BroadcastTestMetadata>> _logger = null!;
    private RxSseBroadcastService<BroadcastTestModel, BroadcastTestMetadata> _broadcast = null!;

    [TestInitialize]
    public void SetUp() {
        _logger = new TestLogger<RxSseBroadcastService<BroadcastTestModel, BroadcastTestMetadata>>();
        _broadcast = new RxSseBroadcastService<BroadcastTestModel, BroadcastTestMetadata>(_logger);
    }

    [TestCleanup]
    public void TearDown() {
        _broadcast?.Dispose();
    }

    #region Metadata Storage Tests

    [TestMethod]
    public void Subscribe_WithMetadata_ExtractsSubscriberIdCorrectly() {
        // Arrange
        var metadata = new BroadcastTestMetadata("sub-1", "tenant-1", "Admin");

        // Act
        var result = _broadcast.Subscribe(metadata);

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(_broadcast.HasSubscriber("sub-1"));
        Assert.AreEqual(1, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public void Subscribe_WithMetadata_StoresMetadataCorrectly() {
        // Arrange
        var metadata = new BroadcastTestMetadata("sub-1", "tenant-1", "Admin");

        // Act
        _broadcast.Subscribe(metadata);
        var retrieved = _broadcast.GetSubscriberMetadata("sub-1");

        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreEqual("sub-1", retrieved.SubscriberId);
        Assert.AreEqual("tenant-1", retrieved.TenantId);
        Assert.AreEqual("Admin", retrieved.Role);
    }

    [TestMethod]
    public void Subscribe_WithNullMetadata_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => _broadcast.Subscribe(null!));
    }

    [TestMethod]
    public void Subscribe_WithEmptySubscriberId_ThrowsArgumentException() {
        // Arrange
        var metadata = new BroadcastTestMetadata("", "tenant-1", "Admin");

        // Act & Assert
        var ex = Assert.ThrowsExactly<ArgumentException>(() => _broadcast.Subscribe(metadata));
        Assert.IsTrue(ex.Message.Contains("SubscriberId"));
    }

    [TestMethod]
    public void Subscribe_WithWhitespaceSubscriberId_ThrowsArgumentException() {
        // Arrange
        var metadata = new BroadcastTestMetadata("   ", "tenant-1", "Admin");

        // Act & Assert
        var ex = Assert.ThrowsExactly<ArgumentException>(() => _broadcast.Subscribe(metadata));
        Assert.IsTrue(ex.Message.Contains("SubscriberId"));
    }

    [TestMethod]
    public void Subscribe_DuplicateSubscriberId_ReturnsFalse() {
        // Arrange
        var metadata1 = new BroadcastTestMetadata("sub-1", "tenant-1", "Admin");
        var metadata2 = new BroadcastTestMetadata("sub-1", "tenant-2", "User");

        // Act
        var result1 = _broadcast.Subscribe(metadata1);
        var result2 = _broadcast.Subscribe(metadata2);

        // Assert
        Assert.IsTrue(result1);
        Assert.IsFalse(result2);
        Assert.AreEqual(1, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public void GetAllSubscriberMetadata_ReturnsAllMetadata() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));

        // Act
        var allMetadata = _broadcast.GetAllSubscriberMetadata();

        // Assert
        Assert.AreEqual(3, allMetadata.Count);
        Assert.IsTrue(allMetadata.ContainsKey("sub-1"));
        Assert.IsTrue(allMetadata.ContainsKey("sub-2"));
        Assert.IsTrue(allMetadata.ContainsKey("sub-3"));
        Assert.AreEqual("tenant-1", allMetadata["sub-1"].TenantId);
        Assert.AreEqual("User", allMetadata["sub-2"].Role);
    }

    [TestMethod]
    public void GetSubscriberMetadata_NonExistentSubscriber_ReturnsNull() {
        // Act
        var metadata = _broadcast.GetSubscriberMetadata("non-existent");

        // Assert
        Assert.IsNull(metadata);
    }

    #endregion

    #region Local Filtering Tests

    [TestMethod]
    public async Task BroadcastUpdate_WithNoFilter_DeliversToAllSubscribers() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var tasks = new List<Task> {
            ConsumeMessages("sub-1", receivedMessages),
            ConsumeMessages("sub-2", receivedMessages),
            ConsumeMessages("sub-3", receivedMessages)
        };
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(new BroadcastTestModel("Test message"));
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(3, receivedMessages.Count);
    }

    [TestMethod]
    public async Task BroadcastUpdate_FilterBySubscriberId_ExcludesSpecificSubscriber() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages),
            ConsumeMessagesWithId("sub-3", receivedMessages)
        };
        await Task.Delay(100);

        // Act - exclude sub-2
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Test"),
            filter: meta => meta.SubscriberId != "sub-2"
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
        Assert.IsFalse(receivedMessages.Contains("sub-2"));
        Assert.IsTrue(receivedMessages.Contains("sub-3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_FilterByTenantId_DeliversToTenantOnly() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages),
            ConsumeMessagesWithId("sub-3", receivedMessages)
        };
        await Task.Delay(100);

        // Act - only tenant-1
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Test"),
            filter: meta => meta.TenantId == "tenant-1"
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
        Assert.IsTrue(receivedMessages.Contains("sub-2"));
        Assert.IsFalse(receivedMessages.Contains("sub-3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_FilterByRole_DeliversToRoleOnly() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages),
            ConsumeMessagesWithId("sub-3", receivedMessages)
        };
        await Task.Delay(100);

        // Act - only admins
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Admin alert"),
            filter: meta => meta.Role == "Admin"
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
        Assert.IsFalse(receivedMessages.Contains("sub-2"));
        Assert.IsTrue(receivedMessages.Contains("sub-3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_CombinedFilter_TenantAndRole() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages),
            ConsumeMessagesWithId("sub-3", receivedMessages)
        };
        await Task.Delay(100);

        // Act - tenant-1 admins only
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Test"),
            filter: meta => meta.TenantId == "tenant-1" && meta.Role == "Admin"
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
        Assert.IsFalse(receivedMessages.Contains("sub-2"));
        Assert.IsFalse(receivedMessages.Contains("sub-3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_CombinedFilter_TenantAndExcludeSubscriber() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-1", "Admin"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages),
            ConsumeMessagesWithId("sub-3", receivedMessages)
        };
        await Task.Delay(100);

        // Act - tenant-1, exclude sub-2 (echo suppression pattern)
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Test"),
            filter: meta =>
                meta.TenantId == "tenant-1" &&
                meta.SubscriberId != "sub-2"
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
        Assert.IsFalse(receivedMessages.Contains("sub-2"));
        Assert.IsTrue(receivedMessages.Contains("sub-3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_FilterExcludesAll_DeliversToNone() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages)
        };
        await Task.Delay(100);

        // Act - filter that excludes everyone
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Test"),
            filter: meta => meta.TenantId == "non-existent-tenant"
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(0, receivedMessages.Count);
    }

    [TestMethod]
    public async Task BroadcastUpdate_WithNullModel_ThrowsArgumentNullException() {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            _broadcast.BroadcastUpdate(null!)
        );
    }

    [TestMethod]
    public async Task BroadcastUpdate_AfterDispose_ThrowsObjectDisposedException() {
        // Arrange
        _broadcast.Dispose();

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            _broadcast.BroadcastUpdate(new BroadcastTestModel("Test"))
        );
    }

    #endregion

    #region Subscriber Management Tests

    [TestMethod]
    public void Unsubscribe_RemovesSubscriber() {
        // Arrange
        var metadata = new BroadcastTestMetadata("sub-1", "tenant-1", "Admin");
        _broadcast.Subscribe(metadata);
        Assert.IsTrue(_broadcast.HasSubscriber("sub-1"));

        // Act
        _broadcast.Unsubscribe("sub-1");

        // Assert
        Assert.IsFalse(_broadcast.HasSubscriber("sub-1"));
        Assert.AreEqual(0, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public void Unsubscribe_NonExistentSubscriber_NoError() {
        // Act (should not throw)
        _broadcast.Unsubscribe("non-existent");

        // Assert
        Assert.AreEqual(0, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public void GetActiveSubscribers_ReturnsAllSubscriberIds() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));

        // Act
        var subscribers = _broadcast.GetActiveSubscribers();

        // Assert
        Assert.AreEqual(3, subscribers.Count);
        Assert.IsTrue(subscribers.Contains("sub-1"));
        Assert.IsTrue(subscribers.Contains("sub-2"));
        Assert.IsTrue(subscribers.Contains("sub-3"));
    }

    #endregion

    #region Logging Tests

    [TestMethod]
    public void Subscribe_DuplicateSubscriberId_LogsWarning() {
        // Arrange
        var metadata1 = new BroadcastTestMetadata("sub-1", "tenant-1", "Admin");
        var metadata2 = new BroadcastTestMetadata("sub-1", "tenant-2", "User");
        _broadcast.Subscribe(metadata1);
        _logger.Clear();

        // Act
        var result = _broadcast.Subscribe(metadata2);

        // Assert
        Assert.IsFalse(result);
        Assert.AreEqual(LogLevel.Warning, _logger.LastLogLevel);
        Assert.IsTrue(_logger.LogMessages.Any(msg =>
            msg.Contains("Duplicate subscription attempt") &&
            msg.Contains("sub-1")
        ));
    }

    [TestMethod]
    public void Subscribe_Successful_LogsDebug() {
        // Arrange
        var metadata = new BroadcastTestMetadata("sub-1", "tenant-1", "Admin");

        // Act
        var result = _broadcast.Subscribe(metadata);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(LogLevel.Debug, _logger.LastLogLevel);
        Assert.IsTrue(_logger.LogMessages.Any(msg =>
            msg.Contains("registered successfully") &&
            msg.Contains("sub-1")
        ));
    }

    [TestMethod]
    public void Unsubscribe_Successful_LogsDebug() {
        // Arrange
        var metadata = new BroadcastTestMetadata("sub-1", "tenant-1", "Admin");
        _broadcast.Subscribe(metadata);
        _logger.Clear();

        // Act
        _broadcast.Unsubscribe("sub-1");

        // Assert
        Assert.AreEqual(LogLevel.Debug, _logger.LastLogLevel);
        Assert.IsTrue(_logger.LogMessages.Any(msg =>
            msg.Contains("unsubscribed") &&
            msg.Contains("sub-1")
        ));
    }

    [TestMethod]
    public void Unsubscribe_NonExistentSubscriber_LogsDebug() {
        // Act
        _broadcast.Unsubscribe("non-existent");

        // Assert
        Assert.AreEqual(LogLevel.Debug, _logger.LastLogLevel);
        Assert.IsTrue(_logger.LogMessages.Any(msg =>
            msg.Contains("non-existent") &&
            (msg.Contains("never subscribed") || msg.Contains("Already unsubscribed"))
        ));
    }

    #endregion

    #region Complex Filtering Scenarios

    [TestMethod]
    public async Task BroadcastUpdate_ComplexFilter_MultipleConditions() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-3", "tenant-2", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-4", "tenant-1", "Manager"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages),
            ConsumeMessagesWithId("sub-3", receivedMessages),
            ConsumeMessagesWithId("sub-4", receivedMessages)
        };
        await Task.Delay(100);

        // Act - tenant-1 AND (Admin OR Manager) AND not sub-4
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Test"),
            filter: meta =>
                meta.TenantId == "tenant-1" &&
                (meta.Role == "Admin" || meta.Role == "Manager") &&
                meta.SubscriberId != "sub-4"
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_FilterWithMethodCall_WorksCorrectly() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-2", "User"));
        var receivedMessages = new ConcurrentBag<string>();
        var tasks = new List<Task> {
            ConsumeMessagesWithId("sub-1", receivedMessages),
            ConsumeMessagesWithId("sub-2", receivedMessages)
        };
        await Task.Delay(100);
        var allowedTenants = new[] { "tenant-1", "tenant-3" };

        // Act - filter using method call
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel("Test"),
            filter: meta => allowedTenants.Contains(meta.TenantId)
        );
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public async Task BroadcastUpdate_NoSubscribers_CompletesSuccessfully() {
        // Act (should not throw)
        await _broadcast.BroadcastUpdate(new BroadcastTestModel("Test"));

        // Assert - no subscribers, no error
        Assert.AreEqual(0, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public async Task BroadcastUpdate_SubscriberDisconnectsDuringBroadcast_OtherSubscribersReceive() {
        // Arrange
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-1", "tenant-1", "Admin"));
        _broadcast.Subscribe(new BroadcastTestMetadata("sub-2", "tenant-1", "User"));
        var receivedMessages = new ConcurrentBag<string>();
        var task1 = ConsumeMessagesWithId("sub-1", receivedMessages);
        var cts = new CancellationTokenSource();
        var task2 = ConsumeMessagesWithId("sub-2", receivedMessages, cts.Token);
        await Task.Delay(100);

        // Disconnect sub-2 before broadcast
        _broadcast.Unsubscribe("sub-2");

        // Act
        await _broadcast.BroadcastUpdate(new BroadcastTestModel("Test"));
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Contains("sub-1"));
    }

    #endregion

    #region Helper Methods

    private async Task ConsumeMessages(string subscriberId, ConcurrentBag<BroadcastTestModel> messages) {
        try {
            await foreach (var msg in _broadcast.GetUpdates(subscriberId, CancellationToken.None)) {
                messages.Add(msg);
                break;
            }
        } catch (OperationCanceledException) {
            // Expected on cancellation
        }
    }

    private async Task ConsumeMessagesWithId(string subscriberId, ConcurrentBag<string> subscriberIds, CancellationToken ct = default) {
        try {
            await foreach (var msg in _broadcast.GetUpdates(subscriberId, ct)) {
                subscriberIds.Add(subscriberId);
                break;
            }
        } catch (OperationCanceledException) {
            // Expected on cancellation
        }
    }

    #endregion
}

#region Test Models

public record BroadcastTestModel(string Message);

public record BroadcastTestMetadata(
    string SubscriberId,
    string TenantId,
    string Role
) : ISseMetadataProvider {
    public IReadOnlyDictionary<string, string> ToSerializableDictionary() {
        return new Dictionary<string, string> {
            [nameof(SubscriberId)] = SubscriberId,
            [nameof(TenantId)] = TenantId,
            [nameof(Role)] = Role
        };
    }
}

#endregion
