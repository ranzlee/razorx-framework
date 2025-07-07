using RazorX.Framework;
using RazorX.Integration.Components.Layout;

namespace RazorX.Integration.Components.Error;

public class ErrorHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/error", Get);
    }

    public static async Task<IResult> Get(
        HttpContext context,
        int? code,
        IRxDriver rxDriver
    ) {
        var m = new ErrorModel(code ?? 404);
        return await rxDriver
            .With(context)
            .AddPage<App, ErrorPage, ErrorModel>(m, "Error")
            .Render();
    }
}