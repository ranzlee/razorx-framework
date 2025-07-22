using RazorX.Framework;

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
            IResult result = context.Request.IsRxRequest()
                ? TypedResults.Accepted("/error?code=500")
                : TypedResults.Redirect("/error?code=500");
            return result.ExecuteAsync(context);
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
    .MapFallback(static (HttpContext context) => {
        IResult result = context.Request.IsRxRequest()
            ? TypedResults.Accepted("/error?code=404")
            : TypedResults.Redirect("/error?code=404");
        return result;
    });

app.Run();

