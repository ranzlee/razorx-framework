using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using RazorX.Framework.Tests.Mocks;

namespace RazorX.Framework.Tests.Core;

[TestClass]
public class RxDriverTests {
    private TestLogger<RxDriver> _logger = null!;
    private MockHtmlRenderer _mockRenderer = null!;
    private IRxDriver _rxDriver = null!;
    private DefaultHttpContext _httpContext = null!;

    [TestInitialize]
    public void SetUp() {
        _logger = new TestLogger<RxDriver>();
        _mockRenderer = new MockHtmlRenderer();
        _rxDriver = new RxDriver(_mockRenderer, _logger);
        _httpContext = new DefaultHttpContext();
    }

    [TestCleanup]
    public void TearDown() {
        _rxDriver?.Dispose();
    }

    #region Disposal Tests

    [TestMethod]
    public void Dispose_SetsDisposedFlag() {
        // Act
        _rxDriver.Dispose();

        // Assert
        var exception = Assert.ThrowsException<ObjectDisposedException>(() => _rxDriver.With(_httpContext));
        Assert.AreEqual("RxDriver", exception.ObjectName);
    }

    [TestMethod]
    public async Task DisposeAsync_SetsDisposedFlag() {
        // Act
        await _rxDriver.DisposeAsync();

        // Assert
        var exception = Assert.ThrowsException<ObjectDisposedException>(() => _rxDriver.With(_httpContext));
        Assert.AreEqual("RxDriver", exception.ObjectName);
    }

    [TestMethod]
    public void Dispose_LogsDebugMessage() {
        // Act
        _rxDriver.Dispose();

        // Assert
        Assert.IsTrue(_logger.LogMessages.Any(msg => msg.Contains("Disposing RxDriver")));
        Assert.IsTrue(_logger.LogMessages.Any(msg => msg.Contains("RxDriver Disposed successfully")));
    }

    [TestMethod]
    public async Task DisposeAsync_LogsDebugMessage() {
        // Act
        await _rxDriver.DisposeAsync();

        // Assert
        Assert.IsTrue(_logger.LogMessages.Any(msg => msg.Contains("Async Disposing RxDriver")));
        Assert.IsTrue(_logger.LogMessages.Any(msg => msg.Contains("RxDriver Async Disposed successfully")));
    }

    [TestMethod]
    public void Dispose_WhenCalledTwice_DoesNotThrow() {
        // Act
        _rxDriver.Dispose();
        _rxDriver.Dispose(); // Should not throw

        // Assert - No exception thrown
    }

    [TestMethod]
    public async Task DisposeAsync_WhenCalledTwice_DoesNotThrow() {
        // Act
        await _rxDriver.DisposeAsync();
        await _rxDriver.DisposeAsync(); // Should not throw

        // Assert - No exception thrown
    }

    #endregion

    #region With Method Tests

    [TestMethod]
    public void With_ReturnsResponseBuilder() {
        // Act
        var builder = _rxDriver.With(_httpContext);

        // Assert
        Assert.IsNotNull(builder);
        Assert.IsInstanceOfType<IRxResponseBuilder>(builder);
    }

    [TestMethod]
    public void With_WhenDisposed_ThrowsObjectDisposedException() {
        // Arrange
        _rxDriver.Dispose();

        // Act & Assert
        var exception = Assert.ThrowsException<ObjectDisposedException>(() => _rxDriver.With(_httpContext));
        Assert.AreEqual("RxDriver", exception.ObjectName);
    }

    #endregion

    #region Page Rendering Tests

    [TestMethod]
    public async Task AddPage_WithModel_SetsCorrectParameters() {
        // Arrange
        var model = new TestModel { Value = "test" };
        
        // Act
        var result = await _rxDriver
            .With(_httpContext)
            .AddPage<TestRootComponent, TestPageComponent, TestModel>(model, "Test Title")
            .Render();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(_mockRenderer.RenderCalled);
        Assert.AreEqual(typeof(TestRootComponent), _mockRenderer.LastComponentType);
        
        var parameters = _mockRenderer.LastParametersDictionary;
        Assert.AreEqual("Test Title", parameters["Title"]);
        Assert.AreEqual(typeof(TestPageComponent), parameters["MainContent"]);
        
        var mainContentParams = parameters["MainContentParameters"] as Dictionary<string, object?>;
        Assert.IsNotNull(mainContentParams);
        Assert.AreEqual(model, mainContentParams["Model"]);
    }

    [TestMethod]
    public async Task AddPage_WithoutModel_SetsCorrectParameters() {
        // Act
        var result = await _rxDriver
            .With(_httpContext)
            .AddPage<TestRootComponent, TestPageComponent>("Test Title")
            .Render();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(_mockRenderer.RenderCalled);
        Assert.AreEqual(typeof(TestRootComponent), _mockRenderer.LastComponentType);
        
        var parameters = _mockRenderer.LastParametersDictionary;
        Assert.AreEqual("Test Title", parameters["Title"]);
        Assert.AreEqual(typeof(TestPageComponent), parameters["MainContent"]);
    }

    [TestMethod]
    public async Task AddPage_WithHeadContent_SetsCorrectParameters() {
        // Arrange
        var model = new TestModel { Value = "test" };
        
        // Act
        var result = await _rxDriver
            .With(_httpContext)
            .AddPage<TestRootComponent, TestHeadComponent, TestPageComponent, TestModel>(model, "Test Title")
            .Render();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(_mockRenderer.RenderCalled);
        
        var parameters = _mockRenderer.LastParametersDictionary;
        Assert.AreEqual("Test Title", parameters["Title"]);
        Assert.AreEqual(typeof(TestPageComponent), parameters["MainContent"]);
        Assert.AreEqual(typeof(TestHeadComponent), parameters["HeadContent"]);
    }

    #endregion

    #region Fragment Tests

    [TestMethod]
    public async Task AddFragment_WithModel_GeneratesCorrectTemplate() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        var model = new TestModel { Value = "test" };
        
        // Act
        var result = await _rxDriver
            .With(_httpContext)
            .AddFragment<TestFragmentComponent, TestModel>(model, "test-target", FragmentMergeStrategyType.Swap)
            .Render();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(_mockRenderer.RenderCalled);
        
        // Verify fragment was rendered
        var responseContent = _mockRenderer.LastRenderedContent;
        Assert.IsTrue(responseContent.Contains("Mock Fragment Content"));
        
        // Note: The template wrapper is added by RxDriver internally
        // and not accessible through the mock renderer interface
    }

    [TestMethod]
    public async Task AddFragment_WithoutModel_GeneratesCorrectTemplate() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        var result = await _rxDriver
            .With(_httpContext)
            .AddFragment<TestFragmentComponent>("test-target", FragmentMergeStrategyType.Swap)
            .Render();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(_mockRenderer.RenderCalled);
        
        var responseContent = _mockRenderer.LastRenderedContent;
        Assert.IsTrue(responseContent.Contains("Mock Fragment Content"));
    }

    [TestMethod]
    public async Task AddFragment_SetsCorrectMergeStrategyHeader() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddFragment<TestFragmentComponent>("test-target", FragmentMergeStrategyType.Morph)
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-merge"));
        var mergeHeader = _httpContext.Response.Headers["rx-merge"].ToString();
        Assert.IsTrue(mergeHeader.Contains("\"target\":\"test-target\""));
        Assert.IsTrue(mergeHeader.Contains("\"strategy\":\"morph\""));
    }

    [TestMethod]
    public async Task RemoveElement_SetsCorrectMergeStrategy() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .RemoveElement("test-target")
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-merge"));
        var mergeHeader = _httpContext.Response.Headers["rx-merge"].ToString();
        Assert.IsTrue(mergeHeader.Contains("\"target\":\"test-target\""));
        Assert.IsTrue(mergeHeader.Contains("\"strategy\":\"remove\""));
    }

    #endregion

    #region Trigger Tests

    [TestMethod]
    public async Task AddTriggerCloseDialog_SetsCorrectHeader() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddTriggerCloseDialog("test-dialog", "close-data", "test-form")
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-trigger-close-dialog"));
        var triggerHeader = _httpContext.Response.Headers["rx-trigger-close-dialog"].ToString();
        Assert.IsTrue(triggerHeader.Contains("\"dialogId\":\"test-dialog\""));
        Assert.IsTrue(triggerHeader.Contains("\"onCloseData\":\"close-data\""));
        Assert.IsTrue(triggerHeader.Contains("\"resetFormId\":\"test-form\""));
    }

    [TestMethod]
    public async Task AddTriggerFocusElement_SetsCorrectHeader() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddTriggerFocusElement("test-element", true)
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-trigger-focus-element"));
        var triggerHeader = _httpContext.Response.Headers["rx-trigger-focus-element"].ToString();
        Assert.IsTrue(triggerHeader.Contains("\"elementId\":\"test-element\""));
        Assert.IsTrue(triggerHeader.Contains("\"positionCursorEnd\":true"));
    }

    [TestMethod]
    public async Task AddTriggerSetState_SetsCorrectHeader() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddTriggerSetState("test-key", "test-value", MetadataScope.Session)
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-trigger-set-state"));
        var triggerHeader = _httpContext.Response.Headers["rx-trigger-set-state"].ToString();
        Assert.IsTrue(triggerHeader.Contains("\"key\":\"test-key\""));
        Assert.IsTrue(triggerHeader.Contains("\"value\":\"test-value\""));
        Assert.IsTrue(triggerHeader.Contains("\"scope\":\"Session\""));
    }

    [TestMethod]
    public async Task AddTriggerSetStateBatch_SetsMultipleHeaders() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        var state = new Dictionary<string, string> {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddTriggerSetStateBatch(state, MetadataScope.Persistent)
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-trigger-set-state"));
        var triggerHeaders = _httpContext.Response.Headers["rx-trigger-set-state"];
        Assert.AreEqual(2, triggerHeaders.Count);
        
        var allHeaders = string.Join(",", (IEnumerable<string>)triggerHeaders);
        Assert.IsTrue(allHeaders.Contains("\"key\":\"key1\""));
        Assert.IsTrue(allHeaders.Contains("\"value\":\"value1\""));
        Assert.IsTrue(allHeaders.Contains("\"key\":\"key2\""));
        Assert.IsTrue(allHeaders.Contains("\"value\":\"value2\""));
        Assert.IsTrue(allHeaders.Contains("\"scope\":\"Persistent\""));
    }

    [TestMethod]
    public async Task AddTriggerSetStateBatch_WithUpdateUrl_SetsUpdateUrlInHeaders() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        var state = new Dictionary<string, string> {
            ["filter"] = "active",
            ["page"] = "2"
        };
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddTriggerSetStateBatch(state, MetadataScope.Session, updateUrl: true)
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-trigger-set-state"));
        var triggerHeaders = _httpContext.Response.Headers["rx-trigger-set-state"];
        Assert.AreEqual(2, triggerHeaders.Count);
        
        var allHeaders = string.Join(",", (IEnumerable<string>)triggerHeaders);
        Assert.IsTrue(allHeaders.Contains("\"key\":\"filter\""));
        Assert.IsTrue(allHeaders.Contains("\"value\":\"active\""));
        Assert.IsTrue(allHeaders.Contains("\"key\":\"page\""));
        Assert.IsTrue(allHeaders.Contains("\"value\":\"2\""));
        Assert.IsTrue(allHeaders.Contains("\"scope\":\"Session\""));
        Assert.IsTrue(allHeaders.Contains("\"updateUrl\":true"));
    }

    [TestMethod]
    public async Task AddTriggerSetState_WithUpdateUrl_SetsUpdateUrlInHeaders() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddTriggerSetState("theme", "dark", MetadataScope.Session, updateUrl: true)
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-trigger-set-state"));
        var triggerHeader = _httpContext.Response.Headers["rx-trigger-set-state"].ToString();
        Assert.IsTrue(triggerHeader.Contains("\"key\":\"theme\""));
        Assert.IsTrue(triggerHeader.Contains("\"value\":\"dark\""));
        Assert.IsTrue(triggerHeader.Contains("\"scope\":\"Session\""));
        Assert.IsTrue(triggerHeader.Contains("\"updateUrl\":true"));
    }

    [TestMethod]
    public async Task AddTriggerSetState_WithoutUpdateUrl_DefaultsToFalse() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";
        
        // Act
        await _rxDriver
            .With(_httpContext)
            .AddTriggerSetState("setting", "value", MetadataScope.Session)
            .Render();

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-trigger-set-state"));
        var triggerHeader = _httpContext.Response.Headers["rx-trigger-set-state"].ToString();
        Assert.IsTrue(triggerHeader.Contains("\"key\":\"setting\""));
        Assert.IsTrue(triggerHeader.Contains("\"value\":\"value\""));
        Assert.IsTrue(triggerHeader.Contains("\"scope\":\"Session\""));
        Assert.IsTrue(triggerHeader.Contains("\"updateUrl\":false"));
    }

    #endregion

    #region State Validation Tests

    [TestMethod]
    public void AddTriggerSetState_WithInvalidKey_ThrowsArgumentException() {
        // Arrange
        var builder = _rxDriver.With(_httpContext);

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(() => 
            builder.AddTriggerSetState("invalid key!", "value"));
        Assert.IsTrue(exception.Message.Contains("State key 'invalid key!' contains invalid characters"));
    }

    [TestMethod]
    public void AddTriggerSetState_WithEmptyKey_ThrowsArgumentException() {
        // Arrange
        var builder = _rxDriver.With(_httpContext);

        // Act & Assert
        var exception = Assert.ThrowsException<ArgumentException>(() => 
            builder.AddTriggerSetState("", "value"));
        Assert.IsTrue(exception.Message.Contains("State key cannot be null or empty"));
    }

    [TestMethod]
    public void AddTriggerSetState_WithDuplicateKey_ThrowsInvalidOperationException() {
        // Arrange
        var builder = _rxDriver.With(_httpContext);

        // Act & Assert
        builder.AddTriggerSetState("test-key", "value1");
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            builder.AddTriggerSetState("test-key", "value2"));
        Assert.IsTrue(exception.Message.Contains("State key 'test-key' has already been set"));
    }

    [TestMethod]
    public void AddTriggerSetStateBatch_WithDuplicateKey_ThrowsInvalidOperationException() {
        // Arrange
        var builder = _rxDriver.With(_httpContext);
        
        // First add a state key
        builder.AddTriggerSetState("key1", "value1", MetadataScope.Session);
        
        // Then try to add it again in a batch - this should fail
        var state = new Dictionary<string, string> {
            ["key1"] = "value2" // This key was already set above
        };

        // Act & Assert
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            builder.AddTriggerSetStateBatch(state, MetadataScope.Session));
        Assert.IsTrue(exception.Message.Contains("State key 'key1' has already been set"));
    }

    #endregion

    #region Render Tests

    [TestMethod]
    public async Task Render_WithoutRxRequest_ForFragments_ThrowsInvalidOperationException() {
        // Arrange
        var builder = _rxDriver.With(_httpContext);

        // Act & Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => 
            builder.AddFragment<TestFragmentComponent>("test").Render());
        Assert.IsTrue(exception.Message.Contains("Partial rendering is not supported for synchronous requests"));
    }

    [TestMethod]
    public async Task Render_CalledTwice_ThrowsInvalidOperationException() {
        // Arrange
        var builder = _rxDriver
            .With(_httpContext)
            .AddPage<TestRootComponent, TestPageComponent>();

        // Act
        await builder.Render();

        // Assert
        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => 
            builder.Render());
        Assert.IsTrue(exception.Message.Contains("Render has already been called"));
    }

    [TestMethod]
    public async Task Render_WithNoContent_ReturnsNoContent() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";

        // Act
        var result = await _rxDriver
            .With(_httpContext)
            .Render();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType(result, typeof(NoContent));
        
        // Verify that rx-merge header is still present even with no content
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-merge"));
        var mergeHeader = _httpContext.Response.Headers["rx-merge"].ToString();
        Assert.AreEqual("[]", mergeHeader);
    }

    [TestMethod]
    public async Task Render_WithIgnoreActiveElementOnMorph_SetsHeader() {
        // Arrange
        _httpContext.Request.Headers["rx-request"] = "";

        // Act
        await _rxDriver
            .With(_httpContext)
            .AddFragment<TestFragmentComponent>("test", FragmentMergeStrategyType.Morph)
            .Render(ignoreActiveElementValueOnMorph: true);

        // Assert
        Assert.IsTrue(_httpContext.Response.Headers.ContainsKey("rx-morph-ignore-active"));
    }

    #endregion

    #region Builder State Tests

    [TestMethod]
    public void AddPage_CalledTwice_ThrowsInvalidOperationException() {
        // Arrange
        var builder = _rxDriver.With(_httpContext);

        // Act
        builder.AddPage<TestRootComponent, TestPageComponent>();

        // Assert
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            builder.AddPage<TestRootComponent, TestPageComponent>());
        Assert.IsTrue(exception.Message.Contains("RxDriver is set to render a page"));
    }

    [TestMethod]
    public void AddFragment_AfterAddPage_ThrowsInvalidOperationException() {
        // Arrange
        var builder = _rxDriver.With(_httpContext);

        // Act
        builder.AddPage<TestRootComponent, TestPageComponent>();

        // Assert
        var exception = Assert.ThrowsException<InvalidOperationException>(() => 
            builder.AddFragment<TestFragmentComponent>("test"));
        Assert.IsTrue(exception.Message.Contains("RxDriver is set to render a page"));
    }

    #endregion
}

#region Test Components and Models

public class TestModel {
    public string Value { get; set; } = "";
}

public class TestRootComponent : ComponentBase, IRootComponent {
    [Parameter] public Type? HeadContent { get; set; }
    [Parameter] public Type MainContent { get; set; } = null!;
    [Parameter] public Dictionary<string, object?> MainContentParameters { get; set; } = [];
    [Parameter] public string? Title { get; set; }
}

public class TestPageComponent : ComponentBase, IComponentModel<TestModel> {
    [Parameter] public TestModel Model { get; set; } = null!;
}

public class TestHeadComponent : ComponentBase { }

public class TestFragmentComponent : ComponentBase, IComponentModel<TestModel> {
    [Parameter] public TestModel Model { get; set; } = null!;
}

#endregion