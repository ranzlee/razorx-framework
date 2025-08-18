using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

public static class RouteMapper {
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
                    logger?.LogDebug("Successfully mapped routes for handler {HandlerType}", type.Name);
                    processedCount++;
                }
            }
            finally {
                // Dispose handler if it implements IDisposable to clean up any resources
                (handler as IDisposable)?.Dispose();
            }
        }
        logger?.LogInformation("Route mapping completed. Processed {HandlerCount} request handlers from assembly {AssemblyName}", processedCount, assembly.GetName().Name);
        return router;
    }
}


