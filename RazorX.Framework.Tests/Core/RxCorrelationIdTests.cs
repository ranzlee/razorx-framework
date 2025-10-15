using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RazorX.Framework.Tests.Mocks;
using System.Diagnostics;

namespace RazorX.Framework.Tests.Core;

[TestClass]
public class RxCorrelationIdTests {
    private TestLogger<RxCorrelationIdMiddleware> _logger = null!;
    private DefaultHttpContext _httpContext = null!;
    private bool _nextCalled;
    private string? _correlationIdInNext;

    [TestInitialize]
    public void SetUp() {
        _logger = new TestLogger<RxCorrelationIdMiddleware>();
        _httpContext = new DefaultHttpContext();
        _nextCalled = false;
        _correlationIdInNext = null;
    }

    #region Middleware Tests

    [TestMethod]
    public async Task InvokeAsync_GeneratesNewCorrelationId_WhenNoHeadersPresent() {
        // Arrange
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.IsTrue(_nextCalled, "Next delegate should be called");
        Assert.IsNotNull(_correlationIdInNext, "Correlation ID should be set");
        Assert.IsTrue(Guid.TryParse(_correlationIdInNext, out _), "Correlation ID should be a valid GUID");
    }

    [TestMethod]
    public async Task InvokeAsync_UsesXCorrelationIdHeader_WhenPresent() {
        // Arrange
        var expectedId = "test-correlation-id-123";
        _httpContext.Request.Headers["X-Correlation-Id"] = expectedId;
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.AreEqual(expectedId, _correlationIdInNext);
    }

    [TestMethod]
    public async Task InvokeAsync_UsesXRequestIdHeader_WhenPresent() {
        // Arrange
        var expectedId = "request-id-456";
        _httpContext.Request.Headers["X-Request-Id"] = expectedId;
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.IsTrue(_nextCalled);
        Assert.AreEqual(expectedId, _correlationIdInNext);
    }

    [TestMethod]
    public async Task InvokeAsync_PrefersXCorrelationIdOverXRequestId() {
        // Arrange
        var correlationId = "correlation-789";
        var requestId = "request-999";
        _httpContext.Request.Headers["X-Correlation-Id"] = correlationId;
        _httpContext.Request.Headers["X-Request-Id"] = requestId;
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.AreEqual(correlationId, _correlationIdInNext);
    }

    [TestMethod]
    public async Task InvokeAsync_SetsUpResponseHeaderCallback() {
        // Arrange
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert - verify correlation ID is stored in context
        Assert.IsNotNull(_correlationIdInNext);
        Assert.IsTrue(_httpContext.Items.ContainsKey("RxCorrelationId"));
        Assert.AreEqual(_correlationIdInNext, _httpContext.Items["RxCorrelationId"]);
    }

    [TestMethod]
    public async Task InvokeAsync_LogsDebugMessages_WhenLoggerEnabled() {
        // Arrange
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);
        _httpContext.Request.Method = "GET";
        _httpContext.Request.Path = "/test";

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.IsTrue(_logger.LogMessages.Any(m => m.Contains("Processing request with CorrelationId")));
        Assert.IsTrue(_logger.LogMessages.Any(m => m.Contains("Completed request with CorrelationId")));
    }

    [TestMethod]
    public async Task InvokeAsync_UsesActivityTraceId_WhenAvailable() {
        // Arrange
        var activity = new Activity("Test");
        activity.Start();
        var expectedTraceId = activity.TraceId.ToString();
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        try {
            // Act
            await middleware.InvokeAsync(_httpContext);

            // Assert - Should use Activity's TraceId
            Assert.AreEqual(expectedTraceId, _correlationIdInNext);
        }
        finally {
            activity.Stop();
        }
    }

    [TestMethod]
    public async Task InvokeAsync_GeneratesNewId_WhenHeadersEmpty() {
        // Arrange
        _httpContext.Request.Headers["X-Correlation-Id"] = "";
        _httpContext.Request.Headers["X-Request-Id"] = "  ";
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.IsNotNull(_correlationIdInNext);
        Assert.IsTrue(Guid.TryParse(_correlationIdInNext, out _));
    }

    [TestMethod]
    public async Task InvokeAsync_PropagatesExceptions() {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        var middleware = new RxCorrelationIdMiddleware(_ => throw expectedException, _logger);

        // Act & Assert
        var actualException = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(_httpContext));
        Assert.AreEqual(expectedException, actualException);
    }

    #endregion

    #region Extension Method Tests

    [TestMethod]
    public void GetCorrelationId_ReturnsId_WhenSet() {
        // Arrange
        var expectedId = "test-id-123";
        _httpContext.Items["RxCorrelationId"] = expectedId;

        // Act
        var actualId = _httpContext.GetCorrelationId();

        // Assert
        Assert.AreEqual(expectedId, actualId);
    }

    [TestMethod]
    public void GetCorrelationId_ReturnsNull_WhenNotSet() {
        // Act
        var id = _httpContext.GetCorrelationId();

        // Assert
        Assert.IsNull(id);
    }

    [TestMethod]
    public void SetCorrelationId_SetsValueInContext() {
        // Arrange
        var expectedId = "custom-id-456";

        // Act
        _httpContext.SetCorrelationId(expectedId);

        // Assert
        Assert.AreEqual(expectedId, _httpContext.Items["RxCorrelationId"]);
        Assert.AreEqual(expectedId, _httpContext.GetCorrelationId());
    }

    [TestMethod]
    public void SetCorrelationId_ThrowsOnNullContext() {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ((HttpContext)null!).SetCorrelationId("id"));
    }

    [TestMethod]
    public void SetCorrelationId_ThrowsOnNullOrEmptyId() {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _httpContext.SetCorrelationId(null!));
        Assert.ThrowsExactly<ArgumentException>(() =>
            _httpContext.SetCorrelationId(""));
        Assert.ThrowsExactly<ArgumentException>(() =>
            _httpContext.SetCorrelationId("  "));
    }

    [TestMethod]
    public void BeginCorrelationScope_CreatesScope_WhenIdPresent() {
        // Arrange
        var logger = new TestLogger<RxCorrelationIdTests>();
        var correlationId = "scope-test-789";
        _httpContext.SetCorrelationId(correlationId);

        // Act
        using var scope = logger.BeginCorrelationScope(_httpContext);

        // Assert
        Assert.IsNotNull(scope);
    }

    [TestMethod]
    public void BeginCorrelationScope_ReturnsNull_WhenNoId() {
        // Arrange
        var logger = new TestLogger<RxCorrelationIdTests>();

        // Act
        using var scope = logger.BeginCorrelationScope(_httpContext);

        // Assert
        Assert.IsNull(scope);
    }

    [TestMethod]
    public void BeginCorrelationScope_ThrowsOnNullLogger() {
        // Arrange
        ILogger logger = null!;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            logger.BeginCorrelationScope(_httpContext));
    }

    [TestMethod]
    public void BeginCorrelationScope_ThrowsOnNullContext() {
        // Arrange
        var logger = new TestLogger<RxCorrelationIdTests>();

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            logger.BeginCorrelationScope(null!));
    }

    #endregion

    #region Integration Tests

    [TestMethod]
    public async Task CorrelationId_FlowsThroughPipeline() {
        // Arrange
        var correlationId = "pipeline-test-123";
        _httpContext.Request.Headers["X-Correlation-Id"] = correlationId;
        var middleware = new RxCorrelationIdMiddleware(NextDelegate, _logger);

        string? idInMiddleware = null;
        var customNext = new RequestDelegate(ctx => {
            idInMiddleware = ctx.GetCorrelationId();
            return Task.CompletedTask;
        });
        middleware = new RxCorrelationIdMiddleware(customNext, _logger);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.AreEqual(correlationId, idInMiddleware);
        Assert.AreEqual(correlationId, _httpContext.GetCorrelationId());
    }

    [TestMethod]
    public async Task CorrelationId_WorksWithMultipleMiddlewareInstances() {
        // Arrange - Chain two middleware instances
        var middleware2 = new RxCorrelationIdMiddleware(NextDelegate, _logger);
        var middleware1 = new RxCorrelationIdMiddleware(ctx => middleware2.InvokeAsync(ctx), _logger);

        // Act
        await middleware1.InvokeAsync(_httpContext);

        // Assert - Should generate once and pass through
        Assert.IsTrue(_nextCalled);
        Assert.IsNotNull(_correlationIdInNext);
        Assert.IsTrue(Guid.TryParse(_correlationIdInNext, out _));
    }

    #endregion

    #region Helper Methods

    private Task NextDelegate(HttpContext context) {
        _nextCalled = true;
        _correlationIdInNext = context.GetCorrelationId();
        return Task.CompletedTask;
    }

    #endregion
}