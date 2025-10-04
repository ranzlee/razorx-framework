using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Simplified wrapper interface for ASP.NET Core's HtmlRenderer to enable testing and abstraction.
/// This version eliminates Expression.Compile() for AOT compatibility and simplicity.
/// </summary>
public interface IHtmlRendererWrapper : IAsyncDisposable, IDisposable {
    /// <summary>
    /// Gets the dispatcher object used for component rendering synchronization.
    /// </summary>
    object Dispatcher { get; }

    /// <summary>
    /// Renders a component of the specified type to HTML.
    /// </summary>
    ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync(Type componentType, ParameterView parameters);

    /// <summary>
    /// Renders a component of the specified generic type to HTML.
    /// </summary>
    ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync<TComponent>(ParameterView parameters) where TComponent : IComponent;
}

/// <summary>
/// Wrapper interface for the rendered HTML content of a component.
/// </summary>
public interface IHtmlRootComponentWrapper {
    /// <summary>
    /// Converts the rendered component to an HTML string.
    /// </summary>
    string ToHtmlString();
}

/// <summary>
/// Simplified concrete wrapper that delegates to real HtmlRenderer.
/// Uses direct reflection instead of compiled expressions for AOT compatibility.
/// </summary>
internal class HtmlRendererWrapper(HtmlRenderer htmlRenderer, ILogger<HtmlRootComponentWrapper> logger) : IHtmlRendererWrapper {
    private readonly HtmlRenderer _htmlRenderer = htmlRenderer;
    private readonly ILogger<HtmlRootComponentWrapper> _logger = logger;
    public object Dispatcher => _htmlRenderer.Dispatcher;

    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync(Type componentType, ParameterView parameters) {
        var result = await InvokeOnDispatcherAsync(async () => {
            return await _htmlRenderer.RenderComponentAsync(componentType, parameters).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return new HtmlRootComponentWrapper(result, _logger);
    }

    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync<TComponent>(ParameterView parameters) where TComponent : IComponent {
        var result = await InvokeOnDispatcherAsync(async () => {
            return await _htmlRenderer.RenderComponentAsync<TComponent>(parameters).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return new HtmlRootComponentWrapper(result, _logger);
    }

    private async Task<T> InvokeOnDispatcherAsync<T>(Func<Task<T>> workItem) {
        var dispatcher = _htmlRenderer.Dispatcher;
        var dispatcherType = dispatcher.GetType();
        var invokeAsyncMethod = dispatcherType.GetMethod("InvokeAsync", [typeof(Func<Task>)]);

        if (invokeAsyncMethod == null) {
            // Fallback: execute directly if dispatcher doesn't have InvokeAsync
            _logger.LogWarning("InvokeAsync method not found on dispatcher {Type}, executing directly", dispatcherType.Name);
            return await workItem().ConfigureAwait(false);
        }
        var tcs = new TaskCompletionSource<T>();
        Func<Task> wrappedWorkItem = async () => {
            try {
                var result = await workItem().ConfigureAwait(false);
                tcs.SetResult(result);
            } catch (Exception ex) {
                tcs.SetException(ex);
            }
        };
        try {
            var task = (Task)invokeAsyncMethod.Invoke(dispatcher, [wrappedWorkItem])!;
            await task.ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to invoke work on dispatcher");
            throw;
        }
    }

    public void Dispose() {
        _htmlRenderer.Dispose();
    }

    public ValueTask DisposeAsync() {
        return _htmlRenderer.DisposeAsync();
    }
}

internal class HtmlRootComponentWrapper(object htmlRootComponent, ILogger<HtmlRootComponentWrapper> logger) : IHtmlRootComponentWrapper {
    private readonly object _htmlRootComponent = htmlRootComponent;
    public string ToHtmlString() {
        var componentType = _htmlRootComponent.GetType();
        var method = componentType.GetMethod("ToHtmlString", Type.EmptyTypes);
        if (method == null) {
            logger.LogError("ToHtmlString method not found on {ComponentType}", componentType.Name);
            throw new InvalidOperationException($"ToHtmlString method not found on {componentType.Name}");
        }
        try {
            return (string)method.Invoke(_htmlRootComponent, null)!;
        }
        catch (System.Reflection.TargetInvocationException tie) {
            logger.LogError(tie.InnerException, "Failed to invoke ToHtmlString on {ComponentType}", componentType.Name);
            throw tie.InnerException!;
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to invoke ToHtmlString on {ComponentType}", componentType.Name);
            throw;
        }
    }
}