using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Provides extension methods for automatically discovering and registering RequestHandler routes.
/// </summary>
/// <remarks>
/// The RouteMapper scans assemblies for RequestHandler implementations and automatically
/// registers their routes with the application. This enables a convention-based approach
/// to route registration.
/// </remarks>
public static class RouteMapper {
    /// <summary>
    /// Discovers and maps all RequestHandler routes from the specified or calling assembly.
    /// </summary>
    /// <param name="router">The route group builder to add routes to.</param>
    /// <param name="routeHandlerAssembly">Optional assembly to scan for handlers. If null, uses the calling assembly.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <returns>The route group builder for method chaining.</returns>
    /// <remarks>
    /// This method performs the following:
    /// 1. Scans the assembly for non-abstract classes inheriting from RequestHandler
    /// 2. Creates an instance of each handler
    /// 3. Calls MapRoutes on each handler to register its routes
    /// 4. Properly disposes each handler after registration
    /// 
    /// Handler instances are only used for route registration and are disposed immediately after.
    /// The actual request handling uses dependency injection at runtime.
    /// </remarks>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.MapGroup(string.Empty).MapRoutes();
    /// // Or specify a different assembly
    /// app.MapGroup("/api").MapRoutes(typeof(MyHandler).Assembly);
    /// </code>
    /// </example>
    public static RouteGroupBuilder MapRoutes(this RouteGroupBuilder router, Assembly? routeHandlerAssembly = null, ILogger? logger = null) {
        var assembly = routeHandlerAssembly ?? Assembly.GetCallingAssembly();
        var handlerTypes = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(RequestHandler)))
            .ToArray();
        var processedCount = 0;
        // Process each handler type and ensure proper disposal
        foreach (var type in handlerTypes) {
            RequestHandler? handler = null;
            try {
                handler = Activator.CreateInstance(type) as RequestHandler;
                if (handler != null) {
                    handler.MapRoutes(router);
                    if (logger?.IsEnabled(LogLevel.Debug) == true) {
                        logger.LogDebug("Successfully mapped routes for handler {HandlerType}", type.Name);
                    }
                    processedCount++;
                }
            }
            finally {
                // Dispose handler if it implements IDisposable to clean up any resources
                (handler as IDisposable)?.Dispose();
            }
        }
        if (logger?.IsEnabled(LogLevel.Information) == true) {
            logger.LogInformation("Route mapping completed. Processed {HandlerCount} request handlers from assembly {AssemblyName}", processedCount, assembly.GetName().Name);
        }
        return router;
    }
}


