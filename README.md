# RazorX.Framework

[![Hypercommit](https://img.shields.io/badge/Hypercommit-DB2475)](https://hypercommit.com/razorx-framework)

**RazorX.Framework** is a Server-Driven UI (SDUI) hypermedia framework that rethinks where architectural decisions belong in web applications. Inspired by htmx's hypermedia approach, RazorX takes a fundamentally different path: it separates concerns based on their natural ownership - the server controls what happens to the UI, while the client controls when and how to request changes.

This separation represents a philosophical stance about hypermedia applications. In RazorX, determining where a new todo appears in the DOM and how it merges with existing content is business logic that belongs on the server. The server understands the complete UI structure, relationships between components, and the semantic intent of updates. Meanwhile, the client owns interaction concerns - when users click a button that issues a request to an endpoint, the client handles the mechanics for the request, like queueing, encoding form data as JSON, or including state data that was persisted in a previous response.

By choosing deep ASP.NET Core integration over server-agnostic design, RazorX can fully realize this vision. Response headers carry precise DOM manipulation instructions while HTML attributes define request behaviors. The server orchestrates complex multi-element updates atomically through a fluent API, while the client remains a thin hypermedia agent that follows server directives without embedding application logic. This achieves what REST always promised: the server drives application state through hypermedia controls.

The result is a framework where responsibilities live where they belong. Business logic resides entirely on the server, expressed through Razor components and strongly-typed handlers. Interaction patterns stay on the client, managed through declarative attributes. This creates a coherent mental model where changing application behavior requires only server-side changes, while client-side code remains stable and reusable. It's hypermedia as it was meant to be - with the server as the single source of truth for both state and state transitions.

## Getting Started

### Installation

Install the RazorX.Framework NuGet package:

```bash
dotnet add package RazorX.Framework
```

### Basic Setup

Configure RazorX in your ASP.NET Core application's `Program.cs`:

```csharp
using RazorX.Framework;

var builder = WebApplication.CreateBuilder(args);

// Add RazorX services
builder.Services.AddRxDriver(); // Defaults to JSON form encoding

var app = builder.Build();

// Serve static files (for razorx.js and razorx.css)
app.UseStaticFiles();

// Map RazorX routes
app.MapGroup(string.Empty).MapRoutes();

app.Run();
```

**Note:** By default, RazorX encodes form data as JSON for optimal compatibility with ASP.NET Core minimal APIs, which provide better model binding for JSON payloads. If you're using traditional MVC controllers or prefer standard form encoding, you can disable JSON conversion:

```csharp
builder.Services.AddRxDriver(options => {
    options.AddJsonConverters = false; // Use traditional form encoding
});
```

### Client Setup

The RazorX client files are automatically copied to your `wwwroot` folder during build:

- **`razorx.js`** (~32KB) - The JavaScript client file (compiled from TypeScript source) that handles:
  - Event delegation and trigger management
  - AJAX request processing
  - DOM manipulation via fragment merging
  - Memory management with automatic cleanup
  
- **`razorx.css`** (~6KB) - Essential styles for:
  - Toast notifications positioning and animations
  - Loading indicator visibility states

Initialize the client in your layout or page:

```html
<link rel="stylesheet" href="/css/razorx.css">
<script type="module">
    import { razorx } from '/js/razorx.js';
    
    razorx.init({
        // encodeRequestFormDataAsJson: true is the default
        // Set to false if using traditional form encoding
    });
</script>
```

**Note on script placement:** JavaScript modules are deferred by default, executing after the DOM is fully parsed regardless of placement in `<head>` or `<body>`. This ensures `razorx.init()` runs after all elements with `data-rx-*` attributes exist. Script placement is therefore a matter of preference - use `<head>` for organization or `<body>` for traditional placement.

### Example Project

For a complete working implementation, see the [RazorX Framework Example](https://github.com/ranzlee/razorx-framework-example) project. The examples below are simplified to demonstrate the general patterns and do not include all necessary code.

### Step 1: Create a Layout (IRootComponent)

First, create a layout component that implements `IRootComponent`. This serves as the shell for full page renders:

```razor
@* Components/Layout/App.razor *@
@implements IRootComponent

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>@(Title ?? "RazorX App")</title>
    <link rel="stylesheet" href="/css/razorx.css">
    <script type="module">
        import { razorx } from '/js/razorx.js';
        razorx.init({
            // Add CSRF token if using antiforgery
            addCookieToRequestHeader: "RequestVerificationToken"
        });
    </script>
    @if (HeadContent != null)
    {
        <DynamicComponent Type="@HeadContent" />
    }
</head>
<body>
    <DynamicComponent Type="@MainContent" Parameters="@MainContentParameters" />
</body>
</html>

@code {
    [Parameter] public Type? HeadContent { get; set; }
    [Parameter] public Type MainContent { get; set; } = null!;
    [Parameter] public Dictionary<string, object?> MainContentParameters { get; set; } = [];
    [Parameter] public string? Title { get; set; }
}
```

**Note on DynamicComponent usage:** RazorX uses ASP.NET Core's `DynamicComponent` from Razor Components to enable runtime component selection. The RxDriver passes component *types* (not instances) to the layout:

- `MainContent` receives the Type of the page component to render
- `HeadContent` optionally receives a Type for additional head elements
- `MainContentParameters` passes the model data as a dictionary to the MainContent component

This pattern allows the server to dynamically control which components are rendered without the layout needing compile-time knowledge of specific page types. When `RenderPage<App, HomePage, HomeModel>()` is called, the framework passes `typeof(HomePage)` as MainContent and the model wrapped in MainContentParameters.

### Step 2: Create Components

Create your page and fragment components. Page components are full views, while fragment components are reusable pieces:

```razor
@* Components/Todo/TodoListPage.razor *@
@implements IComponentModel<TodoListModel>

<div id="todo-container">
    <h1>Todo List</h1>

    <!-- Search with debounce -->
    <input id="search-todos"
           type="search"
           name="filter"
           data-rx-action="/search-todos"
           data-rx-trigger="input"
           data-rx-debounce="400"
           placeholder="Search todos...">

    <!-- Todo list container -->
    <div id="todo-list">
        @foreach (var todo in Model.Todos)
        {
            <TodoItem Model="@todo" />
        }
    </div>

    <!-- Add new todo button -->
    <button id="add-todo-btn"
            data-rx-action="/todo/new"
            data-rx-method="GET">
        Add Todo
    </button>
</div>

@code {
    [Parameter] public TodoListModel Model { get; set; } = null!;
}
```

```razor
@* Components/Todo/TodoItem.razor *@
@implements IComponentModel<TodoModel>

<article id="todo-item-@Model.Id">
    <div>@Model.Text</div>
    <button data-rx-action="/todo/@Model.Id"
            data-rx-method="DELETE"
            data-rx-loading-indicator="delete-spinner-@Model.Id">
        Delete
        <span id="delete-spinner-@Model.Id"
              class="rx-loading-hidden"
              aria-busy="true"></span>
    </button>
</article>

@code {
    [Parameter] public TodoModel Model { get; set; } = null!;
}
```

### Step 3: Create a Request Handler

Create a handler that serves both full page requests and fragment updates:

```csharp
using RazorX.Framework;

// Model definitions
public record TodoModel(int Id, string Text, bool IsComplete);
public record TodoListModel(List<TodoModel> Todos, int TotalCount);

public class TodoHandler : RequestHandler
{
    private static readonly List<TodoModel> _todos = [];

    public override void MapRoutes(IEndpointRouteBuilder router)
    {
        router.MapGet("/", GetTodoPage);
        router.MapGet("/search-todos", SearchTodos);
        router.MapDelete("/todo/{id:int}", DeleteTodo);
        router.MapGet("/todo/new", GetNewTodoForm);
        router.MapPost("/todo", CreateTodo);
    }

    // Full page render (initial load)
    public static async Task<IResult> GetTodoPage(
        HttpContext context,
        IRxDriver rxDriver)
    {
        var model = new TodoListModel(_todos, _todos.Count);

        return await rxDriver.RenderPage<App, TodoListPage, TodoListModel>(
            context,
            model,
            "Todo List"
        );
    }

    // Search with fragment updates
    public static async Task<IResult> SearchTodos(
        HttpContext context,
        IRxDriver rxDriver,
        string filter = "")
    {
        var filtered = _todos
            .Where(t => t.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return await rxDriver
            .With(context)
            .AddFragment<TodoList, List<TodoModel>>(
                filtered,
                "todo-list",
                FragmentMergeStrategyType.SwapInner)
            .AddTriggerSetState("filter", filter, MetadataScope.Session, updateUrl: true)
            .Render();
    }

    // Delete with element removal
    public static async Task<IResult> DeleteTodo(
        HttpContext context,
        IRxDriver rxDriver,
        int id)
    {
        var todo = _todos.FirstOrDefault(t => t.Id == id);
        if (todo == null)
        {
            return Results.NotFound();
        }

        _todos.Remove(todo);

        return await rxDriver
            .With(context)
            .RemoveElement($"todo-item-{id}")
            .AddTriggerToast("Todo deleted", ToastType.Success)
            .Render();
    }

    // Return a form fragment for creating new todo
    public static async Task<IResult> GetNewTodoForm(
        HttpContext context,
        IRxDriver rxDriver)
    {
        return await rxDriver
            .With(context)
            .AddFragment<TodoForm>("todo-container", FragmentMergeStrategyType.AppendBeforeEnd)
            .AddTriggerFocusElement("todo-text-input", positionCursorEnd: true)
            .Render();
    }

    // Create todo and update multiple fragments
    public static async Task<IResult> CreateTodo(
        HttpContext context,
        IRxDriver rxDriver,
        [FromForm] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return await rxDriver
                .With(context)
                .AddTriggerToast("Text is required", ToastType.Error)
                .Render();
        }

        var todo = new TodoModel(_todos.Count + 1, text, false);
        _todos.Add(todo);

        return await rxDriver
            .With(context)
            .AddFragment<TodoItem, TodoModel>(
                todo,
                "todo-list",
                FragmentMergeStrategyType.AppendAfterBegin)
            .RemoveElement("todo-form")
            .AddTriggerToast("Todo created!", ToastType.Success)
            .AddTriggerFocusElement("add-todo-btn")
            .Render();
    }
}
```

### Antiforgery Support

To add CSRF protection to your application:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddRxDriver();
builder.Services.AddAntiforgery();     // ASP.NET Core antiforgery
builder.Services.AddRxAntiforgery();   // RazorX antiforgery integration

var app = builder.Build();

// Middleware pipeline
app.UseStaticFiles();
app.UseAntiforgery();
app.UseRxAntiforgeryCookie();  // Manages CSRF tokens for AJAX requests

// Routes
app.MapGroup(string.Empty).MapRoutes();

app.Run();
```

Then update your client initialization:

```javascript
razorx.init({
    addCookieToRequestHeader: "RequestVerificationToken"
});
```

### Important Notes

- **Templating**: RazorX uses Razor Components (.razor files) exclusively. Traditional Razor Pages (.cshtml) are not supported.

- **Routing**: RazorX uses the `RequestHandler` pattern with minimal APIs by default. Traditional MVC controllers can coexist - simply add `services.AddControllers()` and `app.MapControllers()`. Controllers can return Razor Components using the same `IRxDriver` methods.

- **Request Detection**: RazorX automatically detects whether to return a full page or fragment based on the presence of the "rx-request" header. You can check this in your handlers using the `IsRxRequest()` extension method:

  ```csharp
  // Useful for error handling or conditional logic
  if (context.Request.IsRxRequest())
  {
      // This is an AJAX request from RazorX client
      // Return 202 Accepted with location header for redirects
      return TypedResults.Accepted("/error");
  }
  else
  {
      // This is a regular page navigation
      return TypedResults.Redirect("/error");
  }
  ```

- **Element IDs**: Any element with a `data-rx-action` attribute must have a unique ID for proper request tracking. The framework will throw an error if an ID is missing.

- **Special Triggers**: The `initialized`, `poll`, and `revealed` triggers must use GET method. They cannot be debounced and don't support `data-rx-disable-queueing`.

## Framework Mechanics Reference

### RxDriver API

The `IRxDriver` interface provides methods for rendering pages and building AJAX responses.

#### Page Rendering Methods

#### `RenderPage<TRoot, TComponent>(HttpContext context, string? title)`

Renders a full HTML page without a model.

#### `RenderPage<TRoot, TComponent, TModel>(HttpContext context, TModel model, string? title)`

Renders a full HTML page with a model.

#### `RenderPage<TRoot, THead, TComponent>(HttpContext context, string? title)`

Renders a full HTML page with custom head content.

#### `RenderPage<TRoot, THead, TComponent, TModel>(HttpContext context, TModel model, string? title)`

Renders a full HTML page with custom head content and a model.

#### Response Builder Methods

#### `With(HttpContext context)`

Starts building a response for the given HTTP context. Returns `IRxResponseBuilder`.

#### IRxResponseBuilder Methods

#### Fragment Methods

- **`AddFragment<TComponent>(string targetId, FragmentMergeStrategyType strategy)`**
  Adds a component fragment without a model.

- **`AddFragment<TComponent, TModel>(TModel model, string targetId, FragmentMergeStrategyType strategy)`**
  Adds a component fragment with a model.

- **`RemoveElement(string targetId)`**
  Removes an element from the DOM.

#### Trigger Methods

- **`AddTriggerToast(string message, ToastType type, ...)`**
  Shows a toast notification. Optional parameters: duration, vertical position, horizontal position, click to dismiss.

- **`AddTriggerFocusElement(string elementId, bool positionCursorEnd = false)`**
  Sets focus to an element, optionally positioning cursor at end.

- **`AddTriggerSetState(string key, string value, MetadataScope scope, bool updateUrl = false)`**
  Persists a single state value to browser storage.

- **`AddTriggerSetStateBatch(Dictionary<string, string> states, MetadataScope scope, bool updateUrl = false)`**
  Persists multiple state values to browser storage.

- **`AddTriggerCloseDialog(string dialogId, string? onCloseData = null, string? resetFormId = null)`**
  Closes a dialog element, optionally resetting a form.

#### Render Method

- **`Render(bool ignoreActiveElementValueOnMorph = false)`**
  Executes the response, returning an `IResult`.

#### Fragment Merge Strategies

- **`Swap`** - Replaces entire target element (default)
- **`SwapInner`** - Replaces inner content only
- **`Morph`** - Intelligent DOM diffing (preserves state)
- **`AppendAfterBegin`** - Insert as first child
- **`AppendBeforeEnd`** - Insert as last child
- **`AppendBeforeBegin`** - Insert before target
- **`AppendAfterEnd`** - Insert after target

#### Enums

- **`ToastType`**: Info, Success, Warning, Error
- **`ToastVerticalPosition`**: Top, Center, Bottom
- **`ToastHorizontalPosition`**: Left, Middle, Right
- **`MetadataScope`**: Session (sessionStorage), Persistent (localStorage)

### Client Attributes Reference

#### Core Attributes

| Attribute | Required | Description | Example |
|-----------|----------|-------------|----------|
| `data-rx-action` | Yes | URL/path for the request | `data-rx-action="/api/todos"` |
| `data-rx-method` | No | HTTP method (GET, POST, PUT, PATCH, DELETE). Defaults: GET for most elements, POST for forms | `data-rx-method="DELETE"` |
| `data-rx-trigger` | No | Event(s) that trigger the request. Default: "click" for buttons, "submit" for forms, "change" for inputs | `data-rx-trigger="input"` or `data-rx-trigger='["click", "blur"]'` |

#### Request Modifiers

| Attribute | Description | Example |
|-----------|-------------|----------|
| `data-rx-debounce` | Delay in milliseconds before sending request | `data-rx-debounce="500"` |
| `data-rx-disable-in-flight` | Disable element while request is in progress | `data-rx-disable-in-flight` or `data-rx-disable-in-flight="true"` |
| `data-rx-disable-queueing` | Skip request queue, allow parallel requests | `data-rx-disable-queueing` |
| `data-rx-allow-event-default` | Don't prevent default browser behavior | `data-rx-allow-event-default="true"` |

#### UI Feedback

| Attribute | Description | Example |
|-----------|-------------|----------|
| `data-rx-loading-indicator` | ID of element to show/hide during request | `data-rx-loading-indicator="spinner"` |

#### State Management

| Attribute | Description | Example |
|-----------|-------------|----------|
| `data-rx-include-state` | Browser storage keys to include in request. String for single key, JSON array for multiple | `data-rx-include-state="theme"` or `data-rx-include-state='["theme", "locale", "userId"]'` |

#### Delegation

| Attribute | Description | Example |
|-----------|-------------|----------|
| `data-rx-delegate-action-to` | ID of element to transfer the action to | `data-rx-delegate-action-to="confirm-dialog-ok"` |

#### File Upload

| Attribute | Description | Example |
|-----------|-------------|----------|
| `data-rx-file-upload-max-size` | Maximum file size in bytes | `data-rx-file-upload-max-size="5242880"` |
| `data-rx-file-upload-timeout` | Upload timeout in milliseconds | `data-rx-file-upload-timeout="60000"` |
| `data-rx-file-upload-progress-id` | ID of progress element to update | `data-rx-file-upload-progress-id="upload-progress"` |

### Special Triggers

Special triggers use JSON syntax in the `data-rx-trigger` attribute.

#### Initialized

Fires once when element enters DOM.

```html
<div data-rx-action="/api/load" data-rx-trigger='{"type": "initialized"}'></div>
<div data-rx-action="/api/load" data-rx-trigger='{"type": "initialized", "delay": 1000}'></div>
```

#### Poll

Fires repeatedly at intervals.

```html
<div data-rx-action="/api/status" data-rx-trigger='{"type": "poll", "interval": 5000}'></div>
```

#### Revealed

Fires when element enters viewport.

```html
<div data-rx-action="/api/lazy" data-rx-trigger='{"type": "revealed"}'></div>
<div data-rx-action="/api/lazy" data-rx-trigger='{"type": "revealed", "margin": "200px"}'></div>
```

#### Combining Triggers

Mix regular and special triggers.

```html
<button data-rx-action="/api/data" data-rx-trigger='["click", {"type": "poll", "interval": 30000}]'>Click or Auto-refresh</button>
<div data-rx-action="/api/update" data-rx-trigger='["input", "blur", {"type": "initialized"}]'>Content</div>
```

**Note**: Special triggers must use GET method and cannot be debounced.

### Complete Element Examples

Common attribute combinations:

```html
<!-- Search input with debounce -->
<input data-rx-action="/api/search" data-rx-trigger="input" data-rx-debounce="300" />

<!-- Form with loading indicator -->
<form data-rx-action="/api/save" data-rx-method="POST" data-rx-disable-in-flight data-rx-loading-indicator="save-spinner">
  <!-- form content -->
</form>

<!-- Button with state inclusion -->
<button data-rx-action="/api/filter" data-rx-include-state='["sort", "filter", "page"]'>Apply Filter</button>

<!-- Delete with delegation to confirmation -->
<button data-rx-action="/api/item/1" data-rx-method="DELETE" data-rx-delegate-action-to="confirm-ok">Delete</button>

<!-- File input with progress -->
<input type="file" data-rx-action="/api/upload" data-rx-file-upload-progress-id="progress" data-rx-file-upload-max-size="10485760" />

<!-- Lazy loading with revealed trigger -->
<div data-rx-action="/api/content" data-rx-trigger='{"type": "revealed", "margin": "100px"}'>Loading...</div>

<!-- Dashboard with multiple triggers -->
<div data-rx-action="/api/metrics" data-rx-trigger='[{"type": "initialized"}, {"type": "poll", "interval": 10000}]'>Dashboard</div>
```

### Response Headers

Headers sent by the server to control client behavior.

| Header | Description |
|--------|-------------|
| `rx-merge` | JSON array of fragment merge strategies |
| `rx-trigger-close-dialog` | JSON object with dialog close instructions |
| `rx-trigger-focus-element` | JSON object with focus instructions |
| `rx-trigger-set-state` | JSON array of state updates |
| `rx-trigger-toast` | JSON object with toast notification |
| `rx-morph-ignore-active` | Boolean flag to preserve active element value |

### Request Headers

Headers sent by the client.

| Header | Description |
|--------|-------------|
| `rx-request` | Present on all AJAX requests (empty value) |

### CSS Classes

Classes used by the framework.

| Class | Description |
|-------|-------------|
| `rx-loading-visible` | Applied to loading indicators when visible |
| `rx-loading-hidden` | Applied to loading indicators when hidden |

### Extension Methods

#### `HttpContext.Request.IsRxRequest()`

Returns `true` if the request is an AJAX request from the RazorX client.

### Component Interfaces

#### `IRootComponent`

Implemented by layout components for full page rendering.

#### `IComponentModel<TModel>`

Implemented by components that accept a model parameter.

### Request Handler Pattern

#### `RequestHandler`

Abstract base class for defining routes and handlers.

```csharp
public abstract class RequestHandler
{
    public abstract void MapRoutes(IEndpointRouteBuilder router);
}
```

Routes are automatically discovered and registered at startup.

## Events and Callbacks

RazorX provides three ways to hook into the framework's lifecycle: DOM events, global callbacks, and element callbacks.

### Client Events

The framework dispatches custom events on the document that you can listen to:

```javascript
document.addEventListener('rx:before-fetch', (event) => {
    console.log('Request starting:', event.detail.requestConfiguration.action);
});
```

#### Available Events

| Event | Detail Properties | Cancelable | Description |
|-------|------------------|------------|-------------|
| `rx:before-document-processed` | None | No | Fired before initial document processing |
| `rx:after-document-processed` | None | No | Fired after initial document processing |
| `rx:before-initialize-element` | `{ element: HTMLElement }` | Yes | Fired before element initialization. Call `preventDefault()` to cancel |
| `rx:after-initialize-element` | `{ element: HTMLElement }` | No | Fired after element initialization |
| `rx:before-fetch` | `{ triggerElement: HTMLElement, requestConfiguration: RequestConfiguration }` | No | Fired before AJAX request. Use `requestConfiguration.abort()` to cancel (see "Aborting Requests") |
| `rx:after-fetch` | `{ triggerElement: HTMLElement, requestDetail: RequestDetail, response: Response }` | No | Fired after AJAX response |
| `rx:before-document-update` | `{ triggerElement: HTMLElement, mergeElement: HTMLElement, strategy: MergeStrategyType }` | Yes | Fired before DOM update. Call `preventDefault()` to cancel |
| `rx:after-document-update` | `{ triggerElement: HTMLElement }` | No | Fired after DOM update |
| `rx:element-added` | `{ element: HTMLElement }` | No | Fired when element added to DOM |
| `rx:element-morphed` | `{ element: HTMLElement }` | No | Fired when element morphed |
| `rx:element-removed` | `{ element: HTMLElement }` | No | Fired when element removed from DOM |
| `rx:element-trigger-error` | `{ triggerElement: HTMLElement, error: unknown }` | No | Fired on request error |
| `rx:file-selected` | `{ fileInput: HTMLInputElement, files: FileInfo[], error?: Error }` | No | Fired when files selected |
| `rx:file-upload-progress` | `{ fileInput: HTMLInputElement, progressContext: FileUploadProgressContext }` | No | Fired during upload progress |

### Global Callbacks

Register callbacks that apply to all RazorX elements:

```javascript
razorx.addCallbacks({
    beforeFetch: (element, config) => {
        // Add auth header to all requests
        config.headers.set('Authorization', 'Bearer ' + token);
    },

    afterFetch: (element, request, response) => {
        // Log all responses
        console.log(`${request.method} ${request.action}: ${response.status}`);
    },

    onElementTriggerError: (element, error) => {
        // Global error handler
        console.error('Request failed:', error);
    }
});
```

#### Available Global Callbacks

| Callback | Parameters | Return Value | Description |
|----------|------------|--------------|-------------|
| `beforeDocumentProcessed` | None | void | Called before initial processing |
| `afterDocumentProcessed` | None | void | Called after initial processing |
| `beforeInitializeElement` | `(element: HTMLElement)` | boolean | Return false to prevent initialization |
| `afterInitializeElement` | `(element: HTMLElement)` | void | Called after element initialized |
| `beforeFetch` | `(element: HTMLElement, config: RequestConfiguration)` | void | Called before request. Can modify config or call `config.abort()` to cancel |
| `afterFetch` | `(element: HTMLElement, request: RequestDetail, response: Response)` | void | Called after response received |
| `beforeDocumentUpdate` | `(triggerElement: HTMLElement, mergeElement: HTMLElement, strategy: MergeStrategyType)` | boolean | Return false to prevent update |
| `afterDocumentUpdate` | `(element: HTMLElement)` | void | Called after DOM update |
| `onElementTriggerError` | `(element: HTMLElement, error: unknown)` | void | Called on request error |
| `onElementAdded` | `(addedElement: HTMLElement)` | void | Called when element with data-rx attributes is added to DOM |
| `onElementMorphed` | `(element: HTMLElement)` | void | Called after element morphed |
| `onElementRemoved` | `(removedElement: HTMLElement)` | void | Called when element with data-rx attributes is removed from DOM |
| `onFileSelected` | `(fileInput: HTMLInputElement, files: FileInfo[], error?: Error)` | void | Called when files selected |
| `onFileUploadProgress` | `(fileInput: HTMLInputElement, context: FileUploadProgressContext)` | void | Called during upload |

### Element Callbacks

Register callbacks for specific elements:

```javascript
const searchInput = document.getElementById('search');
searchInput.addRxCallbacks({
    beforeFetch: (config) => {
        // Add search-specific header
        config.headers.set('X-Search-Context', 'navbar');
    },

    afterDocumentUpdate: () => {
        // Highlight search results
        highlightMatches(searchInput.value);
    }
});
```

#### Available Element Callbacks

| Callback | Parameters | Return Value | Description |
|----------|------------|--------------|-------------|
| `beforeFetch` | `(config: RequestConfiguration)` | void | Called before this element's request. Can modify config or call `config.abort()` to cancel |
| `afterFetch` | `(request: RequestDetail, response: Response)` | void | Called after this element's response |
| `beforeDocumentUpdate` | `(mergeElement: HTMLElement, strategy: MergeStrategyType)` | boolean | Return false to prevent this element's update |
| `afterDocumentUpdate` | None | void | Called after this element triggers DOM update |
| `onElementTriggerError` | `(error: unknown)` | void | Called on this element's request error |
| `onFileUploadProgress` | `(context: FileUploadProgressContext)` | void | Called during this file input's upload |
| `onFileSelected` | `(files: FileInfo[], error?: Error)` | void | Called when files selected on this input |

### Special Patterns

#### Aborting Requests

In `beforeFetch` callbacks, call `config.abort()` to cancel the request:

```javascript
razorx.addCallbacks({
    beforeFetch: (element, config) => {
        if (!userIsAuthenticated()) {
            config.abort(); // Prevents the request
            showLoginModal();
        }
    }
});
```

#### Preventing Updates

Return `false` from `beforeDocumentUpdate` to cancel DOM updates:

```javascript
element.addRxCallbacks({
    beforeDocumentUpdate: (mergeElement, strategy) => {
        if (mergeElement.querySelector('.unsaved-changes')) {
            return confirm('Discard unsaved changes?');
        }
        return true; // Allow update
    }
});
```

#### Request Configuration

The `RequestConfiguration` object in `beforeFetch`:

```typescript
{
    trigger: Event,              // The triggering event
    action: string,              // Request URL
    method: string,              // HTTP method
    body?: FormData | string,    // Request body
    headers: Headers,            // Request headers (modifiable)
    abort: (reason?: string) => void  // Function to cancel request
}
```

#### File Upload Context

The `FileUploadProgressContext` object:

```typescript
{
    file: File,                 // The file being uploaded
    loaded: number,             // Bytes uploaded
    total: number,              // Total file size
    percentage: number          // Upload percentage (0-100)
}
```

### Event vs Callback Precedence

When both events and callbacks are registered, they execute in this order:

1. Element callback (if defined)
2. Global callback (if defined)
3. DOM event (always dispatched)

For cancelable operations (`beforeInitializeElement`, `beforeDocumentUpdate`):

- Element callback returning `false` cancels immediately
- Global callback returning `false` cancels immediately
- Event `preventDefault()` cancels if not already canceled

### Common Use Cases

```javascript
// Global auth header
razorx.addCallbacks({
    beforeFetch: (element, config) => {
        config.headers.set('Authorization', `Bearer ${getToken()}`);
    }
});

// Loading overlay
document.addEventListener('rx:before-fetch', () => {
    showLoadingOverlay();
});
document.addEventListener('rx:after-fetch', () => {
    hideLoadingOverlay();
});

// Error toast
razorx.addCallbacks({
    onElementTriggerError: (element, error) => {
        showErrorToast(error.message);
    }
});

// Analytics tracking
document.addEventListener('rx:after-fetch', (event) => {
    analytics.track('ajax_request', {
        url: event.detail.requestDetail.action,
        status: event.detail.response.status
    });
});
```
