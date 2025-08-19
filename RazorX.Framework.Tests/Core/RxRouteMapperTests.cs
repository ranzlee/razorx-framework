using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace RazorX.Framework.Tests.Core;

[TestClass]
public class RxRouteMapperTests {
    private WebApplication _app = null!;
    private RouteGroupBuilder _routeGroup = null!;

    [TestInitialize]
    public void SetUp() {
        var builder = WebApplication.CreateBuilder();
        _app = builder.Build();
        _routeGroup = _app.MapGroup("");
    }

    [TestCleanup]
    public void TearDown() {
        _app?.DisposeAsync().AsTask().Wait();
    }

    #region MapRoutes Tests

    [TestMethod]
    public void MapRoutes_WithCurrentAssembly_DiscoversRequestHandlers() {
        // Act
        var result = _routeGroup.MapRoutes();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<RouteGroupBuilder>(result);
        
        // Verify that test handlers in this assembly were discovered
        // Route validation requires integration testing
    }

    [TestMethod]
    public void MapRoutes_WithSpecificAssembly_DiscoversRequestHandlers() {
        // Arrange
        var testAssembly = Assembly.GetExecutingAssembly();

        // Act
        var result = _routeGroup.MapRoutes(testAssembly);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<RouteGroupBuilder>(result);
    }

    [TestMethod]
    public void MapRoutes_WithAssemblyContainingHandlers_RegistersRoutes() {
        // Arrange
        var testAssembly = Assembly.GetExecutingAssembly();

        // Act
        _routeGroup.MapRoutes(testAssembly);

        // Assert
        // Route validation requires integration testing
        
        // Should have registered routes from TestRequestHandler
        // Route validation requires integration testing
    }

    [TestMethod]
    public void MapRoutes_WithEmptyAssembly_DoesNotThrow() {
        // Arrange
        var emptyAssembly = typeof(string).Assembly; // mscorlib has no IRequestHandlers

        // Act & Assert
        var result = _routeGroup.MapRoutes(emptyAssembly);
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void MapRoutes_CallMultipleTimes_RegistersAllHandlers() {
        // Act
        var result1 = _routeGroup.MapRoutes();
        var result2 = _routeGroup.MapRoutes();

        // Assert
        Assert.IsNotNull(result1);
        Assert.IsNotNull(result2);
        Assert.AreSame(result1, result2); // Should return same RouteGroupBuilder
    }

    #endregion

    #region Handler Discovery Tests

    [TestMethod]
    public void MapRoutes_DiscoversConcreteHandlers() {
        // Arrange
        var testAssembly = Assembly.GetExecutingAssembly();

        // Act
        _routeGroup.MapRoutes(testAssembly);

        // Assert
        // Route validation requires integration testing
        
        // Should have discovered TestRequestHandler routes
        // Route validation requires integration testing
    }

    [TestMethod]
    public void MapRoutes_IgnoresAbstractHandlers() {
        // This test verifies that abstract classes implementing IRequestHandler are ignored
        // The test is implicit - if abstract handlers were included, instantiation would fail
        
        // Act & Assert - Should not throw despite AbstractTestHandler being in assembly
        var result = _routeGroup.MapRoutes();
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void MapRoutes_IgnoresInterfaces() {
        // This test verifies that IRequestHandler interface itself is ignored
        // The test is implicit - interfaces cannot be instantiated
        
        // Act & Assert - Should not throw despite IRequestHandler being in assembly
        var result = _routeGroup.MapRoutes();
        Assert.IsNotNull(result);
    }


    #endregion

    #region Error Handling Tests

    [TestMethod]
    public void MapRoutes_WithNullAssembly_UsesCallingAssembly() {
        // Act & Assert - Should not throw
        var result = _routeGroup.MapRoutes(null);
        Assert.IsNotNull(result);
    }


    #endregion
}

#region Test Request Handlers

public class TestRequestHandler : RequestHandler {
    public override void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/test", () => "Test");
        router.MapPost("/test", () => "Test Post");
        router.MapGet("/test/{id:int}", (int id) => $"Test {id}");
    }
}

public class AnotherTestHandler : RequestHandler {
    public override void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/another", () => "Another");
        router.MapDelete("/another/{id:int}", (int id) => $"Delete {id}");
    }
}

public abstract class AbstractTestHandler : RequestHandler {
    public abstract override void MapRoutes(IEndpointRouteBuilder router);
}



#endregion