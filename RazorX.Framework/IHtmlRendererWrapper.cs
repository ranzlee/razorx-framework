using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Wrapper interface for ASP.NET Core's HtmlRenderer to enable testing and abstraction.
/// </summary>
/// <remarks>
/// This interface wraps the sealed HtmlRenderer class from ASP.NET Core,
/// allowing it to be mocked in tests and providing an abstraction layer.
/// Automatically registered by AddRxDriver() when configuring services.
/// </remarks>
public interface IHtmlRendererWrapper : IAsyncDisposable, IDisposable {
    /// <summary>
    /// Gets the dispatcher object used for component rendering synchronization.
    /// </summary>
    object Dispatcher { get; }
    /// <summary>
    /// Renders a component of the specified type to HTML.
    /// </summary>
    /// <param name="componentType">The type of the component to render.</param>
    /// <param name="parameters">The parameters to pass to the component.</param>
    /// <returns>A wrapper containing the rendered HTML content.</returns>
    ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync(Type componentType, ParameterView parameters);
    /// <summary>
    /// Renders a component of the specified generic type to HTML.
    /// </summary>
    /// <typeparam name="TComponent">The type of the component to render.</typeparam>
    /// <param name="parameters">The parameters to pass to the component.</param>
    /// <returns>A wrapper containing the rendered HTML content.</returns>
    ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync<TComponent>(ParameterView parameters) where TComponent : IComponent;
}

/// <summary>
/// Wrapper interface for the rendered HTML content of a component.
/// </summary>
/// <remarks>
/// This interface provides access to the HTML string representation
/// of a rendered Blazor component.
/// </remarks>
public interface IHtmlRootComponentWrapper {
    /// <summary>
    /// Converts the rendered component to an HTML string.
    /// </summary>
    /// <returns>The HTML representation of the rendered component.</returns>
    string ToHtmlString();
}

// Concrete wrapper that delegates to real HtmlRenderer
internal class HtmlRendererWrapper(HtmlRenderer htmlRenderer, ILogger<HtmlRootComponentWrapper> logger) : IHtmlRendererWrapper {
    private readonly HtmlRenderer _htmlRenderer = htmlRenderer;
    private readonly ILogger<HtmlRootComponentWrapper> _logger = logger;
    
    public object Dispatcher => _htmlRenderer.Dispatcher;

    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync(Type componentType, ParameterView parameters) {
        var result = await _htmlRenderer.RenderComponentAsync(componentType, parameters).ConfigureAwait(false);
        return new HtmlRootComponentWrapper(result, _logger);
    }
    
    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync<TComponent>(ParameterView parameters) where TComponent : IComponent {
        var result = await _htmlRenderer.RenderComponentAsync<TComponent>(parameters).ConfigureAwait(false);
        return new HtmlRootComponentWrapper(result, _logger);
    }
    
    public void Dispose() {
        _htmlRenderer.Dispose();
    }
    
    public ValueTask DisposeAsync() {
        return _htmlRenderer.DisposeAsync();
    }
}

// Wrapper for HtmlRootComponent
internal class HtmlRootComponentWrapper(object htmlRootComponent, ILogger<HtmlRootComponentWrapper> logger) : IHtmlRootComponentWrapper {
    private readonly object _htmlRootComponent = htmlRootComponent; // Use object since HtmlRootComponent is internal to ASP.NET Core
    private readonly ILogger<HtmlRootComponentWrapper> _logger = logger;
    private static readonly ConcurrentDictionary<Type, Func<object, string>> _toHtmlStringFuncCache = new();

    public string ToHtmlString() {
        // Use cached compiled expression for better performance
        var componentType = _htmlRootComponent.GetType();
        var func = _toHtmlStringFuncCache.GetOrAdd(componentType, type => CreateToHtmlStringFunc(type, _logger));
        return func.Invoke(_htmlRootComponent);
    }
    
    private static Func<object, string> CreateToHtmlStringFunc(Type componentType, ILogger<HtmlRootComponentWrapper> logger) {
        var method = componentType.GetMethod("ToHtmlString", Type.EmptyTypes);
        if (method == null) {
            logger.LogError("ToHtmlString method not found on {ComponentType}", componentType.Name);
            throw new InvalidOperationException($"ToHtmlString method not found on {componentType.Name}");
        }
        // Create compiled expression: obj => ((ComponentType)obj).ToHtmlString()
        var parameter = Expression.Parameter(typeof(object), "obj");
        var cast = Expression.Convert(parameter, componentType);
        var call = Expression.Call(cast, method);
        var lambda = Expression.Lambda<Func<object, string>>(call, parameter);
        return lambda.Compile();
    }
}