using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

public record CloseDialogTrigger(string DialogId, string? OnCloseData, string? ResetFormId);
public record FocusElementTrigger(string ElementId, bool PositionCursorEnd);
public record SetStateTrigger(string Key, string Value, string Scope, bool UpdateUrl = false);
public record ToastTrigger(string Message, string Type, int Duration, string VerticalPosition, string HorizontalPosition, bool ClickToDismiss);

public class RxDriverOptions {
    /// <summary>
    /// When true, registers JSON converters for form data encoding.
    /// Default is true to match razorx client default behavior.
    /// Set to false if you're not using JSON-encoded form data.
    /// </summary>
    public bool AddJsonConverters { get; set; } = true;
}

public static class RxDriverServices {
    public static void AddRxDriver(this IServiceCollection services, Action<RxDriverOptions>? configureOptions = null) {
        var options = new RxDriverOptions();
        configureOptions?.Invoke(options);
        services.TryAddScoped<HtmlRenderer>();
        services.TryAddScoped<IHtmlRendererWrapper>(factory => {
            return new HtmlRendererWrapper(
                factory.GetRequiredService<HtmlRenderer>(),
                factory.GetRequiredService<ILogger<HtmlRootComponentWrapper>>());
        });
        services.TryAddScoped<IRxDriver, RxDriver>();
        if (options.AddJsonConverters) {
            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.ConfigureOptions<RxJsonOptions>();
        }
    }

    public static bool IsRxRequest(this HttpRequest request) {
        return request.Headers.ContainsKey("rx-request");
    }
}

public interface IRxDriver : IAsyncDisposable, IDisposable {
    IRxResponseBuilder With(HttpContext context);
    
    Task<IResult> RenderPage<TRoot, TComponent, TModel>(
        HttpContext context, 
        TModel model, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where TComponent : IComponent, IComponentModel<TModel>;
    
    Task<IResult> RenderPage<TRoot, TComponent>(
        HttpContext context, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where TComponent : IComponent;
    
    Task<IResult> RenderPage<TRoot, THead, TComponent, TModel>(
        HttpContext context,
        TModel model, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where THead : IComponent
      where TComponent : IComponent, IComponentModel<TModel>;
    
    Task<IResult> RenderPage<TRoot, THead, TComponent>(
        HttpContext context,
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where THead : IComponent
      where TComponent : IComponent;
}

public interface IRxResponseBuilder {
    IRxResponseBuilder AddFragment<TComponent, TModel>(
        TModel model,
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent, IComponentModel<TModel>;

    IRxResponseBuilder AddFragment<TComponent>(
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent;

    IRxResponseBuilder RemoveElement(string targetId);

    IRxResponseBuilder AddTriggerCloseDialog(string dialogId, string? onCloseData = null, string? resetFormId = null);

    IRxResponseBuilder AddTriggerFocusElement(string elementId, bool positionCursorEnd = false);

    IRxResponseBuilder AddTriggerSetState(string key, string value, MetadataScope scope = MetadataScope.Session, bool updateUrl = false);

    IRxResponseBuilder AddTriggerSetStateBatch(Dictionary<string, string> state, MetadataScope scope, bool updateUrl = false);

    IRxResponseBuilder AddTriggerToast(
        string message,
        ToastType type = ToastType.Success,
        int duration = 3500,
        ToastVerticalPosition verticalPosition = ToastVerticalPosition.Top,
        ToastHorizontalPosition horizontalPosition = ToastHorizontalPosition.Right,
        bool clickToDismiss = true
    );

    Task<IResult> Render(
        bool ignoreActiveElementValueOnMorph = false,
        CancellationToken cancellationToken = default
    );
}

internal sealed class RxDriver(IHtmlRendererWrapper htmlRenderer, ILogger<RxDriver> logger) : IRxDriver {
    private bool disposed = false;
    
    public IRxResponseBuilder With(HttpContext context) {
        return disposed
            ? throw new ObjectDisposedException(nameof(RxDriver)) 
            : (IRxResponseBuilder)new RxResponseBuilder(context, htmlRenderer, logger);
    }
    
    public async Task<IResult> RenderPage<TRoot, TComponent, TModel>(
        HttpContext context, 
        TModel model, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where TComponent : IComponent, IComponentModel<TModel> {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxDriver));
        cancellationToken.ThrowIfCancellationRequested();
        
        var pageComponentParameters = new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        };
        
        var rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.MainContentParameters), pageComponentParameters },
            { nameof(IRootComponent.Title), title },
        });
        
        return await RenderPageInternal(typeof(TRoot), rootParameters, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<IResult> RenderPage<TRoot, TComponent>(
        HttpContext context, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where TComponent : IComponent {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxDriver));
        cancellationToken.ThrowIfCancellationRequested();
        
        var rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.Title), title },
        });
        
        return await RenderPageInternal(typeof(TRoot), rootParameters, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<IResult> RenderPage<TRoot, THead, TComponent, TModel>(
        HttpContext context,
        TModel model, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where THead : IComponent
      where TComponent : IComponent, IComponentModel<TModel> {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxDriver));
        cancellationToken.ThrowIfCancellationRequested();
        
        var pageComponentParameters = new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        };
        
        var rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.HeadContent), typeof(THead) },
            { nameof(IRootComponent.MainContentParameters), pageComponentParameters },
            { nameof(IRootComponent.Title), title },
        });
        
        return await RenderPageInternal(typeof(TRoot), rootParameters, cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<IResult> RenderPage<TRoot, THead, TComponent>(
        HttpContext context,
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where THead : IComponent
      where TComponent : IComponent {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxDriver));
        cancellationToken.ThrowIfCancellationRequested();
        
        var rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.HeadContent), typeof(THead) },
            { nameof(IRootComponent.Title), title },
        });
        
        return await RenderPageInternal(typeof(TRoot), rootParameters, cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<IResult> RenderPageInternal(
        Type rootComponentType,
        ParameterView rootParameters,
        CancellationToken cancellationToken
    ) {
        cancellationToken.ThrowIfCancellationRequested();
        
        string output = string.Empty;

        await InvokeOnDispatcher(htmlRenderer.Dispatcher, async () => {
            cancellationToken.ThrowIfCancellationRequested();
            var root = await htmlRenderer.RenderComponentAsync(rootComponentType, rootParameters).ConfigureAwait(false);
            output = root.ToHtmlString();
        }, logger).ConfigureAwait(false);
        
        return Results.Content(output, "text/html");
    }

    public async ValueTask DisposeAsync() {
        if (disposed) {
            return;
        }
        try {
            logger.LogDebug("Async Disposing RxDriver");
            await htmlRenderer.DisposeAsync().ConfigureAwait(false);
            disposed = true;
            logger.LogDebug("RxDriver Async Disposed successfully");
        }
        catch (Exception ex) {
            logger.LogError(ex, "Error during async disposal of RxDriver");
            throw;
        }
        finally {
            GC.SuppressFinalize(this);
        }
    }

    private static readonly ConcurrentDictionary<Type, Func<object, Func<Task>, Task>?> _invokeAsyncFuncCache = new();
    
    internal static Task InvokeOnDispatcher(object dispatcher, Func<Task> workItem, ILogger logger) {
        try {
            // Use cached compiled expression for better performance
            var dispatcherType = dispatcher.GetType();
            var func = _invokeAsyncFuncCache.GetOrAdd(dispatcherType, type => CreateInvokeAsyncFunc(type, logger));
            return func?.Invoke(dispatcher, workItem) ?? Task.CompletedTask;
        }
        catch (Exception ex) {
            // Log reflection failure
            logger.LogError(ex, "Failed to invoke InvokeAsync on dispatcher {DispatcherType}", dispatcher.GetType().Name);
            throw;
        }
    }
    
    private static Func<object, Func<Task>, Task>? CreateInvokeAsyncFunc(Type dispatcherType, ILogger logger) {
        try {
            var method = dispatcherType.GetMethod("InvokeAsync", [typeof(Func<Task>)]);
            if (method == null) {
                logger.LogWarning("InvokeAsync method not found on dispatcher {DispatcherType}", dispatcherType.Name);
                return null;
            }
            // Create compiled expression based on whether method is static or instance
            var dispatcherParam = Expression.Parameter(typeof(object), "dispatcher");
            var workItemParam = Expression.Parameter(typeof(Func<Task>), "workItem");
            Expression call;
            if (method.IsStatic) {
                // Static method: DispatcherType.InvokeAsync(workItem)
                call = Expression.Call(method, workItemParam);
            } else {
                // Instance method: ((DispatcherType)dispatcher).InvokeAsync(workItem)
                var cast = Expression.Convert(dispatcherParam, dispatcherType);
                call = Expression.Call(cast, method, workItemParam);
            }
            var lambda = Expression.Lambda<Func<object, Func<Task>, Task>>(call, dispatcherParam, workItemParam);
            return lambda.Compile();
        }
        catch (Exception ex) {
            logger.LogWarning(ex, "Failed to create compiled expression for dispatcher {DispatcherType}", dispatcherType.Name);
            return null;
        }
    }
    
    public void Dispose() {
        if (disposed) {
            return;
        }
        try {
            logger.LogDebug("Disposing RxDriver");
            htmlRenderer.Dispose();
            disposed = true;
            logger.LogDebug("RxDriver Disposed successfully");
        }
        catch (Exception ex) {
            logger.LogError(ex, "Error during disposal of RxDriver");
            throw;
        }
        finally {
            GC.SuppressFinalize(this);
        }
    }
}

internal record MergeStrategy(string Target, string Strategy);

internal sealed class RxResponseBuilder(HttpContext context, IHtmlRendererWrapper htmlRenderer, ILogger logger) : IRxResponseBuilder, IDisposable {
    private bool isRendering = false;
    private bool disposed = false;
    private readonly StringBuilder content = new(capacity: 4096);
    private readonly Lock contentLock = new();
    private readonly List<Task> renderTasks = [];
    private readonly List<MergeStrategy> mergeStrategies = [];
    private CloseDialogTrigger? closeDialogTrigger = null;
    private FocusElementTrigger? focusElementTrigger = null;
    private readonly List<SetStateTrigger> setStateTriggers = [];
    private ToastTrigger? toastTrigger = null;
    private readonly HashSet<string> stateKeysInResponse = [];
    private static readonly JsonSerializerOptions serializerSettings = new(JsonSerializerDefaults.Web);
    
    // Cache template format to avoid repeated string operations
    private static string CreateTemplate(string targetId, string htmlContent) => 
        $"<template id=\"{targetId}-rx-fragment\">{htmlContent}</template>";
    
    // Validate targetId parameter
    private static void ValidateTargetId(string targetId) {
        if (string.IsNullOrWhiteSpace(targetId)) {
            throw new ArgumentException("Target ID cannot be null or empty", nameof(targetId));
        }
        // Basic validation - no special characters that could break HTML
        if (targetId.IndexOfAny(['<', '>', '"', '\'', '&']) >= 0) {
            throw new ArgumentException("Target ID contains invalid HTML characters", nameof(targetId));
        }
    }

    public IRxResponseBuilder AddFragment<TComponent, TModel>(
        TModel model,
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent, IComponentModel<TModel> {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        ValidateTargetId(targetId);
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        });
        renderTasks.Add(RxDriver.InvokeOnDispatcher(htmlRenderer.Dispatcher, async () => {
            var output = await htmlRenderer.RenderComponentAsync<TComponent>(parameters).ConfigureAwait(false);
            var template = CreateTemplate(targetId, output.ToHtmlString());
            lock (contentLock) {
                content.Append(template);
            }
        }, logger));
        AddMergeStrategy(targetId, fragmentMergeStrategy);
        return this;
    }

    public IRxResponseBuilder AddFragment<TComponent>(
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        ValidateTargetId(targetId);
        renderTasks.Add(RxDriver.InvokeOnDispatcher(htmlRenderer.Dispatcher, async () => {
            var output = await htmlRenderer.RenderComponentAsync<TComponent>(ParameterView.Empty).ConfigureAwait(false);
            var template = CreateTemplate(targetId, output.ToHtmlString());
            lock (contentLock) {
                content.Append(template);
            }
        }, logger));
        AddMergeStrategy(targetId, fragmentMergeStrategy);
        return this;
    }

    public IRxResponseBuilder RemoveElement(string targetId) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        ValidateTargetId(targetId);
        mergeStrategies.Add(new(targetId, "remove"));
        return this;
    }

    public IRxResponseBuilder AddTriggerCloseDialog(string dialogId, string? onCloseData = null, string? resetFormId = null) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        closeDialogTrigger = new(dialogId, onCloseData, resetFormId);
        return this;
    }

    public IRxResponseBuilder AddTriggerFocusElement(string elementId, bool positionCursorEnd = false) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        focusElementTrigger = new(elementId, positionCursorEnd); 
        return this;
    }

    public IRxResponseBuilder AddTriggerSetState(string key, string value, MetadataScope scope = MetadataScope.Session, bool updateUrl = false) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        ValidateStateKey(key);
        if (!stateKeysInResponse.Add(key)) {
            throw new InvalidOperationException($"State key '{key}' has already been set in this response. Multiple state triggers with the same key are not allowed.");
        }
        setStateTriggers.Add(new SetStateTrigger(key, value, scope.ToString(), updateUrl));
        return this;
    }

    public IRxResponseBuilder AddTriggerSetStateBatch(Dictionary<string, string> state, MetadataScope scope, bool updateUrl = false) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        foreach (var (key, value) in state) {
            ValidateStateKey(key);
            if (!stateKeysInResponse.Add(key)) {
                throw new InvalidOperationException($"State key '{key}' has already been set in this response. Multiple state triggers with the same key are not allowed.");
            }
            setStateTriggers.Add(new SetStateTrigger(key, value, scope.ToString(), updateUrl));
        }
        return this;
    }

    public IRxResponseBuilder AddTriggerToast(
        string message,
        ToastType type = ToastType.Info,
        int duration = 5000,
        ToastVerticalPosition verticalPosition = ToastVerticalPosition.Top,
        ToastHorizontalPosition horizontalPosition = ToastHorizontalPosition.Right,
        bool clickToDismiss = true
    ) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        CheckRenderingStatus();
        
        if (string.IsNullOrWhiteSpace(message)) {
            throw new ArgumentException("Toast message cannot be null or empty", nameof(message));
        }
        if (duration < 0) {
            throw new ArgumentException("Duration cannot be negative", nameof(duration));
        }
        
        toastTrigger = new ToastTrigger(message, type.ToString(), duration, verticalPosition.ToString(), horizontalPosition.ToString(), clickToDismiss);
        return this;
    }

    public async Task<IResult> Render(
        bool ignoreActiveElementValueOnMorph = false,
        CancellationToken cancellationToken = default
    ) {
        ObjectDisposedException.ThrowIf(disposed, nameof(RxResponseBuilder));
        cancellationToken.ThrowIfCancellationRequested();
        CheckRenderingStatus();
        
        // Fragments and triggers require rx-request header
        if (!context.Request.IsRxRequest()) {
            throw new InvalidOperationException("Fragment and trigger operations require rx-request header. Use RenderPage methods for full page rendering.");
        }
        
        // Set rendering flag early to prevent duplicate renders
        isRendering = true;
        
        //triggers
        if (closeDialogTrigger != null) {
            context.Response.Headers.Append("rx-trigger-close-dialog", JsonSerializer.Serialize(closeDialogTrigger, serializerSettings));
        }
        if (focusElementTrigger != null) {
            context.Response.Headers.Append("rx-trigger-focus-element", JsonSerializer.Serialize(focusElementTrigger, serializerSettings));
        }
        if (setStateTriggers.Count > 0) {
            foreach (var t in setStateTriggers) {
                context.Response.Headers.Append("rx-trigger-set-state", JsonSerializer.Serialize(t, serializerSettings));
            }
        }
        if (toastTrigger != null) {
            context.Response.Headers.Append("rx-trigger-toast", JsonSerializer.Serialize(toastTrigger, serializerSettings));
        }

        //fragments
        if (ignoreActiveElementValueOnMorph) {
            context.Response.Headers.Append("rx-morph-ignore-active", true.ToString());
        }
        context.Response.Headers.Append("rx-merge", JsonSerializer.Serialize(mergeStrategies, serializerSettings));
        if (renderTasks.Count != 0) {
            logger.LogDebug("Rendering Fragments");
            await Task.WhenAll(renderTasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        string htmlContent;
        lock (contentLock) {
            if (content.Length == 0) {
                return TypedResults.NoContent();
            }
            htmlContent = content.ToString();
        }
        return Results.Content(htmlContent, contentType: "text/html");
    }

    private void AddMergeStrategy(string targetId, FragmentMergeStrategyType fragmentMergeStrategy) {
        var mergeStrategy = fragmentMergeStrategy switch {
            FragmentMergeStrategyType.Swap => "swap",
            FragmentMergeStrategyType.SwapInner => "swapInner",
            FragmentMergeStrategyType.AppendAfterBegin => "afterbegin",
            FragmentMergeStrategyType.AppendAfterEnd => "afterend",
            FragmentMergeStrategyType.AppendBeforeBegin => "beforebegin",
            FragmentMergeStrategyType.AppendBeforeEnd => "beforeend",
            _ => "morph"
        };
        mergeStrategies.Add(new(targetId, mergeStrategy));
    }

    private void CheckRenderingStatus() {
        if (isRendering) {
            throw new InvalidOperationException("Render has already been called and may only be called once per request.");
        }
    }

    private static void ValidateStateKey(string key) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("State key cannot be null or empty", nameof(key));
        }
        if (!IsValidStateKey(key)) {
            throw new ArgumentException($"State key '{key}' contains invalid characters. Only alphanumeric characters, hyphens, and underscores are allowed.", nameof(key));
        }
    }

    private static bool IsValidStateKey(string key) {
        if (string.IsNullOrWhiteSpace(key)) {
            return false;
        }
        return key.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }
    
    public void Dispose() {
        disposed = true;
    }
}
