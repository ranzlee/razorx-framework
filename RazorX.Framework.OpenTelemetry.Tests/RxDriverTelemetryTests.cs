using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework.OpenTelemetry.Tests;

[TestClass]
public class RxDriverTelemetryTests {
    private ServiceProvider CreateServiceProvider() {
        var services = new ServiceCollection();
        services.AddRxDriver();
        services.AddLogging(builder => builder.AddConsole());
        return services.BuildServiceProvider();
    }
    private HttpContext CreateHttpContext() {
        var context = new DefaultHttpContext();
        context.Request.Headers["rx-request"] = "true";
        return context;
    }
    public record TestPageModel(string Message);
    public class TestPage : ComponentBase, IComponentModel<TestPageModel> {
        [Parameter] public TestPageModel Model { get; set; } = null!;
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) {
            builder.AddContent(0, $"<div>{Model.Message}</div>");
        }
    }
    public class TestRoot : ComponentBase, IRootComponent {
        [Parameter] public Type? HeadContent { get; set; }
        [Parameter] public Dictionary<string, object?> HeadContentParameters { get; set; } = new();
        [Parameter] public Type MainContent { get; set; } = null!;
        [Parameter] public Dictionary<string, object?> MainContentParameters { get; set; } = new();
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) {
            builder.OpenElement(0, "html");
            builder.OpenElement(1, "body");
            if (MainContent != null) {
                builder.OpenComponent(2, MainContent);
                if (MainContentParameters != null) {
                    foreach (var param in MainContentParameters) {
                        builder.AddAttribute(3, param.Key, param.Value);
                    }
                }
                builder.CloseComponent();
            }
            builder.CloseElement();
            builder.CloseElement();
        }
    }
    public class TestFragment : ComponentBase, IComponentModel<TestPageModel> {
        [Parameter] public TestPageModel Model { get; set; } = null!;
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) {
            builder.AddContent(0, $"<div id='fragment'>{Model.Message}</div>");
        }
    }
    [TestMethod]
    public async Task RenderPage_DoesNotThrow_WithInstrumentation() {
        var serviceProvider = CreateServiceProvider();
        var driver = serviceProvider.GetRequiredService<IRxDriver>();
        var context = CreateHttpContext();
        context.Request.Headers.Remove("rx-request");
        await driver.RenderPage<TestRoot, TestPage, TestPageModel>(
            context,
            new TestPageModel("Test"));
        await driver.DisposeAsync();
    }
    [TestMethod]
    public async Task Render_DoesNotThrow_WithInstrumentation() {
        var serviceProvider = CreateServiceProvider();
        var driver = serviceProvider.GetRequiredService<IRxDriver>();
        var context = CreateHttpContext();
        await driver
            .With(context)
            .AddFragment<TestFragment, TestPageModel>(new TestPageModel("Test"), "target")
            .Render();
        await driver.DisposeAsync();
    }
    [TestMethod]
    public async Task Render_WithMultipleFragments_DoesNotThrow() {
        var serviceProvider = CreateServiceProvider();
        var driver = serviceProvider.GetRequiredService<IRxDriver>();
        var context = CreateHttpContext();
        await driver
            .With(context)
            .AddFragment<TestFragment, TestPageModel>(new TestPageModel("Test1"), "target1")
            .AddFragment<TestFragment, TestPageModel>(new TestPageModel("Test2"), "target2")
            .AddFragment<TestFragment, TestPageModel>(new TestPageModel("Test3"), "target3")
            .Render();
        await driver.DisposeAsync();
    }
}
