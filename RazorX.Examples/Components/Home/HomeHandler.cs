using RazorX.Framework;
using RazorX.Examples.Components.Layout;

namespace RazorX.Examples.Components.Home;

public class HomeHandler : IRequestHandler {
    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/", Get);
    }

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver
            .With(context)
            .AddPage<App, HomeHead, HomePage>("RazorX - Home")
            .Render();
    }
}
