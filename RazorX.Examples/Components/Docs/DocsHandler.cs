using RazorX.Framework;
using RazorX.Examples.Components.Layout;

namespace RazorX.Examples.Components.Docs;

public class DocsHandler : RequestHandler {
    public override void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/docs", Get);
    }

    public static async Task<IResult> Get(HttpContext context, IRxDriver rxDriver) {
        return await rxDriver.RenderPage<App, DocsPage>(context, "RazorX - Docs");
    }
}
