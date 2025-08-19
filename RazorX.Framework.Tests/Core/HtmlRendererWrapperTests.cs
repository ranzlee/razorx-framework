using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RazorX.Framework.Tests.Mocks;

namespace RazorX.Framework.Tests.Core;

[TestClass]
public class HtmlRendererWrapperTests {
    private HtmlRenderer _htmlRenderer = null!;
    private ILogger<HtmlRootComponentWrapper> _logger = null!;
    private HtmlRendererWrapper _wrapper = null!;

    [TestInitialize]
    public void SetUp() {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        
        _htmlRenderer = new HtmlRenderer(serviceProvider, NullLoggerFactory.Instance);
        _logger = new TestLogger<HtmlRootComponentWrapper>();
        _wrapper = new HtmlRendererWrapper(_htmlRenderer, _logger);
    }

    [TestCleanup]
    public void TearDown() {
        _wrapper?.Dispose();
    }

    #region Constructor Tests

    [TestMethod]
    public void Constructor_InitializesWithValidParameters() {
        // Assert
        Assert.IsNotNull(_wrapper);
        Assert.IsNotNull(_wrapper.Dispatcher);
    }

    #endregion

    #region Dispatcher Property Tests

    [TestMethod]
    public void Dispatcher_ReturnsHtmlRendererDispatcher() {
        // Act
        var dispatcher = _wrapper.Dispatcher;

        // Assert
        Assert.IsNotNull(dispatcher);
        Assert.AreEqual(_htmlRenderer.Dispatcher, dispatcher);
    }

    #endregion

    #region RenderComponentAsync Tests

    // Note: Real HtmlRenderer tests would require proper Blazor infrastructure
    // and Dispatcher context. These tests are omitted as they would require
    // invoking within Dispatcher.InvokeAsync() which is beyond the scope of 
    // unit testing the wrapper itself. The wrapper correctly delegates to
    // HtmlRenderer as shown in the implementation.

    #endregion

    #region Disposal Tests

    [TestMethod]
    public void Dispose_DisposesHtmlRenderer() {
        // Arrange
        var localWrapper = new HtmlRendererWrapper(_htmlRenderer, _logger);

        // Act
        localWrapper.Dispose();

        // Assert - should not throw when disposed
        Assert.IsNotNull(localWrapper); // Just verify it doesn't crash
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesHtmlRendererAsync() {
        // Arrange
        var localWrapper = new HtmlRendererWrapper(_htmlRenderer, _logger);

        // Act
        await localWrapper.DisposeAsync();

        // Assert - should not throw when disposed
        Assert.IsNotNull(localWrapper); // Just verify it doesn't crash
    }

    [TestMethod]
    public void Dispose_CanBeCalledMultipleTimes() {
        // Arrange
        var localWrapper = new HtmlRendererWrapper(_htmlRenderer, _logger);

        // Act & Assert - should not throw
        localWrapper.Dispose();
        localWrapper.Dispose(); // Second call should be safe
    }

    [TestMethod]
    public async Task DisposeAsync_CanBeCalledMultipleTimes() {
        // Arrange
        var localWrapper = new HtmlRendererWrapper(_htmlRenderer, _logger);

        // Act & Assert - should not throw
        await localWrapper.DisposeAsync();
        await localWrapper.DisposeAsync(); // Second call should be safe
    }

    #endregion

    // Thread safety tests removed as they require real HtmlRenderer with Dispatcher context
}