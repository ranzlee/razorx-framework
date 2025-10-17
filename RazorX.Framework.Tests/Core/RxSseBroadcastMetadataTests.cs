using System.Collections.Concurrent;
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

    #region Basic Subscription Tests

    [TestMethod]
    public void Subscribe_WithNoFilter_ReturnsTrue() {
        // Act
        var result = _broadcast.Subscribe("sub-1");

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(_broadcast.HasSubscriber("sub-1"));
        Assert.AreEqual(1, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public void Subscribe_WithFilter_ReturnsTrue() {
        // Act
        var result = _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");

        // Assert
        Assert.IsTrue(result);
        Assert.IsTrue(_broadcast.HasSubscriber("sub-1"));
        Assert.AreEqual(1, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public void Subscribe_DuplicateSubscriberId_ReturnsFalse() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");

        // Act
        var result = _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-2");

        // Assert
        Assert.IsFalse(result);
        Assert.AreEqual(1, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public void Subscribe_WithEmptySubscriberId_ThrowsArgumentException() {
        // Act & Assert
        var ex = Assert.ThrowsExactly<ArgumentException>(() => _broadcast.Subscribe(""));
        Assert.IsTrue(ex.Message.Contains("subscriberId") || ex.Message.Contains("null or whitespace"));
    }

    [TestMethod]
    public void Subscribe_WithWhitespaceSubscriberId_ThrowsArgumentException() {
        // Act & Assert
        var ex = Assert.ThrowsExactly<ArgumentException>(() => _broadcast.Subscribe("   "));
        Assert.IsTrue(ex.Message.Contains("subscriberId") || ex.Message.Contains("null or whitespace"));
    }

    [TestMethod]
    public void Subscribe_WithNullSubscriberId_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => _broadcast.Subscribe(null!));
    }

    #endregion

    #region Filtering Tests - No Filter

    [TestMethod]
    public async Task BroadcastUpdate_NoFilter_ReceivesAllBroadcasts() {
        // Arrange
        _broadcast.Subscribe("sub-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 3);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 2" },
            new BroadcastTestMetadata { SubscriberId = "sub-1", TenantId = "tenant-2", Role = "User" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 3" },
            null);
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(3, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Message 1"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Message 2"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Message 3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_FilterAcceptingAll_ReceivesAllBroadcasts() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: _ => true);
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 3);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 2" },
            new BroadcastTestMetadata { SubscriberId = "sub-1", TenantId = "tenant-2", Role = "User" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 3" },
            null);
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(3, receivedMessages.Count);
    }

    #endregion

    #region Filtering Tests - Reject All

    [TestMethod]
    public async Task BroadcastUpdate_FilterRejectingAll_ReceivesNothing() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: _ => false);
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var task = ConsumeMessages("sub-1", receivedMessages, ct: cts.Token);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Message 2" },
            null);
        await Task.Delay(150);

        // Assert
        Assert.AreEqual(0, receivedMessages.Count);
    }

    #endregion

    #region Filtering Tests - SubscriberId (Echo Suppression)

    [TestMethod]
    public async Task BroadcastUpdate_FilterBySubscriberId_ExcludesOwnBroadcasts() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.SubscriberId != "sub-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "From other" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "From sub-1 (echo)" },
            new BroadcastTestMetadata { SubscriberId = "sub-1", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "From another" },
            new BroadcastTestMetadata { SubscriberId = "another", TenantId = "tenant-1", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "From other"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "From sub-1 (echo)"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "From another"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_MultipleSubscribers_EchoSuppressionWorksPerSubscriber() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.SubscriberId != "sub-1");
        _broadcast.Subscribe("sub-2", filter: meta => meta?.SubscriberId != "sub-2");
        var messages1 = new ConcurrentBag<BroadcastTestModel>();
        var messages2 = new ConcurrentBag<BroadcastTestModel>();
        var task1 = ConsumeMessages("sub-1", messages1, maxMessages: 2);
        var task2 = ConsumeMessages("sub-2", messages2, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Broadcast from sub-1" },
            new BroadcastTestMetadata { SubscriberId = "sub-1", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Broadcast from sub-2" },
            new BroadcastTestMetadata { SubscriberId = "sub-2", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Broadcast from other" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, messages1.Count);
        Assert.IsFalse(messages1.Any(m => m.Message == "Broadcast from sub-1"));
        Assert.IsTrue(messages1.Any(m => m.Message == "Broadcast from sub-2"));
        Assert.IsTrue(messages1.Any(m => m.Message == "Broadcast from other"));

        Assert.AreEqual(2, messages2.Count);
        Assert.IsTrue(messages2.Any(m => m.Message == "Broadcast from sub-1"));
        Assert.IsFalse(messages2.Any(m => m.Message == "Broadcast from sub-2"));
        Assert.IsTrue(messages2.Any(m => m.Message == "Broadcast from other"));
    }

    #endregion

    #region Filtering Tests - TenantId

    [TestMethod]
    public async Task BroadcastUpdate_FilterByTenantId_ReceivesMatchingOnly() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 1 message" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 2 message" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Another Tenant 1 message" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "User" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Tenant 1 message"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "Tenant 2 message"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Another Tenant 1 message"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_MultipleSubscribersWithDifferentTenants_ReceivesCorrectMessages() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
        _broadcast.Subscribe("sub-2", filter: meta => meta?.TenantId == "tenant-1");
        _broadcast.Subscribe("sub-3", filter: meta => meta?.TenantId == "tenant-2");
        var messages1 = new ConcurrentBag<string>();
        var messages2 = new ConcurrentBag<string>();
        var messages3 = new ConcurrentBag<string>();
        var task1 = ConsumeMessagesWithId("sub-1", messages1, maxMessages: 2);
        var task2 = ConsumeMessagesWithId("sub-2", messages2, maxMessages: 2);
        var task3 = ConsumeMessagesWithId("sub-3", messages3, maxMessages: 1);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 1 message 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 2 message 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 1 message 2" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "User" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, messages1.Count);
        Assert.AreEqual(2, messages2.Count);
        Assert.AreEqual(1, messages3.Count);
        Assert.IsTrue(messages3.Contains("sub-3"));
    }

    #endregion

    #region Filtering Tests - Role

    [TestMethod]
    public async Task BroadcastUpdate_FilterByRole_ReceivesMatchingOnly() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.Role == "Admin");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Admin broadcast" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "User broadcast" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "User" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Another Admin broadcast" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Admin broadcast"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "User broadcast"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Another Admin broadcast"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_MultipleSubscribersWithDifferentRoles_ReceivesCorrectMessages() {
        // Arrange
        _broadcast.Subscribe("sub-admin", filter: meta => meta?.Role == "Admin");
        _broadcast.Subscribe("sub-user", filter: meta => meta?.Role == "User");
        var adminMessages = new ConcurrentBag<string>();
        var userMessages = new ConcurrentBag<string>();
        var task1 = ConsumeMessagesWithId("sub-admin", adminMessages, maxMessages: 2);
        var task2 = ConsumeMessagesWithId("sub-user", userMessages, maxMessages: 1);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Admin message 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "User message" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "User" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Admin message 2" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, adminMessages.Count);
        Assert.AreEqual(1, userMessages.Count);
        Assert.IsTrue(userMessages.Contains("sub-user"));
    }

    #endregion

    #region Filtering Tests - Combined Filters

    [TestMethod]
    public async Task BroadcastUpdate_CombinedFilter_TenantAndRole() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta =>
            meta?.TenantId == "tenant-1" && meta?.Role == "Admin");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 1);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 1 Admin" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 1 User" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "User" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 2 Admin" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Tenant 1 Admin"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_CombinedFilter_TenantAndExcludeSubscriber() {
        // Arrange - Common echo suppression + tenant isolation pattern
        _broadcast.Subscribe("sub-1", filter: meta =>
            meta?.TenantId == "tenant-1" && meta?.SubscriberId != "sub-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "From other in tenant 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "From sub-1 in tenant 1 (echo)" },
            new BroadcastTestMetadata { SubscriberId = "sub-1", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "From other in tenant 2" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "From another in tenant 1" },
            new BroadcastTestMetadata { SubscriberId = "another", TenantId = "tenant-1", Role = "User" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "From other in tenant 1"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "From sub-1 in tenant 1 (echo)"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "From other in tenant 2"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "From another in tenant 1"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_ComplexFilter_MultipleConditions() {
        // Arrange - tenant-1 AND (Admin OR Manager) AND not sub-4
        _broadcast.Subscribe("sub-1", filter: meta =>
            meta?.TenantId == "tenant-1" &&
            (meta?.Role == "Admin" || meta?.Role == "Manager") &&
            meta?.SubscriberId != "sub-4");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T1 Admin sub-1" },
            new BroadcastTestMetadata { SubscriberId = "sub-1", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T1 Manager sub-2" },
            new BroadcastTestMetadata { SubscriberId = "sub-2", TenantId = "tenant-1", Role = "Manager" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T1 Admin sub-4" },
            new BroadcastTestMetadata { SubscriberId = "sub-4", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T1 User sub-5" },
            new BroadcastTestMetadata { SubscriberId = "sub-5", TenantId = "tenant-1", Role = "User" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T2 Admin sub-6" },
            new BroadcastTestMetadata { SubscriberId = "sub-6", TenantId = "tenant-2", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "T1 Admin sub-1"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "T1 Manager sub-2"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "T1 Admin sub-4"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "T1 User sub-5"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "T2 Admin sub-6"));
    }

    #endregion

    #region Filtering Tests - Null Metadata Handling

    [TestMethod]
    public async Task BroadcastUpdate_NullMetadata_NoFilter_ReceivesBroadcast() {
        // Arrange
        _broadcast.Subscribe("sub-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 1);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "No metadata" },
            null);
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(1, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "No metadata"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_NullMetadata_FilterRejectingNull_DoesNotReceive() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta != null);
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var task = ConsumeMessages("sub-1", receivedMessages, ct: cts.Token);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "No metadata" },
            null);
        await Task.Delay(150);

        // Assert
        Assert.AreEqual(0, receivedMessages.Count);
    }

    [TestMethod]
    public async Task BroadcastUpdate_NullMetadata_FilterAcceptingNull_ReceivesBroadcast() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta == null || meta.TenantId == "tenant-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "No metadata" },
            null);
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
    }

    [TestMethod]
    public async Task BroadcastUpdate_NullMetadata_NullConditionalFilter_ReceivesBroadcast() {
        // Arrange - Use null-conditional operator in filter
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var task = ConsumeMessages("sub-1", receivedMessages, ct: cts.Token);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "No metadata" },
            null);
        await Task.Delay(150);

        // Assert - Filter returns false for null metadata (null != "tenant-1")
        Assert.AreEqual(0, receivedMessages.Count);
    }

    #endregion

    #region Filtering Tests - Method Call in Filter

    [TestMethod]
    public async Task BroadcastUpdate_FilterWithMethodCall_WorksCorrectly() {
        // Arrange
        var allowedTenants = new[] { "tenant-1", "tenant-3" };
        _broadcast.Subscribe("sub-1", filter: meta => meta != null && allowedTenants.Contains(meta.TenantId));
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 1" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 2" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 3" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-3", Role = "User" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Tenant 1"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "Tenant 2"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Tenant 3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_FilterWithStringMethod_WorksCorrectly() {
        // Arrange - Use string methods in filter
        _broadcast.Subscribe("sub-1", filter: meta =>
            meta != null && meta.Role != null && meta.Role.StartsWith("Admin"));
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var task = ConsumeMessages("sub-1", receivedMessages, maxMessages: 2);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Admin" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "AdminPlus" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "AdminPlus" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "User" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "User" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(2, receivedMessages.Count);
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "Admin"));
        Assert.IsTrue(receivedMessages.Any(m => m.Message == "AdminPlus"));
        Assert.IsFalse(receivedMessages.Any(m => m.Message == "User"));
    }

    #endregion

    #region Subscriber Management Tests

    [TestMethod]
    public void Unsubscribe_RemovesSubscriber() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
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
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
        _broadcast.Subscribe("sub-2");
        _broadcast.Subscribe("sub-3", filter: meta => meta?.Role == "Admin");

        // Act
        var subscribers = _broadcast.GetActiveSubscribers();

        // Assert
        Assert.AreEqual(3, subscribers.Count);
        Assert.IsTrue(subscribers.Contains("sub-1"));
        Assert.IsTrue(subscribers.Contains("sub-2"));
        Assert.IsTrue(subscribers.Contains("sub-3"));
    }

    [TestMethod]
    public async Task BroadcastUpdate_AfterUnsubscribe_DoesNotReceive() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
        _broadcast.Unsubscribe("sub-1");
        var receivedMessages = new ConcurrentBag<BroadcastTestModel>();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var task = ConsumeMessages("sub-1", receivedMessages, ct: cts.Token);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "After unsubscribe" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await Task.Delay(150);

        // Assert
        Assert.AreEqual(0, receivedMessages.Count);
    }

    #endregion

    #region Multiple Subscribers with Different Filters

    [TestMethod]
    public async Task BroadcastUpdate_MultipleSubscribersWithDifferentFilters_ReceivesCorrectly() {
        // Arrange
        _broadcast.Subscribe("sub-no-filter");
        _broadcast.Subscribe("sub-tenant-1", filter: meta => meta?.TenantId == "tenant-1");
        _broadcast.Subscribe("sub-admin", filter: meta => meta?.Role == "Admin");
        _broadcast.Subscribe("sub-complex", filter: meta =>
            meta?.TenantId == "tenant-1" && meta?.Role == "Admin" && meta?.SubscriberId != "sub-complex");

        var noFilterMessages = new ConcurrentBag<string>();
        var tenant1Messages = new ConcurrentBag<string>();
        var adminMessages = new ConcurrentBag<string>();
        var complexMessages = new ConcurrentBag<string>();

        var task1 = ConsumeMessagesWithId("sub-no-filter", noFilterMessages, maxMessages: 4);
        var task2 = ConsumeMessagesWithId("sub-tenant-1", tenant1Messages, maxMessages: 3);
        var task3 = ConsumeMessagesWithId("sub-admin", adminMessages, maxMessages: 2);
        var task4 = ConsumeMessagesWithId("sub-complex", complexMessages, maxMessages: 1);
        await Task.Delay(100);

        // Act
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T1 Admin other" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T1 User other" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "User" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T2 Admin other" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "T1 Admin sub-complex" },
            new BroadcastTestMetadata { SubscriberId = "sub-complex", TenantId = "tenant-1", Role = "Admin" });
        await Task.Delay(100);

        // Assert
        Assert.AreEqual(4, noFilterMessages.Count);
        Assert.AreEqual(3, tenant1Messages.Count);
        Assert.AreEqual(2, adminMessages.Count);
        Assert.AreEqual(1, complexMessages.Count);

        Assert.IsTrue(complexMessages.Contains("sub-complex"));
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task BroadcastUpdate_WithNullModel_ThrowsArgumentNullException() {
        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            _broadcast.BroadcastUpdate(null!, null)
        );
    }

    [TestMethod]
    public async Task BroadcastUpdate_AfterDispose_ThrowsObjectDisposedException() {
        // Arrange
        _broadcast.Dispose();

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            _broadcast.BroadcastUpdate(new BroadcastTestModel { Message = "Test" }, null)
        );
    }

    [TestMethod]
    public void Subscribe_AfterDispose_ThrowsObjectDisposedException() {
        // Arrange
        _broadcast.Dispose();

        // Act & Assert
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            _broadcast.Subscribe("sub-1")
        );
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public async Task BroadcastUpdate_NoSubscribers_CompletesSuccessfully() {
        // Act (should not throw)
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Test" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-1", Role = "Admin" });

        // Assert - no subscribers, no error
        Assert.AreEqual(0, _broadcast.GetActiveConnectionCount());
    }

    [TestMethod]
    public async Task BroadcastUpdate_AllSubscribersFiltered_CompletesSuccessfully() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
        _broadcast.Subscribe("sub-2", filter: meta => meta?.TenantId == "tenant-1");
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var messages = new ConcurrentBag<string>();
        var task1 = ConsumeMessagesWithId("sub-1", messages, ct: cts.Token);
        var task2 = ConsumeMessagesWithId("sub-2", messages, ct: cts.Token);
        await Task.Delay(100);

        // Act - broadcast to tenant-2 (all subscribers filter for tenant-1)
        await _broadcast.BroadcastUpdate(
            new BroadcastTestModel { Message = "Tenant 2 message" },
            new BroadcastTestMetadata { SubscriberId = "other", TenantId = "tenant-2", Role = "Admin" });
        await Task.Delay(150);

        // Assert
        Assert.AreEqual(0, messages.Count);
    }

    #endregion

    #region Logging Tests

    [TestMethod]
    public void Subscribe_Successful_LogsDebug() {
        // Act
        var result = _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(LogLevel.Debug, _logger.LastLogLevel);
        Assert.IsTrue(_logger.LogMessages.Any(msg =>
            msg.Contains("registered successfully") &&
            msg.Contains("sub-1")
        ));
    }

    [TestMethod]
    public void Subscribe_Duplicate_LogsWarning() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
        _logger.Clear();

        // Act
        var result = _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-2");

        // Assert
        Assert.IsFalse(result);
        Assert.AreEqual(LogLevel.Warning, _logger.LastLogLevel);
        Assert.IsTrue(_logger.LogMessages.Any(msg =>
            msg.Contains("Duplicate subscription attempt") &&
            msg.Contains("sub-1")
        ));
    }

    [TestMethod]
    public void Unsubscribe_Successful_LogsDebug() {
        // Arrange
        _broadcast.Subscribe("sub-1", filter: meta => meta?.TenantId == "tenant-1");
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
            (msg.Contains("never subscribed") || msg.Contains("Already unsubscribed") || msg.Contains("non-existent"))
        ));
    }

    #endregion

    #region Helper Methods

    private async Task ConsumeMessages(
        string subscriberId,
        ConcurrentBag<BroadcastTestModel> messages,
        int? maxMessages = null,
        CancellationToken ct = default) {
        try {
            var count = 0;
            await foreach (var msg in _broadcast.GetUpdates(subscriberId, ct)) {
                messages.Add(msg);
                count++;
                if (maxMessages.HasValue && count >= maxMessages.Value) {
                    break;
                }
            }
        } catch (OperationCanceledException) {
            // Expected on cancellation
        }
    }

    private async Task ConsumeMessagesWithId(
        string subscriberId,
        ConcurrentBag<string> subscriberIds,
        int? maxMessages = null,
        CancellationToken ct = default) {
        try {
            var count = 0;
            await foreach (var msg in _broadcast.GetUpdates(subscriberId, ct)) {
                subscriberIds.Add(subscriberId);
                count++;
                if (maxMessages.HasValue && count >= maxMessages.Value) {
                    break;
                }
            }
        } catch (OperationCanceledException) {
            // Expected on cancellation
        }
    }

    #endregion
}

#region Test Models

public record BroadcastTestModel {
    public string Message { get; init; } = "";
}

public record BroadcastTestMetadata {
    public string? SubscriberId { get; init; }
    public string? TenantId { get; init; }
    public string? Role { get; init; }
}

#endregion
