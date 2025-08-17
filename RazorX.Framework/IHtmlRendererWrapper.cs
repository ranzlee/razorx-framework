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
internal class HtmlRendererWrapper(HtmlRenderer htmlRenderer) : IHtmlRendererWrapper {
    private readonly HtmlRenderer _htmlRenderer = htmlRenderer;
    
    public object Dispatcher => _htmlRenderer.Dispatcher;

    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync(Type componentType, ParameterView parameters) {
        var result = await _htmlRenderer.RenderComponentAsync(componentType, parameters).ConfigureAwait(false);
        return new HtmlRootComponentWrapper(result);
    }
    
    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync<TComponent>(ParameterView parameters) where TComponent : IComponent {
        var result = await _htmlRenderer.RenderComponentAsync<TComponent>(parameters).ConfigureAwait(false);
        return new HtmlRootComponentWrapper(result);
    }
    
    public void Dispose() {
        _htmlRenderer.Dispose();
    }
    
    public ValueTask DisposeAsync() {
        return _htmlRenderer.DisposeAsync();
    }
}

// Wrapper for HtmlRootComponent
internal class HtmlRootComponentWrapper(object htmlRootComponent) : IHtmlRootComponentWrapper {
    private readonly object _htmlRootComponent = htmlRootComponent; // Use object since HtmlRootComponent is internal to ASP.NET Core
    private static readonly ConcurrentDictionary<Type, Func<object, string>?> _toHtmlStringFuncCache = new();
    private static readonly Lazy<ILoggerFactory> _loggerFactory = new(() => {
        return LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
    });
    
    private static readonly Lazy<ILogger> _logger = new(() => {
        return _loggerFactory.Value.CreateLogger<HtmlRootComponentWrapper>();
    });

    public string ToHtmlString() {
        try {
            // Use cached compiled expression for better performance
            var componentType = _htmlRootComponent.GetType();
            var func = _toHtmlStringFuncCache.GetOrAdd(componentType, CreateToHtmlStringFunc);
            return func?.Invoke(_htmlRootComponent) ?? "";
        }
        catch (Exception ex) {
            // Log reflection failure and return empty string
            _logger.Value.LogWarning(ex, "Failed to invoke ToHtmlString on {ComponentType}", _htmlRootComponent.GetType().Name);
            return "";
        }
    }
    
    private static Func<object, string>? CreateToHtmlStringFunc(Type componentType) {
        try {
            var method = componentType.GetMethod("ToHtmlString", Type.EmptyTypes);
            if (method == null) {
                _logger.Value.LogWarning("ToHtmlString method not found on {ComponentType}", componentType.Name);
                return null;
            }
            // Create compiled expression: obj => ((ComponentType)obj).ToHtmlString() ?? ""
            var parameter = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(parameter, componentType);
            var call = Expression.Call(cast, method);
            var nullCoalesce = Expression.Coalesce(call, Expression.Constant(""));
            var lambda = Expression.Lambda<Func<object, string>>(nullCoalesce, parameter);
            return lambda.Compile();
        }
        catch (Exception ex) {
            _logger.Value.LogWarning(ex, "Failed to create compiled expression for {ComponentType}", componentType.Name);
            return null;
        }
    }
}