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

/// <summary>
/// Extension methods for configuring RazorX antiforgery protection.
/// </summary>
/// <remarks>
/// Provides CSRF protection for RazorX applications by automatically managing
/// antiforgery tokens through cookies for GET requests and validating them for
/// state-changing operations.
/// </remarks>
public static class RxAntiforgeryExtensions {

    /// <summary>
    /// Adds RazorX antiforgery configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureOptions">Optional delegate to configure antiforgery options.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// This method configures how antiforgery tokens are managed in cookies.
    /// Must be called after AddAntiforgery() in the service configuration.
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddAntiforgery();
    /// builder.Services.AddRxAntiforgery(options => {
    ///     options.RequestVerificationTokenCookieName = "MyToken";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRxAntiforgery(this IServiceCollection services, Action<RxAntiforgeryOptions>? configureOptions = null) {
        services.Configure<RxAntiforgeryOptions>(options => {
            configureOptions?.Invoke(options);
        });
        return services;
    }

    /// <summary>
    /// Adds the RazorX antiforgery cookie middleware to the request pipeline.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <remarks>
    /// This middleware:
    /// - Automatically adds antiforgery tokens to cookies for GET requests
    /// - Validates antiforgery tokens for non-GET requests (POST, PUT, DELETE, etc.)
    /// - Sets cookies with HttpOnly=false to allow JavaScript access
    /// 
    /// Must be called after UseAntiforgery() in the pipeline configuration.
    /// </remarks>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseAntiforgery();
    /// app.UseRxAntiforgeryCookie();
    /// </code>
    /// </example>
    public static void UseRxAntiforgeryCookie(this WebApplication app) {
        app.UseMiddleware<RxAntiforgeryCookieMiddleware>();
    }
}

/// <summary>
/// Middleware that manages antiforgery tokens for RazorX AJAX requests.
/// </summary>
/// <remarks>
/// This middleware automatically handles CSRF protection by:
/// - Adding antiforgery tokens to cookies for GET requests
/// - Validating tokens for state-changing operations (POST, PUT, DELETE, etc.)
/// - Ensuring tokens are accessible to JavaScript for AJAX requests
/// 
/// This class is used internally by UseRxAntiforgeryCookie and should not be instantiated directly.
/// </remarks>
public sealed class RxAntiforgeryCookieMiddleware(RequestDelegate next) {
    
    private static bool IsGetMethod(string method) {
        // Use ReadOnlySpan to avoid string allocations when trimming
        ReadOnlySpan<char> methodSpan = method.AsSpan().Trim();
        return methodSpan.Equals("GET".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>
    /// Processes the HTTP request for antiforgery token management.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="antiforgery">The antiforgery service.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="options">Configuration options for the middleware.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(
        HttpContext context, 
        IAntiforgery antiforgery, 
        ILogger<RxAntiforgeryCookieMiddleware> logger,
        IOptions<RxAntiforgeryOptions> options) {
        if (IsGetMethod(context.Request.Method)) {
            // Return an antiforgery token in the response for GET requests
            var tokenSet = antiforgery.GetAndStoreTokens(context);
            if (logger.IsEnabled(LogLevel.Trace)) {
                logger.LogTrace("Adding Antiforgery token cookie for {method}:{request}.",
                    context.Request.Method,
                    context.Request.GetDisplayUrl());
            }
            if (string.IsNullOrEmpty(tokenSet.RequestToken)) {
                if (logger.IsEnabled(LogLevel.Warning)) {
                    logger.LogWarning("Antiforgery token is null or empty for {method}:{request}",
                        context.Request.Method, context.Request.GetDisplayUrl());
                }
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
        if (logger.IsEnabled(LogLevel.Trace)) {
            logger.LogTrace("Validating Antiforgery token for {method}:{request}.",
                context.Request.Method,
                context.Request.GetDisplayUrl());
        }
        try {
            await antiforgery.ValidateRequestAsync(context).ConfigureAwait(false);
            RxTelemetry.AntiforgeryValidationCounter.Add(1,
                new KeyValuePair<string, object?>("result", "success"));
        } catch {
            RxTelemetry.AntiforgeryValidationCounter.Add(1,
                new KeyValuePair<string, object?>("result", "failure"));
            throw;
        }
        await next(context).ConfigureAwait(false);
    }
}