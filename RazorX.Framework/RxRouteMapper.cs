using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Interface for a class that contains endpoints.
/// </summary>
public interface IRequestHandler {
    void MapRoutes(IEndpointRouteBuilder router);
}

public static class RouteMapper {
    public static RouteGroupBuilder MapRoutes(this RouteGroupBuilder router, Assembly? routeHandlerAssembly = null, ILogger? logger = null) {
        var assembly = routeHandlerAssembly ?? Assembly.GetCallingAssembly();
        var handlerTypes = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IRequestHandler)))
            .ToArray();
        var processedCount = 0;
        // Process each handler type and ensure proper disposal
        foreach (var type in handlerTypes) {
            IRequestHandler? handler = null;
            try {
                handler = Activator.CreateInstance(type) as IRequestHandler;
                if (handler != null) {
                    handler.MapRoutes(router);
                    logger?.LogDebug("Successfully mapped routes for handler {HandlerType}", type.Name);
                    processedCount++;
                }
            }
            finally {
                (handler as IDisposable)?.Dispose();
            }
        }
        logger?.LogInformation("Route mapping completed. Processed {HandlerCount} request handlers from assembly {AssemblyName}", processedCount, assembly.GetName().Name);
        return router;
    }
}


