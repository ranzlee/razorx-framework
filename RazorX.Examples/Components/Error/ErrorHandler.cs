using RazorX.Framework;
using RazorX.Examples.Components.Layout;

namespace RazorX.Examples.Components.Error;

public record ErrorModel(int Code);

public class ErrorHandler : IRequestHandler {

    public void MapRoutes(IEndpointRouteBuilder router) {
        router.Map("/error", Get);
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