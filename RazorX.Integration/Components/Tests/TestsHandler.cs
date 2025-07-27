using RazorX.Framework;
using RazorX.Integration.Components.Layout;

namespace RazorX.Integration.Components.Tests;

public class TestsHandler : IRequestHandler {
    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/", Get);
        router.MapPost("/test-swap", TestSwap);
        router.MapPost("/test-morph", TestMorph);
        router.MapPost("/test-remove", TestRemove);
        router.MapPost("/test-all-merge", TestAllMerge);
        router.MapPost("/test-with-trigger", TestWithTrigger);
        router.MapPost("/test-target-with-trigger-remove", TestTriggerRemove);
        router.MapPost("/test-adjacent-target", TestAdjacentSwap);
    }

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddPage<App, TestsHead, TestsPage>("RazorX Framework Integration Tests")
            .Render();
    }

    public static async Task<IResult> TestSwap(HttpContext context, IRxDriver rxDriver) {
        if (context.Request.Headers.TryGetValue("test", out var val)) {
            context.Response.Headers.Append("test", val);
        }
        return await rxDriver
            .With(context)
            .AddFragment<TestsTarget, string>("swap", "test-swap-target", FragmentMergeStrategyType.Swap)
            .Render();
    }

    public static async Task<IResult> TestMorph(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddFragment<TestsTarget, string>("morph", "test-morph-target", FragmentMergeStrategyType.Morph)
            .Render();
    }

    public static async Task<IResult> TestRemove(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .RemoveElement("test-remove-target")
            .Render();
    }

    public static async Task<IResult> TestAllMerge(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddFragment<TestsTarget, string>("swap", "test-swap-target", FragmentMergeStrategyType.Swap)
            .AddFragment<TestsTarget, string>("morph", "test-morph-target", FragmentMergeStrategyType.Morph)
            .RemoveElement("test-remove-target")
            .Render();
    }

    public static async Task<IResult> TestWithTrigger(HttpContext context, IRxDriver rxDriver, NameValueModel model) {
        var driver = rxDriver.With(context);
        if (model.Mode == "swap") {
            driver.AddFragment<TestsTargetWithTrigger, string>("swap", "swap-target-with-trigger", FragmentMergeStrategyType.Swap);
        } else {
            driver.AddFragment<TestsTargetWithTrigger, string>("morph", "morph-target-with-trigger", FragmentMergeStrategyType.Morph);
        }
        return await driver.Render();
    }

    public static async Task<IResult> TestTriggerRemove(HttpContext context, IRxDriver rxDriver, NameValueModel model) {
        return await rxDriver
            .With(context)
            .RemoveElement($"new-{model.Mode}-target-with-trigger")
            .Render();
    }

    public static async Task<IResult> TestAdjacentSwap(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddFragment<TestsAdjacentTarget>("test-adjacent-target", FragmentMergeStrategyType.AppendBeforeEnd)
            .Render();
    }
}
