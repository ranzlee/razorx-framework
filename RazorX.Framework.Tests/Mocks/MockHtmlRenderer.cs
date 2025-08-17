using Microsoft.AspNetCore.Components;

namespace RazorX.Framework.Tests.Mocks;

// Mock implementation for testing
public class MockHtmlRenderer : IHtmlRendererWrapper {
    public bool RenderCalled { get; private set; }
    public int RenderCallCount { get; private set; }
    public Type? LastComponentType { get; private set; }
    public ParameterView LastParameters { get; private set; }
    public Dictionary<string, object?> LastParametersDictionary { get; private set; } = [];
    public string LastRenderedContent { get; private set; } = "";
    public List<string> AllRenderedContent { get; private set; } = [];
    public object Dispatcher { get; } = new MockComponentDispatcher();

    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync(Type componentType, ParameterView parameters) {
        RenderCalled = true;
        RenderCallCount++;
        LastComponentType = componentType;
        LastParameters = parameters;
        
        // Extract parameters to dictionary for easier testing
        LastParametersDictionary.Clear();
        foreach (var parameter in parameters) {
            LastParametersDictionary[parameter.Name] = parameter.Value;
        }
        
        // Generate mock content based on component type
        if (componentType.Name.Contains("Fragment")) {
            LastRenderedContent = $"<div>Mock Fragment Content</div>";
        } else {
            LastRenderedContent = $"<html><body>Mock Page Content</body></html>";
        }
        
        // Track all rendered content for debugging
        AllRenderedContent.Add(LastRenderedContent);
        
        return await Task.FromResult(new MockHtmlRootComponent(LastRenderedContent));
    }

    public async ValueTask<IHtmlRootComponentWrapper> RenderComponentAsync<TComponent>(ParameterView parameters) where TComponent : IComponent {
        return await RenderComponentAsync(typeof(TComponent), parameters);
    }

    public void Dispose() {
        // Mock disposal
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync() {
        // Mock async disposal
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}

// Mock implementation of HtmlRootComponent wrapper
public class MockHtmlRootComponent(string content) : IHtmlRootComponentWrapper {
    private readonly string _content = content;

    public string ToHtmlString() => _content;
}

// Mock ComponentDispatcher
public class MockComponentDispatcher {
    public static bool CheckAccess() => true;
    
    public static Task InvokeAsync(Action workItem) {
        workItem();
        return Task.CompletedTask;
    }
    
    public static Task InvokeAsync(Func<Task> workItem) {
        return workItem();
    }
    
    public static Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem) {
        return Task.FromResult(workItem());
    }
    
    public static Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem) {
        return workItem();
    }
}

