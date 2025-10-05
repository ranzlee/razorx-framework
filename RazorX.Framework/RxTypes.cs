using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing;

namespace RazorX.Framework;

/// <summary>
/// Abstract base class for defining HTTP request handlers in RazorX.Framework.
/// </summary>
/// <remarks>
/// Inherit from this class to define route handlers for your application.
/// The framework automatically discovers and registers all RequestHandler implementations at startup.
/// Each handler should map its routes in the MapRoutes method.
/// </remarks>
/// <example>
/// <code>
/// public class ProductHandler : RequestHandler {
///     public override void MapRoutes(IEndpointRouteBuilder router) {
///         router.MapGet("/products", GetProducts);
///         router.MapPost("/products", CreateProduct);
///     }
///     
///     private static async Task&lt;IResult&gt; GetProducts(IRxDriver driver, HttpContext context) {
///         // Handler implementation
///     }
/// }
/// </code>
/// </example>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
public abstract class RequestHandler {
    /// <summary>
    /// Protected constructor to prevent direct instantiation.
    /// </summary>
    protected RequestHandler() { }
    /// <summary>
    /// Maps HTTP routes for this handler.
    /// </summary>
    /// <param name="router">The endpoint route builder to add routes to.</param>
    /// <remarks>
    /// This method is called during application startup.
    /// Use the router parameter to map your endpoints using MapGet, MapPost, etc.
    /// </remarks>
    public abstract void MapRoutes(IEndpointRouteBuilder router);
}

/// <summary>
/// Interface for a component that represents a page layout.
/// </summary>
public interface IRootComponent {
    /// <summary>
    /// Gets or sets the optional component type for custom head elements (meta tags, styles, scripts, etc.).
    /// </summary>
    public Type? HeadContent { get; set; }

    /// <summary>
    /// Gets or sets the parameters to pass to the head content component.
    /// </summary>
    public Dictionary<string, object?> HeadContentParameters { get; set; }

    /// <summary>
    /// Gets or sets the main content component type to render in the page body.
    /// </summary>
    public Type MainContent { get; set; }

    /// <summary>
    /// Gets or sets the parameters to pass to the main content component.
    /// </summary>
    public Dictionary<string, object?> MainContentParameters { get; set; }
}

/// <summary>
/// Interface for a component with a model.
/// </summary>
/// <typeparam name="TModel">The model to bind to the component.</typeparam>
public interface IComponentModel<TModel> {
    /// <summary>
    /// Gets or sets the strongly-typed component model.
    /// </summary>
    TModel Model { get; set; }
}

/// <summary>
/// Defines strategies for merging HTML fragments into the DOM.
/// </summary>
/// <remarks>
/// These strategies control how the client-side JavaScript updates the DOM
/// when receiving fragment responses from the server.
/// </remarks>
public enum FragmentMergeStrategyType {
    /// <summary>
    /// Replaces the entire target element with the fragment content.
    /// </summary>
    Swap = 0,
    /// <summary>
    /// Replaces only the inner HTML of the target element, preserving the element itself.
    /// </summary>
    SwapInner = 1,
    /// <summary>
    /// Inserts the fragment as the first child of the target element.
    /// </summary>
    AppendAfterBegin = 2,
    /// <summary>
    /// Inserts the fragment immediately after the target element.
    /// </summary>
    AppendAfterEnd = 3,
    /// <summary>
    /// Inserts the fragment immediately before the target element.
    /// </summary>
    AppendBeforeBegin = 4,
    /// <summary>
    /// Inserts the fragment as the last child of the target element.
    /// </summary>
    AppendBeforeEnd = 5,
    /// <summary>
    /// Intelligently morphs the target element to match the fragment,
    /// preserving element state where possible (uses Idiomorph algorithm).
    /// </summary>
    Morph = 6
}

/// <summary>
/// Defines the storage scope for client-side state management.
/// </summary>
/// <remarks>
/// Determines whether state values are stored in session storage
/// (cleared when the browser tab closes) or local storage (persists across sessions).
/// </remarks>
public enum MetadataScope {
    /// <summary>
    /// Stores values in session storage (cleared when the browser tab closes).
    /// </summary>
    Session = 0,
    /// <summary>
    /// Stores values in local storage (persists across browser sessions).
    /// </summary>
    Persistent = 1
}

/// <summary>
/// Toast display types that determine visual styling.
/// </summary>
public enum ToastType {
    /// <summary>
    /// Informational toast notification (neutral styling).
    /// </summary>
    Info = 0,

    /// <summary>
    /// Success toast notification (positive styling).
    /// </summary>
    Success = 1,

    /// <summary>
    /// Warning toast notification (cautionary styling).
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Error toast notification (negative styling).
    /// </summary>
    Error = 3
}

/// <summary>
/// Vertical positioning options for toasts.
/// </summary>
public enum ToastVerticalPosition {
    /// <summary>
    /// Display toast at the top of the viewport.
    /// </summary>
    Top = 0,

    /// <summary>
    /// Display toast at the vertical center of the viewport.
    /// </summary>
    Center = 1,

    /// <summary>
    /// Display toast at the bottom of the viewport.
    /// </summary>
    Bottom = 2
}

/// <summary>
/// Horizontal positioning options for toasts.
/// </summary>
public enum ToastHorizontalPosition {
    /// <summary>
    /// Display toast at the left edge of the viewport.
    /// </summary>
    Left = 0,

    /// <summary>
    /// Display toast at the horizontal center of the viewport.
    /// </summary>
    Middle = 1,

    /// <summary>
    /// Display toast at the right edge of the viewport.
    /// </summary>
    Right = 2
}