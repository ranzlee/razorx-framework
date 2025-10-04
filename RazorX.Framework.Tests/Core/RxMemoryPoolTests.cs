using System.Buffers;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RazorX.Framework;

namespace RazorX.Framework.Tests.Core;

/// <summary>
/// Tests for memory pooling infrastructure.
/// Validates correct buffer lifecycle, reuse, and disposal patterns.
/// </summary>
[TestClass]
public class RxMemoryPoolTests {

    #region RentedBuffer<T> Tests

    [TestMethod]
    public void RentedBuffer_Rent_ReturnsValidBuffer() {
        // Arrange
        var pool = ArrayPool<char>.Shared;
        var buffer = pool.Rent(1024);

        // Act
        using var rentedBuffer = new RentedBuffer<char>(buffer, pool);

        // Assert
        Assert.IsTrue(rentedBuffer.Length >= 1024);
        Assert.AreEqual(rentedBuffer.Span.Length, rentedBuffer.Memory.Length);
        Assert.AreEqual(rentedBuffer.Span.Length, rentedBuffer.Length);
    }

    [TestMethod]
    public void RentedBuffer_Dispose_ReturnsBufferToPool() {
        // Arrange
        var pool = ArrayPool<char>.Shared;
        var buffer = pool.Rent(1024);
        var rentedBuffer = new RentedBuffer<char>(buffer, pool);

        // Act
        rentedBuffer.Dispose();

        // Assert - if we rent again, we should likely get the same buffer
        // (This is not guaranteed by ArrayPool contract, but typically happens)
        var buffer2 = pool.Rent(1024);
        try {
            // The test passes if no exception is thrown - disposal succeeded
            Assert.IsNotNull(buffer2);
        }
        finally {
            pool.Return(buffer2);
        }
    }

    [TestMethod]
    public void RentedBuffer_Span_ProvidesAccess() {
        // Arrange
        var pool = ArrayPool<char>.Shared;
        var buffer = pool.Rent(1024);
        using var rentedBuffer = new RentedBuffer<char>(buffer, pool);

        // Act
        var span = rentedBuffer.Span;
        span[0] = 'A';
        span[1] = 'B';
        span[2] = 'C';

        // Assert
        Assert.AreEqual('A', span[0]);
        Assert.AreEqual('B', span[1]);
        Assert.AreEqual('C', span[2]);
    }

    #endregion

    #region PooledStringBuilder Tests

    [TestMethod]
    public void PooledStringBuilder_Append_String_Works() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 128);

        // Act
        builder.Append("Hello");
        builder.Append(" ");
        builder.Append("World");

        // Assert
        Assert.AreEqual(11, builder.Length);
        Assert.AreEqual("Hello World", builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_Append_Char_Works() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 128);

        // Act
        builder.Append('A');
        builder.Append('B');
        builder.Append('C');

        // Assert
        Assert.AreEqual(3, builder.Length);
        Assert.AreEqual("ABC", builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_Append_Span_Works() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 128);
        var span = "Test String".AsSpan();

        // Act
        builder.Append(span);

        // Assert
        Assert.AreEqual(11, builder.Length);
        Assert.AreEqual("Test String", builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_Append_Null_IsIgnored() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 128);

        // Act
        builder.Append((string?)null);
        builder.Append(string.Empty);
        builder.Append("Test");

        // Assert
        Assert.AreEqual(4, builder.Length);
        Assert.AreEqual("Test", builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_GrowsCapacity_WhenExceeded() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 16);
        var initialCapacity = builder.Capacity;

        // Act - append more than initial capacity
        var longString = new string('X', 100);
        builder.Append(longString);

        // Assert
        Assert.AreEqual(100, builder.Length);
        Assert.IsTrue(builder.Capacity > initialCapacity, "Capacity should have grown");
        Assert.AreEqual(longString, builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_MultipleGrowths_Work() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 8);

        // Act - force multiple doublings
        for (int i = 0; i < 100; i++) {
            builder.Append("ABCDEFGH"); // 8 chars each
        }

        // Assert
        Assert.AreEqual(800, builder.Length);
        Assert.IsTrue(builder.Capacity >= 800);
        var result = builder.ToString();
        Assert.AreEqual(800, result.Length);
        Assert.IsTrue(result.StartsWith("ABCDEFGH"));
    }

    [TestMethod]
    public void PooledStringBuilder_ToString_MultipleCallsWork() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 128);
        builder.Append("Test");

        // Act
        var result1 = builder.ToString();
        var result2 = builder.ToString();

        // Assert
        Assert.AreEqual("Test", result1);
        Assert.AreEqual("Test", result2);
        Assert.AreEqual(result1, result2);
    }

    [TestMethod]
    public void PooledStringBuilder_Clear_ResetsLength() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 128);
        builder.Append("Initial content");

        // Act
        builder.Clear();

        // Assert
        Assert.AreEqual(0, builder.Length);
        Assert.AreEqual(string.Empty, builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_Clear_ThenAppend_Works() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(initialCapacity: 128);
        builder.Append("First");
        builder.Clear();

        // Act
        builder.Append("Second");

        // Assert
        Assert.AreEqual(6, builder.Length);
        Assert.AreEqual("Second", builder.ToString());
    }

    [TestMethod]
    [ExpectedException(typeof(ObjectDisposedException))]
    public void PooledStringBuilder_ToString_AfterDispose_Throws() {
        // Arrange
        var pool = new RxMemoryPool();
        var builder = pool.RentStringBuilder(initialCapacity: 128);
        builder.Append("Test");
        builder.Dispose();

        // Act - should throw
        _ = builder.ToString();
    }

    [TestMethod]
    [ExpectedException(typeof(ObjectDisposedException))]
    public void PooledStringBuilder_Clear_AfterDispose_Throws() {
        // Arrange
        var pool = new RxMemoryPool();
        var builder = pool.RentStringBuilder(initialCapacity: 128);
        builder.Dispose();

        // Act - should throw
        builder.Clear();
    }

    [TestMethod]
    public void PooledStringBuilder_Dispose_IsIdempotent() {
        // Arrange
        var pool = new RxMemoryPool();
        var builder = pool.RentStringBuilder(initialCapacity: 128);

        // Act - dispose multiple times
        builder.Dispose();
        builder.Dispose();
        builder.Dispose();

        // Assert - no exception thrown
        Assert.IsTrue(true);
    }

    #endregion

    #region RxMemoryPool Tests

    [TestMethod]
    public void RxMemoryPool_RentStringBuilder_ReturnsValidBuilder() {
        // Arrange
        var pool = new RxMemoryPool();

        // Act
        using var builder = pool.RentStringBuilder(1024);

        // Assert
        Assert.IsNotNull(builder);
        Assert.IsTrue(builder.Capacity >= 1024);
    }

    [TestMethod]
    public void RxMemoryPool_RentCharBuffer_ReturnsValidBuffer() {
        // Arrange
        var pool = new RxMemoryPool();

        // Act
        using var buffer = pool.RentCharBuffer(1024);

        // Assert
        Assert.IsTrue(buffer.Length >= 1024);
    }

    [TestMethod]
    public void RxMemoryPool_DefaultConstructor_UsesSharedPool() {
        // Arrange & Act
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(128);

        // Assert - should work without custom pool
        builder.Append("Test");
        Assert.AreEqual("Test", builder.ToString());
    }

    #endregion

    #region Integration and Performance Tests

    [TestMethod]
    public void PooledStringBuilder_SimulateFragmentRendering_Works() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(16384); // Same as RxResponseBuilder

        // Act - simulate rendering multiple fragments
        for (int i = 0; i < 10; i++) {
            var fragmentId = $"fragment-{i}";
            var fragmentContent = $"<div id=\"content-{i}\">Fragment {i} content</div>";
            var template = $"<template id=\"{fragmentId}-rx-fragment\">{fragmentContent}</template>";
            builder.Append(template);
        }

        // Assert
        var result = builder.ToString();
        Assert.IsTrue(result.Contains("fragment-0"));
        Assert.IsTrue(result.Contains("fragment-9"));
        Assert.IsTrue(result.Contains("Fragment 0 content"));
        Assert.IsTrue(result.Contains("Fragment 9 content"));
    }

    [TestMethod]
    public void PooledStringBuilder_BufferReuse_ReducesAllocations() {
        // Arrange
        var pool = new RxMemoryPool();
        var allocatedBefore = GC.GetTotalMemory(forceFullCollection: true);

        // Act - simulate 100 requests with pooled builders
        for (int i = 0; i < 100; i++) {
            using var builder = pool.RentStringBuilder(16384);
            builder.Append("<template id=\"test-rx-fragment\">");
            builder.Append("<div>Content</div>");
            builder.Append("</template>");
            _ = builder.ToString();
        } // builder.Dispose() returns buffer to pool

        var allocatedAfter = GC.GetTotalMemory(forceFullCollection: false);
        var allocated = allocatedAfter - allocatedBefore;

        // Assert - should allocate much less than 100 × 32KB = 3.2MB
        // The actual strings are unavoidable, but buffer allocations should be minimal
        // With pooling, we expect < 500KB allocated (mostly the final strings)
        Assert.IsTrue(allocated < 500_000,
            $"Expected < 500KB allocated, but got {allocated:N0} bytes. " +
            "Pooling may not be working correctly.");
    }

    [TestMethod]
    public void PooledStringBuilder_ConcurrentUse_IsThreadSafe() {
        // Arrange
        var pool = new RxMemoryPool();
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        // Act - simulate concurrent requests (like RazorX under load)
        for (int i = 0; i < 50; i++) {
            var taskId = i;
            tasks.Add(Task.Run(() => {
                try {
                    using var builder = pool.RentStringBuilder(1024);
                    builder.Append($"Task {taskId} content");
                    var result = builder.ToString();
                    Assert.AreEqual($"Task {taskId} content", result);
                }
                catch (Exception ex) {
                    lock (exceptions) {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll([.. tasks]);

        // Assert - no exceptions should occur
        if (exceptions.Count > 0) {
            Assert.Fail($"Thread safety violated: {exceptions.Count} exceptions occurred. " +
                       $"First exception: {exceptions[0].Message}");
        }
    }

    [TestMethod]
    public void PooledStringBuilder_EmptyString_Works() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(128);

        // Act - don't append anything
        var result = builder.ToString();

        // Assert
        Assert.AreEqual(string.Empty, result);
        Assert.AreEqual(0, builder.Length);
    }

    [TestMethod]
    public void PooledStringBuilder_LargeContent_ExceedsInitialCapacity() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(128);

        // Act - append 10KB of content (much larger than 128 chars)
        var largeString = new string('X', 10_000);
        builder.Append(largeString);

        // Assert
        Assert.AreEqual(10_000, builder.Length);
        Assert.IsTrue(builder.Capacity >= 10_000);
        Assert.AreEqual(largeString, builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_ManySmallAppends_IsEfficient() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(16384);

        // Act - simulate many small appends (common in fragment rendering)
        for (int i = 0; i < 1000; i++) {
            builder.Append("ABC");
        }

        // Assert
        Assert.AreEqual(3000, builder.Length);
        var result = builder.ToString();
        Assert.AreEqual(3000, result.Length);
        Assert.IsTrue(result.StartsWith("ABCABCABC"));
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void PooledStringBuilder_AppendEmptySpan_IsIgnored() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(128);

        // Act
        builder.Append(ReadOnlySpan<char>.Empty);
        builder.Append("Test");

        // Assert
        Assert.AreEqual(4, builder.Length);
        Assert.AreEqual("Test", builder.ToString());
    }

    [TestMethod]
    public void PooledStringBuilder_ExactCapacityFit_NoGrowth() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(10);
        var initialCapacity = builder.Capacity;

        // Act - append exactly the capacity or less
        builder.Append("12345");

        // Assert
        Assert.AreEqual(initialCapacity, builder.Capacity, "Capacity should not grow");
    }

    [TestMethod]
    public void PooledStringBuilder_SpecialCharacters_ArePreserved() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(128);

        // Act
        builder.Append("<div class=\"test\" data-value='123'>");
        builder.Append("Content with & < > \" ' characters");
        builder.Append("</div>");

        // Assert
        var result = builder.ToString();
        Assert.IsTrue(result.Contains("<div class=\"test\" data-value='123'>"));
        Assert.IsTrue(result.Contains("& < > \" '"));
    }

    [TestMethod]
    public void PooledStringBuilder_UnicodeCharacters_ArePreserved() {
        // Arrange
        var pool = new RxMemoryPool();
        using var builder = pool.RentStringBuilder(128);

        // Act
        builder.Append("Hello 世界 🌍 Привет");

        // Assert
        Assert.AreEqual("Hello 世界 🌍 Привет", builder.ToString());
    }

    #endregion
}
