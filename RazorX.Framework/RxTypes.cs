namespace RazorX.Framework;

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