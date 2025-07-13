using RazorX.Framework;
using RazorX.Examples.Components.Layout;

namespace RazorX.Examples.Components.Examples;

public class ExamplesHandler : IRequestHandler {
    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/examples", Get);
    }

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddPage<App, ExamplesHead, ExamplesPage>("RazorX - Examples")
            .Render();
    }
}
