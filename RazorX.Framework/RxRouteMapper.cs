using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace RazorX.Framework;

/// <summary>
/// Interface for a class that contains endpoints.
/// </summary>
public interface IRequestHandler {
    void MapRoutes(IEndpointRouteBuilder router);
}

public static class RouteMapper {
    public static RouteGroupBuilder MapRoutes(this RouteGroupBuilder router, Assembly? routeHandlerAssembly = null) {
        // Inspect for IRequestHandlers 
        var assembly = routeHandlerAssembly ?? Assembly.GetCallingAssembly();
        var routeGroups = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IRequestHandler)))
            .Select(type => Activator.CreateInstance(type) as IRequestHandler)
            .ToArray();
        // Map routes for IRouteGroups found
        foreach (var routeGroup in routeGroups) {
            routeGroup?.MapRoutes(router);
        }
        return router;
    }
}


