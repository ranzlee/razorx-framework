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
        // Inspect for IRequestHandlers 
        var assembly = routeHandlerAssembly ?? Assembly.GetCallingAssembly();
        var routeGroups = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IRequestHandler)))
            .Select(type => {
                try {
                    return Activator.CreateInstance(type) as IRequestHandler;
                }
                catch (Exception ex) {
                    // Skip handlers that can't be instantiated (e.g., no parameterless constructor)
                    logger?.LogWarning(ex, "Failed to instantiate request handler {HandlerType}. Ensure it has a parameterless constructor.", type.Name);
                    return null;
                }
            })
            .Where(handler => handler != null)
            .ToArray();
        // Map routes for IRouteGroups found
        foreach (var routeGroup in routeGroups) {
            try {
                routeGroup?.MapRoutes(router);
                logger?.LogDebug("Successfully mapped routes for handler {HandlerType}", routeGroup?.GetType().Name);
            }
            catch (Exception ex) {
                // Continue with other handlers even if one fails
                // This allows for graceful degradation
                logger?.LogWarning(ex, "Failed to map routes for handler {HandlerType}. This handler will be skipped.", routeGroup?.GetType().Name);
            }
        }
        
        logger?.LogInformation("Route mapping completed. Processed {HandlerCount} request handlers from assembly {AssemblyName}", 
            routeGroups.Length, assembly.GetName().Name);
        return router;
    }
}


