using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

public static class RxAntiforgeryExtensions {

    public static void UseRxAntiforgeryCookie(this WebApplication app) {
        app.UseMiddleware<RxAntiforgeryCookieMiddleware>();
    }
}

public sealed class RxAntiforgeryCookieMiddleware(RequestDelegate next) {
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery, ILogger<RxAntiforgeryCookieMiddleware> logger) {
        if (context.Request.Method.Trim().Equals("GET", StringComparison.CurrentCultureIgnoreCase)) {
            // Return an antiforgery token in the response for GET requests
            var tokenSet = antiforgery.GetAndStoreTokens(context);
            logger.LogTrace("Adding Antiforgery token cookie for {method}:{request}.",
                context.Request.Method,
                context.Request.GetDisplayUrl());
            context.Response.Cookies.Append("RequestVerificationToken", tokenSet.RequestToken!,
                new CookieOptions {
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                });
            await next(context);
            return;
        }
        // Validate antiforgery token for non-GET requests
        logger.LogTrace("Validating Antiforgery token for {method}:{request}.",
            context.Request.Method,
            context.Request.GetDisplayUrl());
        await antiforgery.ValidateRequestAsync(context);
        await next(context);
    }
}