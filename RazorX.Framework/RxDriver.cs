using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RazorX.Framework;

public enum FragmentMergeStrategyType {
    Swap = 0,
    AppendAfterBegin = 1,
    AppendAfterEnd = 2,
    AppendBeforeBegin = 3,
    AppendBeforeEnd = 4,
    Morph = 5
}

public record CloseDialogTrigger(string DialogId, string? OnCloseData, string? ResetFormId);

public static class RxDriverServices {
    public static void AddRxDriver(this IServiceCollection services) {
        if (!services.Any(x => x.ServiceType == typeof(HtmlRenderer))) {
            services.AddScoped<HtmlRenderer>();
        }
        services.AddScoped<IRxDriver, RxDriver>();
        services.ConfigureOptions<RxJsonOptions>();
    }

    public static bool IsRxRequest(this HttpRequest request) {
        return request.Headers.ContainsKey("rx-request");
    }
}

public interface IRxDriver : IAsyncDisposable, IDisposable {
    IRxResponseBuilder With(HttpContext context);
}

public interface IRxResponseBuilder {
    IRxResponseBuilder AddPage<TRoot, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent, IComponentModel<TModel>;

    IRxResponseBuilder AddPage<TRoot, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent;

    IRxResponseBuilder AddPage<TRoot, THead, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent, IComponentModel<TModel>;

    IRxResponseBuilder AddPage<TRoot, THead, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent;

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

    Task<IResult> Render(
        bool ignoreActiveElementValueOnMorph = false
    );
}

file sealed class RxDriver(HtmlRenderer htmlRenderer, ILogger<RxDriver> logger) : IRxDriver {
    public IRxResponseBuilder With(HttpContext context) {
        return new RxResponseBuilder(context, htmlRenderer, logger);
    }

    public ValueTask DisposeAsync() {
        logger.LogDebug("Async Disposed");
        return htmlRenderer.DisposeAsync();
    }

    public void Dispose() {
        logger.LogDebug("Disposed");
        htmlRenderer.Dispose();
    }
}

file record MergeStrategy(string Target, string Strategy);

file sealed class RxResponseBuilder(HttpContext context, HtmlRenderer htmlRenderer, ILogger logger) : IRxResponseBuilder  {
    private bool isRendering = false;
    private Type? rootComponent = null;
    private ParameterView rootParameters;
    private readonly StringBuilder content = new();
    private readonly List<Task> renderTasks = [];
    private readonly List<MergeStrategy> mergeStrategies = [];
    private CloseDialogTrigger? closeDialogTrigger;
    private static readonly JsonSerializerOptions serializerSettings = new(JsonSerializerDefaults.Web);

    public IRxResponseBuilder AddPage<TRoot, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent, IComponentModel<TModel> {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        var pageComponentParameters = new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        };
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.MainContentParameters),  pageComponentParameters },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddPage<TRoot, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where TComponent : IComponent {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddPage<TRoot, THead, TComponent, TModel>(TModel model, string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent, IComponentModel<TModel> {
        CheckRenderingStatus();
        CheckPageRenderStatus();
         var pageComponentParameters = new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        };
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.HeadContent), typeof(THead) },
            { nameof(IRootComponent.MainContentParameters),  pageComponentParameters },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddPage<TRoot, THead, TComponent>(string? title = null)
    where TRoot : IRootComponent
    where THead : IComponent
    where TComponent : IComponent {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        rootComponent = typeof(TRoot);
        rootParameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IRootComponent.MainContent), typeof(TComponent) },
            { nameof(IRootComponent.HeadContent), typeof(THead) },
            { nameof(IRootComponent.Title), title },
        });
        return this;
    }

    public IRxResponseBuilder AddFragment<TComponent, TModel>(
        TModel model,
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent, IComponentModel<TModel> {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
            { nameof(IComponentModel<TModel>.Model), model }
        });
        renderTasks.Add(htmlRenderer.Dispatcher.InvokeAsync(async () => {
            var output = await htmlRenderer.RenderComponentAsync<TComponent>(parameters);
            content.Append($"<template id=\"{targetId}-fragment\">{output.ToHtmlString()}</template>");
        }));
        AddMergeStrategy(targetId, fragmentMergeStrategy);
        return this;
    }

    public IRxResponseBuilder AddFragment<TComponent>(
        string targetId,
        FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
    ) where TComponent : IComponent {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        renderTasks.Add(htmlRenderer.Dispatcher.InvokeAsync(async () => {
            var output = await htmlRenderer.RenderComponentAsync<TComponent>();
            content.Append($"<template id=\"{targetId}-fragment\">{output.ToHtmlString()}</template>");
        }));
        AddMergeStrategy(targetId, fragmentMergeStrategy);
        return this;
    }

    public IRxResponseBuilder RemoveElement(string targetId) {
        CheckRenderingStatus();
        CheckPageRenderStatus();
        mergeStrategies.Add(new(targetId, "remove"));
        return this;
    }

    public IRxResponseBuilder AddTriggerCloseDialog(string dialogId, string? onCloseData = null, string? resetFormId = null) {
        CheckRenderingStatus();
        closeDialogTrigger = new(dialogId, onCloseData, resetFormId);
        return this;
    }

    public async Task<IResult> Render(
        bool ignoreActiveElementValueOnMorph = false
    ) {
        CheckRenderingStatus();
        if (rootComponent is not null) {
            logger.LogDebug("Rendering Page");
            return await HandlePageRequest();
        }
        if (!context.Request.IsRxRequest()) {
            throw new InvalidOperationException("Partial rendering is not supported for synchronous requests.");
        }
        isRendering = true;
        //triggers
        if (closeDialogTrigger != null) {
            context.Response.Headers.Append("rx-trigger-close-dialog", JsonSerializer.Serialize(closeDialogTrigger, serializerSettings));
        }
        //fragments
        if (ignoreActiveElementValueOnMorph) {
            context.Response.Headers.Append("rx-morph-ignore-active", string.Empty);
        }
        context.Response.Headers.Append("rx-merge", JsonSerializer.Serialize(mergeStrategies, serializerSettings));
        if (renderTasks.Count != 0) {
            logger.LogDebug("Rendering Fragments");
            await Task.WhenAll(renderTasks);
        }
        if (content.Length == 0) {
            return TypedResults.NoContent();
        }
        return Results.Content(content.ToString(), contentType: "text/html");
    }

    private void AddMergeStrategy(string targetId, FragmentMergeStrategyType fragmentMergeStrategy) {
        var mergeStrategy = fragmentMergeStrategy switch {
            FragmentMergeStrategyType.Swap => "swap",
            FragmentMergeStrategyType.AppendAfterBegin => "afterbegin",
            FragmentMergeStrategyType.AppendAfterEnd => "afterend",
            FragmentMergeStrategyType.AppendBeforeBegin => "beforebegin",
            FragmentMergeStrategyType.AppendBeforeEnd => "beforeend",
            _ => "morph"
        };
        mergeStrategies.Add(new(targetId, mergeStrategy));
    }

    private async Task<IResult> HandlePageRequest() {
        string output = default!;
        await htmlRenderer.Dispatcher.InvokeAsync(async () => {
            var root = await htmlRenderer.RenderComponentAsync(rootComponent!, rootParameters);
            output = root.ToHtmlString();
        });
        return Results.Content(output, "text/html");
    }

    private void CheckRenderingStatus() {
        if (isRendering) {
            throw new InvalidOperationException("Render has already been called and may only be called once per request.");
        }
    }

    private void CheckPageRenderStatus() {
        if (rootComponent is not null) {
            throw new InvalidOperationException("RxDriver is set to render a page. No other operations are allowed and Render must be called.");
        }
    }
}
