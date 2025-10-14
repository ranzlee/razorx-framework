using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using RazorX.Framework.Tests.Mocks;

namespace RazorX.Framework.Tests.Core;

[TestClass]
public class RxResponseBuilderSseTests {
    private TestLogger<RxDriver> _logger = null!;
    private MockHtmlRenderer _mockRenderer = null!;
    private IRxDriver _rxDriver = null!;
    private DefaultHttpContext _httpContext = null!;

    [TestInitialize]
    public void SetUp() {
        _logger = new TestLogger<RxDriver>();
        _mockRenderer = new MockHtmlRenderer();
        var memoryPool = new RxMemoryPool();
        _rxDriver = new RxDriver(_mockRenderer, _logger, memoryPool);
        _httpContext = new DefaultHttpContext();
    }

    [TestCleanup]
    public void TearDown() {
        _rxDriver?.Dispose();
    }

    #region RenderSse Basic Tests

    [TestMethod]
    public void RenderSse_WithSingleFragment_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder.AddFragment<SseTestComponent, SseTestModel>(model, "test-target", FragmentMergeStrategyType.Swap);
                    await Task.CompletedTask;
                }
            );

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.GetType().Name.Contains("ServerSentEvents"));
    }

    [TestMethod]
    public void RenderSse_WithMultipleFragmentsAndTargets_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder
                        .AddFragment<SseTestComponent, SseTestModel>(model, "target1", FragmentMergeStrategyType.Swap)
                        .AddFragment<SseTestComponent, SseTestModel>(model, "target2", FragmentMergeStrategyType.AppendBeforeEnd);
                    await Task.CompletedTask;
                }
            );

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RenderSse_WithToastTrigger_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder.AddTriggerToast($"Message: {model.Message}", ToastType.Success, 3000);
                    await Task.CompletedTask;
                }
            );

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RenderSse_WithResetFormTrigger_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder.AddTriggerResetForm(new[] { "form-id", "input-id", "textarea-id" });
                    await Task.CompletedTask;
                }
            );

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RenderSse_WithAllTriggers_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder
                        .AddTriggerToast("Toast message", ToastType.Info, 3000)
                        .AddTriggerFocusElement("input-id", positionCursorEnd: true)
                        .AddTriggerSetState("key1", "value1", MetadataScope.Session, updateUrl: true)
                        .AddTriggerCloseDialog("dialog-id")
                        .AddTriggerResetForm(new[] { "form-1", "input-1" });
                    await Task.CompletedTask;
                }
            );

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RenderSse_WithRemoveElement_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder.RemoveElement("element-to-remove");
                    await Task.CompletedTask;
                }
            );

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RenderSse_WithFragmentAndMultipleTriggers_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder
                        .AddFragment<SseTestComponent, SseTestModel>(model, "notification-area", FragmentMergeStrategyType.AppendBeforeEnd)
                        .AddTriggerToast($"New: {model.Message}", ToastType.Success, 3000)
                        .AddTriggerFocusElement("notification-area")
                        .RemoveElement("old-notification");
                    await Task.CompletedTask;
                }
            );

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RenderSse_WithCancellationToken_ReturnsValidResult() {
        // Arrange
        using var cts = new CancellationTokenSource();
        var models = CreateInfiniteModelStream(cts.Token);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder.AddFragment<SseTestComponent, SseTestModel>(model, "target");
                    await Task.CompletedTask;
                },
                cancellationToken: cts.Token
            );

        // Assert
        Assert.IsNotNull(result);

        // Cleanup
        cts.Cancel();
    }

    [TestMethod]
    public void RenderSse_WithNullModels_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _rxDriver
                .With(_httpContext)
                .RenderSse<SseTestModel>(
                    null!,
                    async (model, builder) => await Task.CompletedTask
                )
        );
    }

    [TestMethod]
    public void RenderSse_WithNullConfigureEvent_ThrowsArgumentNullException() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _rxDriver
                .With(_httpContext)
                .RenderSse(
                    models,
                    null!
                )
        );
    }

    [TestMethod]
    public void RenderSse_CalledTwice_ThrowsInvalidOperationException() {
        // Arrange
        var models1 = CreateTestModelStream(1);
        var models2 = CreateTestModelStream(1);
        var builder = _rxDriver.With(_httpContext);

        // Act
        builder.RenderSse(
            models1,
            async (model, b) => await Task.CompletedTask
        );

        // Assert
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.RenderSse(
                models2,
                async (model, b) => await Task.CompletedTask
            )
        );

        Assert.IsTrue(exception.Message.Contains("Render has already been called"));
    }

    [TestMethod]
    public void RenderSse_AfterRenderCalled_ThrowsInvalidOperationException() {
        // Arrange
        var models = CreateTestModelStream(1);
        var builder = _rxDriver.With(_httpContext);
        _httpContext.Request.Headers["rx-request"] = "";

        // Act - call Render() first
        builder.AddFragment<SseTestComponent>("test");
        var renderTask = builder.Render();

        // Assert - RenderSse() should throw
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            builder.RenderSse(
                models,
                async (model, b) => await Task.CompletedTask
            )
        );

        Assert.IsTrue(exception.Message.Contains("Render has already been called"));
    }

    [TestMethod]
    public async Task RenderSse_WithCancellationRequested_ThrowsOperationCanceledException() {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var models = CreateTestModelStream(1);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            Task.Run(() =>
                _rxDriver
                    .With(_httpContext)
                    .RenderSse(
                        models,
                        async (model, builder) => await Task.CompletedTask,
                        cancellationToken: cts.Token
                    )
            )
        );
    }

    [TestMethod]
    public void RenderSse_WithCustomEventType_ReturnsValidResult() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act
        var result = _rxDriver
            .With(_httpContext)
            .RenderSse(
                models,
                async (model, builder) => {
                    builder.AddFragment<SseTestComponent, SseTestModel>(model, "test-target");
                    await Task.CompletedTask;
                },
                eventType: "rx-custom-event"
            );

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.GetType().Name.Contains("ServerSentEvents"));
    }

    [TestMethod]
    public void RenderSse_WithEmptyEventType_ThrowsArgumentException() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>(() =>
            _rxDriver
                .With(_httpContext)
                .RenderSse(
                    models,
                    async (model, builder) => await Task.CompletedTask,
                    eventType: ""
                )
        );
    }

    [TestMethod]
    public void RenderSse_WithNullEventType_ThrowsArgumentNullException() {
        // Arrange
        var models = CreateTestModelStream(1);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            _rxDriver
                .With(_httpContext)
                .RenderSse(
                    models,
                    async (model, builder) => await Task.CompletedTask,
                    eventType: null!
                )
        );
    }

    #endregion

    #region Helper Methods

    private static async IAsyncEnumerable<SseTestModel> CreateTestModelStream(int count) {
        for (int i = 0; i < count; i++) {
            yield return new SseTestModel { Message = $"Message {i}" };
            await Task.Delay(1);
        }
    }

    private static async IAsyncEnumerable<SseTestModel> CreateInfiniteModelStream([EnumeratorCancellation] CancellationToken ct) {
        int i = 0;
        while (!ct.IsCancellationRequested) {
            yield return new SseTestModel { Message = $"Message {i++}" };
            await Task.Delay(10, ct);
        }
    }

    #endregion
}

#region Test Components and Models

public class SseTestModel {
    public string Message { get; set; } = "";
}

public class SseTestComponent : ComponentBase, IComponentModel<SseTestModel> {
    [Parameter] public SseTestModel Model { get; set; } = null!;
}

#endregion
