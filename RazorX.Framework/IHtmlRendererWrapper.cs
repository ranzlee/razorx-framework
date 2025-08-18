using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

// Interface for wrapping HtmlRenderer since it's sealed and cannot be mocked directly
public interface IHtmlRendererWrapper : IAsyncDisposable, IDisposable {
    object Dispatcher { get; }
    ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync(Type componentType, ParameterView parameters);
    ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync<TComponent>(ParameterView parameters) where TComponent : IComponent;
}

// Interface for wrapping HtmlRootComponent
public interface IHtmlRootComponentWrapper {
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
    private static readonly ConcurrentDictionary<Type, Func<object, string>?> _toHtmlStringFuncCache = new();

    public string ToHtmlString() {
        // Use cached compiled expression for better performance
        var componentType = _htmlRootComponent.GetType();
        var func = _toHtmlStringFuncCache.GetOrAdd(componentType, type => CreateToHtmlStringFunc(type, _logger))
            ?? throw new InvalidOperationException($"Failed to create ToHtmlString delegate for {componentType.Name}");
        
        return func.Invoke(_htmlRootComponent);
    }
    
    private static Func<object, string>? CreateToHtmlStringFunc(Type componentType, ILogger<HtmlRootComponentWrapper> logger) {
        try {
            var method = componentType.GetMethod("ToHtmlString", Type.EmptyTypes);
            if (method == null) {
                logger.LogError("ToHtmlString method not found on {ComponentType}", componentType.Name);
                return null;
            }
            // Create compiled expression: obj => ((ComponentType)obj).ToHtmlString()
            var parameter = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(parameter, componentType);
            var call = Expression.Call(cast, method);
            var lambda = Expression.Lambda<Func<object, string>>(call, parameter);
            return lambda.Compile();
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to create compiled expression for {ComponentType}", componentType.Name);
            return null;
        }
    }
}