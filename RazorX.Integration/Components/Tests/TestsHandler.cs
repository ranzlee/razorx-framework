using RazorX.Framework;
using RazorX.Integration.Components.Layout;

namespace RazorX.Integration.Components.Tests;

public class TestsHandler : IRequestHandler
{
    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/", Get);
        router.MapPost("/test-swap", TestSwap);
        router.MapPost("/test-morph", TestMorph);
    }

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddPage<App, TestsHead, TestsPage>("Integration Tests")
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
}
