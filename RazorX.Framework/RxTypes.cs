using Microsoft.AspNetCore.Routing;

namespace RazorX.Framework;

/// <summary>
/// Interface for a class that contains endpoints.
/// </summary>
public abstract class RequestHandler {
    protected RequestHandler() { }
    public abstract void MapRoutes(IEndpointRouteBuilder router);
}

/// <summary>
/// Interface for a component that represents a page layout.
/// </summary>
public interface IRootComponent {
    public Type? HeadContent { get; set; }
    public Type MainContent { get; set; }
    public Dictionary<string, object?> MainContentParameters { get; set; }
    public string? Title { get; set; }
}

/// <summary>
/// Interface for a component with a model.
/// </summary>
/// <typeparam name="TModel">The model to bind to the component.</typeparam>
public interface IComponentModel<TModel> {
    TModel Model { get; set; }
}

public enum FragmentMergeStrategyType {
    Swap = 0,
    SwapInner = 1,
    AppendAfterBegin = 2,
    AppendAfterEnd = 3,
    AppendBeforeBegin = 4,
    AppendBeforeEnd = 5,
    Morph = 6
}

public enum MetadataScope {
    Session = 0,
    Persistent = 1
}

/// <summary>
/// Toast display types that determine visual styling.
/// </summary>
public enum ToastType {
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Vertical positioning options for toasts.
/// </summary>
public enum ToastVerticalPosition {
    Top = 0,
    Center = 1,
    Bottom = 2
}

/// <summary>
/// Horizontal positioning options for toasts.
/// </summary>
public enum ToastHorizontalPosition {
    Left = 0,
    Middle = 1,
    Right = 2
}