using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RazorX.Framework;

/// <summary>
/// Configuration options for RazorX Antiforgery
/// </summary>
public class RxAntiforgeryOptions {
    /// <summary>
    /// The name of the cookie used for the request verification token
    /// </summary>
    public string RequestVerificationTokenCookieName { get; set; } = "RequestVerificationToken";
}

public static class RxAntiforgeryExtensions {

    /// <summary>
    /// Adds RazorX Antiforgery options to the service collection
    /// </summary>
    public static IServiceCollection AddRxAntiforgery(this IServiceCollection services, Action<RxAntiforgeryOptions>? configure = null) {
        services.Configure<RxAntiforgeryOptions>(options => {
            configure?.Invoke(options);
        });
        return services;
    }

    /// <summary>
    /// Uses the RazorX Antiforgery cookie middleware
    /// </summary>
    public static void UseRxAntiforgeryCookie(this WebApplication app) {
        app.UseMiddleware<RxAntiforgeryCookieMiddleware>();
    }
}

public sealed class RxAntiforgeryCookieMiddleware(RequestDelegate next) {
    
    private static bool IsGetMethod(string method) {
        // Use ReadOnlySpan to avoid string allocations when trimming
        ReadOnlySpan<char> methodSpan = method.AsSpan().Trim();
        return methodSpan.Equals("GET".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }
    public async Task InvokeAsync(
        HttpContext context, 
        IAntiforgery antiforgery, 
        ILogger<RxAntiforgeryCookieMiddleware> logger,
        IOptions<RxAntiforgeryOptions> options) {
        if (IsGetMethod(context.Request.Method)) {
            // Return an antiforgery token in the response for GET requests
            var tokenSet = antiforgery.GetAndStoreTokens(context);
            logger.LogTrace("Adding Antiforgery token cookie for {method}:{request}.",
                context.Request.Method,
                context.Request.GetDisplayUrl());
            if (string.IsNullOrEmpty(tokenSet.RequestToken)) {
                logger.LogWarning("Antiforgery token is null or empty for {method}:{request}", 
                    context.Request.Method, context.Request.GetDisplayUrl());
                await next(context).ConfigureAwait(false);
                return;
            }
            context.Response.Cookies.Append(options.Value.RequestVerificationTokenCookieName, tokenSet.RequestToken,
                new CookieOptions {
                    HttpOnly = false,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                });
            await next(context).ConfigureAwait(false);
            return;
        }
        // Validate antiforgery token for non-GET requests
        logger.LogTrace("Validating Antiforgery token for {method}:{request}.",
            context.Request.Method,
            context.Request.GetDisplayUrl());
        await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
        await next(context).ConfigureAwait(false);
    }
}