using RazorX.Framework;
using RazorX.Integration.Components.Error;
using RazorX.Integration.Components.Layout;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

services.AddRxDriver();
services.AddHttpContextAccessor();
services.AddAntiforgery();
services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) {
    // Redirect errors to the error handler
    app.UseExceptionHandler(handler => {
        handler.Run(context => {
            if (context.Request.IsRxRequest()) {
                // Async response exception returns accepted with a location. The client will issue a GET for the error page
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                context.Response.Headers.Append("Location", "/error?code=500");
            } else {
                // Sync request redirected to the error page
                context.Response.Redirect("/error?code=500");
            }
            return Task.CompletedTask;
        });
    });
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseRxAntiforgeryCookie();

// Setup router group
app.MapGroup(string.Empty)
    .MapRoutes()
    .MapFallback(static async (HttpContext context, IRxDriver rxDriver) => {
        return await rxDriver
            .With(context)
            .AddPage<App, ErrorPage>("Error")
            .Render();
    });

app.Run();

