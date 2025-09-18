using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

/// <summary>
/// Represents a trigger that closes a dialog element in the client.
/// </summary>
/// <param name="DialogId">The ID of the dialog element to close.</param>
/// <param name="OnCloseData">Optional data to pass to the close handler.</param>
/// <param name="ResetFormId">Optional ID of a form element to reset after closing.</param>
public record CloseDialogTrigger(string DialogId, string? OnCloseData, string? ResetFormId);
/// <summary>
/// Represents a trigger that sets focus to a specific element in the client.
/// </summary>
/// <param name="ElementId">The ID of the element to focus.</param>
/// <param name="PositionCursorEnd">When true, positions the cursor at the end of the element's content (for input/textarea elements).</param>
public record FocusElementTrigger(string ElementId, bool PositionCursorEnd);
/// <summary>
/// Represents a trigger that sets state in browser storage (session or local storage).
/// </summary>
/// <param name="Key">The storage key name.</param>
/// <param name="Value">The value to store.</param>
/// <param name="Scope">The storage scope ("session" or "local").</param>
/// <param name="UpdateUrl">When true, updates the URL query parameters with the state value.</param>
public record SetStateTrigger(string Key, string Value, string Scope, bool UpdateUrl = false);
/// <summary>
/// Represents a trigger that displays a toast notification in the client.
/// </summary>
/// <param name="Message">The message to display in the toast.</param>
/// <param name="Type">The toast type (success, error, warning, info).</param>
/// <param name="Duration">Duration in milliseconds before the toast auto-dismisses.</param>
/// <param name="VerticalPosition">Vertical position of the toast (top or bottom).</param>
/// <param name="HorizontalPosition">Horizontal position of the toast (left, center, or right).</param>
/// <param name="ClickToDismiss">When true, allows the user to dismiss the toast by clicking on it.</param>
public record ToastTrigger(string Message, string Type, int Duration, string VerticalPosition, string HorizontalPosition, bool ClickToDismiss);

/// <summary>
/// Configuration options for the RazorX.Framework driver.
/// </summary>
public class RxDriverOptions {
    /// <summary>
    /// When true, registers JSON converters for form data encoding.
    /// Default is true to match razorx client default behavior.
    /// Set to false if you're not using JSON-encoded form data.
    /// </summary>
    public bool AddJsonConverters { get; set; } = true;
}

/// <summary>
/// Extension methods for configuring RazorX.Framework services.
/// </summary>
public static class RxDriverServices {
    /// <summary>
    /// Registers RazorX.Framework services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Optional delegate to configure driver options.</param>
    /// <remarks>
    /// This method registers the following services:
    /// - HtmlRenderer and IHtmlRendererWrapper (scoped)
    /// - IRxDriver implementation (scoped)
    /// - IHttpContextAccessor and JSON converters (when AddJsonConverters is true)
    /// </remarks>
    /// <example>
    /// <code>
    /// // Default configuration with JSON converters
    /// builder.Services.AddRxDriver();
    /// 
    /// // Disable JSON converters
    /// builder.Services.AddRxDriver(options => {
    ///     options.AddJsonConverters = false;
    /// });
    /// </code>
    /// </example>
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

    /// <summary>
    /// Determines whether the current HTTP request is a RazorX AJAX request.
    /// </summary>
    /// <param name="request">The HTTP request to check.</param>
    /// <returns>True if the request contains the "rx-request" header; otherwise, false.</returns>
    /// <remarks>
    /// RazorX client automatically adds the "rx-request" header to all AJAX requests.
    /// Use this method to differentiate between full page requests and AJAX fragment requests.
    /// </remarks>
    public static bool IsRxRequest(this HttpRequest request) {
        return request.Headers.ContainsKey("rx-request");
    }
}

/// <summary>
/// The main driver interface for RazorX.Framework, providing server-driven UI rendering capabilities.
/// </summary>
/// <remarks>
/// IRxDriver is the central orchestration engine for rendering components and building SDUI responses.
/// It supports both full page rendering and fragment-based updates via AJAX.
/// Instances are scoped per request and should be injected via dependency injection.
/// </remarks>
public interface IRxDriver : IAsyncDisposable, IDisposable {
    /// <summary>
    /// Creates a response builder for the specified HTTP context.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A response builder for constructing SDUI responses.</returns>
    /// <remarks>
    /// This method creates a new response builder instance for each request.
    /// The builder is used to compose fragments and triggers for the response.
    /// </remarks>
    IRxResponseBuilder With(HttpContext context);
    
    /// <summary>
    /// Renders a full page with a root layout, component, and model.
    /// </summary>
    /// <typeparam name="TRoot">The root layout component type.</typeparam>
    /// <typeparam name="TComponent">The main content component type.</typeparam>
    /// <typeparam name="TModel">The model type for the component.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="model">The model instance to pass to the component.</param>
    /// <param name="title">Optional page title for the HTML document.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An IResult containing the rendered HTML page.</returns>
    Task<IResult> RenderPage<TRoot, TComponent, TModel>(
        HttpContext context, 
        TModel model, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where TComponent : IComponent, IComponentModel<TModel>;
    
    /// <summary>
    /// Renders a full page with a root layout and component without a model.
    /// </summary>
    /// <typeparam name="TRoot">The root layout component type.</typeparam>
    /// <typeparam name="TComponent">The main content component type.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="title">Optional page title for the HTML document.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An IResult containing the rendered HTML page.</returns>
    Task<IResult> RenderPage<TRoot, TComponent>(
        HttpContext context, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where TComponent : IComponent;
    
    /// <summary>
    /// Renders a full page with a root layout, custom head component, main component, and model.
    /// </summary>
    /// <typeparam name="TRoot">The root layout component type.</typeparam>
    /// <typeparam name="THead">The custom head component type for additional head elements.</typeparam>
    /// <typeparam name="TComponent">The main content component type.</typeparam>
    /// <typeparam name="TModel">The model type for the component.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="model">The model instance to pass to the component.</param>
    /// <param name="title">Optional page title for the HTML document.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An IResult containing the rendered HTML page.</returns>
    Task<IResult> RenderPage<TRoot, THead, TComponent, TModel>(
        HttpContext context,
        TModel model, 
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where THead : IComponent
      where TComponent : IComponent, IComponentModel<TModel>;
    
    /// <summary>
    /// Renders a full page with a root layout, custom head component, and main component without a model.
    /// </summary>
    /// <typeparam name="TRoot">The root layout component type.</typeparam>
    /// <typeparam name="THead">The custom head component type for additional head elements.</typeparam>
    /// <typeparam name="TComponent">The main content component type.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="title">Optional page title for the HTML document.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An IResult containing the rendered HTML page.</returns>
    Task<IResult> RenderPage<TRoot, THead, TComponent>(
        HttpContext context,
        string? title = null,
        CancellationToken cancellationToken = default
    ) where TRoot : IRootComponent
      where THead : IComponent
      where TComponent : IComponent;
}

/// <summary>
/// Builder interface for constructing server-driven UI responses with fragments and triggers.
/// </summary>
/// <remarks>
/// IRxResponseBuilder provides a fluent API for composing AJAX responses that update
/// specific parts of the DOM and trigger client-side actions. Each method returns
/// the builder instance for method chaining.
/// </remarks>
public interface IRxResponseBuilder {
    /// <summary>
    /// Adds a component fragment to the response with a model.
    /// </summary>
    /// <typeparam name="TComponent">The component type to render.</typeparam>
    /// <typeparam name="TModel">The model type for the component.</typeparam>
    /// <param name="model">The model instance to pass to the component.</param>
    /// <param name="targetId">The ID of the target DOM element to update.</param>
    /// <param name="fragmentMergeStrategy">The merge strategy for updating the DOM (default: Swap).</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// The fragment will be rendered as HTML and sent to the client with merge instructions.
    /// The client will update the target element based on the specified merge strategy.
    /// </remarks>
    IRxResponseBuilder AddFragment<TComponent, TModel>(
        TModel model,
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent, IComponentModel<TModel>;

    /// <summary>
    /// Adds a component fragment to the response without a model.
    /// </summary>
    /// <typeparam name="TComponent">The component type to render.</typeparam>
    /// <param name="targetId">The ID of the target DOM element to update.</param>
    /// <param name="fragmentMergeStrategy">The merge strategy for updating the DOM (default: Swap).</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// Use this overload for components that don't require a model.
    /// </remarks>
    IRxResponseBuilder AddFragment<TComponent>(
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent;

    /// <summary>
    /// Adds an instruction to remove a DOM element from the page.
    /// </summary>
    /// <param name="targetId">The ID of the element to remove.</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// The element and all its children will be removed from the DOM.
    /// </remarks>
    IRxResponseBuilder RemoveElement(string targetId);

    /// <summary>
    /// Adds a trigger to close a dialog element.
    /// </summary>
    /// <param name="dialogId">The ID of the dialog element to close.</param>
    /// <param name="onCloseData">Optional data to pass to the dialog's close handler.</param>
    /// <param name="resetFormId">Optional ID of a form to reset after closing the dialog.</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// This trigger will close HTML dialog elements and optionally reset associated forms.
    /// </remarks>
    IRxResponseBuilder AddTriggerCloseDialog(string dialogId, string? onCloseData = null, string? resetFormId = null);

    /// <summary>
    /// Adds a trigger to set focus to a specific element.
    /// </summary>
    /// <param name="elementId">The ID of the element to focus.</param>
    /// <param name="positionCursorEnd">When true, positions the cursor at the end of the element's content.</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// Useful for directing user attention after an update or for improving form UX.
    /// The positionCursorEnd parameter is particularly useful for input and textarea elements.
    /// </remarks>
    IRxResponseBuilder AddTriggerFocusElement(string elementId, bool positionCursorEnd = false);

    /// <summary>
    /// Adds a trigger to set a single state value in browser storage.
    /// </summary>
    /// <param name="key">The storage key name.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="scope">The storage scope (Session or Local, default: Session).</param>
    /// <param name="updateUrl">When true, also updates the URL query parameters with the state value.</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// State values are stored in browser storage and can be included in subsequent requests
    /// using the data-rx-include-state attribute.
    /// </remarks>
    IRxResponseBuilder AddTriggerSetState(string key, string value, MetadataScope scope = MetadataScope.Session, bool updateUrl = false);

    /// <summary>
    /// Adds a trigger to set multiple state values in browser storage.
    /// </summary>
    /// <param name="state">Dictionary of key-value pairs to store.</param>
    /// <param name="scope">The storage scope (Session or Local).</param>
    /// <param name="updateUrl">When true, also updates the URL query parameters with the state values.</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// Efficiently sets multiple state values in a single operation.
    /// All values will be stored in the same scope.
    /// </remarks>
    IRxResponseBuilder AddTriggerSetStateBatch(Dictionary<string, string> state, MetadataScope scope, bool updateUrl = false);

    /// <summary>
    /// Adds a trigger to display a toast notification.
    /// </summary>
    /// <param name="message">The message to display in the toast.</param>
    /// <param name="type">The toast type affecting its appearance (default: Success).</param>
    /// <param name="duration">Duration in milliseconds before auto-dismiss (default: 3500ms).</param>
    /// <param name="verticalPosition">Vertical position on screen (default: Top).</param>
    /// <param name="horizontalPosition">Horizontal position on screen (default: Right).</param>
    /// <param name="clickToDismiss">When true, allows dismissing by clicking the toast (default: true).</param>
    /// <returns>The response builder for method chaining.</returns>
    /// <remarks>
    /// Toast notifications provide non-intrusive feedback to users.
    /// Multiple toasts can be displayed simultaneously.
    /// </remarks>
    IRxResponseBuilder AddTriggerToast(
        string message,
        ToastType type = ToastType.Success,
        int duration = 3500,
        ToastVerticalPosition verticalPosition = ToastVerticalPosition.Top,
        ToastHorizontalPosition horizontalPosition = ToastHorizontalPosition.Right,
        bool clickToDismiss = true
    );

    /// <summary>
    /// Renders the response with all configured fragments and triggers.
    /// </summary>
    /// <param name="ignoreActiveElementValueOnMorph">When true, preserves the value of the currently focused element during morphing.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>An IResult containing the rendered response with appropriate headers.</returns>
    /// <remarks>
    /// This method should be called last in the builder chain.
    /// It renders all fragments, sets response headers for triggers, and returns the final response.
    /// For AJAX requests, returns fragments with merge instructions.
    /// For full page requests, returns complete HTML.
    /// </remarks>
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

        await RxDispatcherHelper.InvokeOnDispatcherAsync(htmlRenderer.Dispatcher, async () => {
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
        renderTasks.Add(RxDispatcherHelper.InvokeOnDispatcherAsync(htmlRenderer.Dispatcher, async () => {
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
        renderTasks.Add(RxDispatcherHelper.InvokeOnDispatcherAsync(htmlRenderer.Dispatcher, async () => {
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
