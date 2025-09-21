# RazorX.Framework

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

- **`razorx.js`** (~45KB) - The JavaScript client file (compiled from TypeScript source) that handles:
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

## Framework Mechanics

RazorX.Framework operates on a request-response cycle where the server controls UI updates through HTML fragments and triggers. The framework provides two primary patterns: full page rendering for initial loads and fragment-based updates for interactions.

## Server-Side: RxDriver

### Dependency Injection

The `IRxDriver` is the core orchestration engine, registered as a scoped service:

```csharp
// Program.cs
builder.Services.AddRxDriver();  // Registers IRxDriver and dependencies
```

### Full Page Rendering

For initial page loads when using Minimal APIs (not needed with MVC/Razor Pages):

```csharp
// Simple page without model
public static async Task<IResult> Get(
    HttpContext context,
    IRxDriver rxDriver)
{
    return await rxDriver.RenderPage<App, HomePage>(
        context,
        "RazorX - Home"
    );
}

// Page with model
public static async Task<IResult> Get(
    HttpContext context,
    IRxDriver rxDriver)
{
    var model = new ExampleModel(
        Todos: _todos,
        Total: _todos.Count,
        Completed: _todos.Count(t => t.IsComplete),
        FileUpload: new FileUploadModel()
    );

    return await rxDriver.RenderPage<App, ExamplesPage, ExampleModel>(
        context,
        model,
        "RazorX - Examples"
    );
}

// With custom head content
return await rxDriver.RenderPage<App, CustomHead, HomePage, HomePageModel>(
    context,
    model,
    title: "Home"
);
```

### Response Builder Pattern

The response builder provides a fluent API for composing AJAX responses with fragments and triggers. Here's a real example from a todo application:

```csharp
// Example: Creating a new todo with multiple UI updates
public static async Task<IResult> NewTodo(
    HttpContext context,
    IRxDriver rxDriver,
    TodoFormModel model)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(model.Text))
    {
        return await rxDriver
            .With(context)
            .AddTriggerToast("Validation error!", ToastType.Error)
            .AddFragment<TodoForm, TodoFormModel>(
                model with { HasError = true },
                "todo-form",
                FragmentMergeStrategyType.Swap)
            .Render();
    }

    // Create the todo
    var todo = new TodoModel(NextId(), model.Text, false);
    _todos.Add(todo);

    // Build response with multiple updates
    return await rxDriver
        .With(context)
        // Close the modal dialog
        .AddTriggerCloseDialog("new-todo-modal")
        // Focus back to the trigger button
        .AddTriggerFocusElement("new-todo-button")
        // Show success message
        .AddTriggerToast("Todo created successfully!", ToastType.Success)
        // Reset the form for next use
        .AddFragment<TodoForm, TodoFormModel>(
            new TodoFormModel(0, "", false, false, false),
            "new-todo-form",
            FragmentMergeStrategyType.Swap)
        // Add the new todo to the list
        .AddFragment<TodoItem, TodoModel>(
            todo,
            "todo-list",
            FragmentMergeStrategyType.AppendAfterBegin)
        // Update the count
        .AddFragment<TodoCount, (int Total, int Completed)>(
            (_todos.Count, _todos.Count(t => t.IsComplete)),
            "todo-count",
            FragmentMergeStrategyType.Swap)
        .Render();
}
```

## Fragment Merge Strategies

The framework provides 7 strategies for updating the DOM:

### 1. **Swap** (Default)
Replaces the entire target element:
```csharp
.AddFragment<Component, Model>(model, "target-id", FragmentMergeStrategyType.Swap)
```
```html
<!-- Before -->
<div id="target-id">Old content</div>

<!-- After -->
<div id="new-id">New content</div>
```

### 2. **SwapInner**
Replaces only the inner content:
```csharp
.AddFragment<Component, Model>(model, "target-id", FragmentMergeStrategyType.SwapInner)
```
```html
<!-- Before -->
<div id="target-id">Old content</div>

<!-- After -->
<div id="target-id">New content</div>
```

### 3. **Morph**
Intelligently updates using DOM diffing (preserves state):
```csharp
.AddFragment<Component, Model>(model, "target-id", FragmentMergeStrategyType.Morph)
```
- Preserves focus, scroll position, and form values
- Minimizes DOM mutations
- Best for complex updates

### 4-7. **Positional Inserts**
```csharp
// Insert as first child
.AddFragment<Alert>(id, FragmentMergeStrategyType.AppendAfterBegin)

// Insert as last child
.AddFragment<Item>(id, FragmentMergeStrategyType.AppendBeforeEnd)

// Insert before target
.AddFragment<Header>(id, FragmentMergeStrategyType.AppendBeforeBegin)

// Insert after target
.AddFragment<Footer>(id, FragmentMergeStrategyType.AppendAfterEnd)
```

### Remove Strategy
Remove elements from the DOM:
```csharp
.RemoveElement("old-modal")
.RemoveElement("temporary-message")
```

## Response Triggers

Triggers execute client-side actions after DOM updates:

### Toast Notifications
```csharp
.AddTriggerToast(
    message: "Profile updated successfully",
    type: ToastType.Success,
    duration: 3500,  // milliseconds
    verticalPosition: ToastVerticalPosition.Top,
    horizontalPosition: ToastHorizontalPosition.Right,
    clickToDismiss: true
)
```

### Focus Management
```csharp
// Focus an element
.AddTriggerFocusElement("username-input")

// Focus with cursor at end (useful for inputs)
.AddTriggerFocusElement("search-box", positionCursorEnd: true)
```

### State Persistence
```csharp
// Session storage (cleared on tab close)
.AddTriggerSetState("currentTab", "products", MetadataScope.Session)

// Local storage (persists across sessions)
.AddTriggerSetState("theme", "dark", MetadataScope.Persistent)

// Update URL query parameters
.AddTriggerSetState("page", "2", MetadataScope.Session, updateUrl: true)

// Set multiple states
.AddTriggerSetStateBatch(new Dictionary<string, string> {
    ["filter"] = "active",
    ["sort"] = "date"
}, MetadataScope.Session, updateUrl: true)
```

### Dialog Control
```csharp
.AddTriggerCloseDialog(
    dialogId: "edit-modal",
    onCloseData: "saved",  // Passed to dialog close handler
    resetFormId: "edit-form"  // Optional form to reset
)
```

## Client Attributes Reference

### Core Action Attributes

#### `data-rx-action`
The URL/path for the AJAX request:
```html
<button data-rx-action="/api/items/create">Add Item</button>
<div data-rx-action="/notifications/latest">...</div>
```

#### `data-rx-method`
HTTP method (defaults: GET for most elements, POST for forms):
```html
<button data-rx-action="/api/items/1" data-rx-method="DELETE">Delete</button>
<form data-rx-action="/api/items" data-rx-method="PUT">...</form>
```

#### `data-rx-trigger`
Event that triggers the request:
```html
<!-- Single trigger -->
<input data-rx-action="/search" data-rx-trigger="input">

<!-- JSON array for multiple triggers -->
<div data-rx-action="/update" data-rx-trigger='["click", "keyup"]'>

<!-- Default triggers -->
<!-- form: submit, input/select/textarea: change, others: click -->
```

### Request Modifiers

#### `data-rx-debounce`
Delay before sending request (milliseconds):
```html
<input data-rx-action="/search"
       data-rx-trigger="input"
       data-rx-debounce="500">
```

#### `data-rx-disable-in-flight`
Disable element during request:
```html
<button data-rx-action="/process"
        data-rx-disable-in-flight>Process</button>

<!-- Disables entire form -->
<form data-rx-action="/submit"
      data-rx-disable-in-flight>...</form>
```

#### `data-rx-disable-queueing`
Skip request queue (parallel requests):
```html
<button data-rx-action="/parallel-task"
        data-rx-disable-queueing>Run</button>
```

#### `data-rx-allow-event-default`
Don't prevent default browser behavior:
```html
<a href="/fallback"
   data-rx-action="/api/action"
   data-rx-allow-event-default>Link</a>
```

### UI Feedback

#### `data-rx-loading-indicator`
Show/hide element during request:
```html
<button data-rx-action="/slow-task"
        data-rx-loading-indicator="spinner">
    Process
</button>

<div id="spinner" class="rx-loading-hidden">
    Loading...
</div>
```

### State Management

#### `data-rx-include-state`
Include browser storage values in request:
```html
<!-- Single key -->
<form data-rx-action="/filter"
      data-rx-include-state="theme">

<!-- Multiple keys -->
<div data-rx-action="/dashboard"
     data-rx-include-state='["theme", "locale", "timezone"]'>
```

### Delegation

#### `data-rx-delegate-action-to`
Transfer action to another element (useful for confirmation dialogs):
```razor
@* TodoItem.razor - Delete button delegates to confirmation modal *@
<button id="delete-todo-trigger-@Model.Id"
        data-rx-action="/todo/@Model.Id"
        data-rx-method="DELETE"
        data-rx-delegate-action-to="delete-todo-modal-ok">
    Delete
</button>

@* In the modal dialog *@
<dialog id="delete-todo-modal">
    <article>
        <h2>Delete Todo</h2>
        <p>Are you sure you want to delete this todo?</p>
        <footer>
            <button id="delete-todo-modal-close" class="secondary">Cancel</button>
            <!-- This button receives the delegated action -->
            <button id="delete-todo-modal-ok" data-rx-include-state="filter">OK</button>
        </footer>
    </article>
</dialog>
```

When the delete button is clicked:
1. The confirmation modal opens (via JavaScript)
2. The action is delegated to the OK button
3. If OK is clicked, the DELETE request is sent
4. The modal closes and the todo is removed

### File Upload with Progress and Validation

```razor
@* FileForm.razor - Complete file upload implementation *@
<input
    id="file-upload-input"
    name="uploadedFile"
    type="file"
    data-rx-action="/file-upload"
    data-rx-file-upload-progress-id="file-upload-progress"
    data-rx-file-upload-max-size="314572800">

<progress id="file-upload-progress" value="0" max="100" />
<div id="file-size-error" class="error-message"></div>

<script>
    // Listen for file selection events
    document.addEventListener("rx:file-selected", (evt) => {
        if (evt.detail.fileInput.id === "file-upload-input") {
            var msg = document.getElementById("file-size-error");
            if (evt.detail.error) {
                // Show validation error
                msg.innerText = evt.detail.error.message;
                evt.detail.fileInput.setAttribute("aria-invalid", "true");
            } else {
                // Clear any errors
                msg.innerText = "";
                evt.detail.fileInput.removeAttribute("aria-invalid");
            }
        }
    });
</script>
```

```csharp
// Handler for file upload
[RequestSizeLimit(long.MaxValue)]
[RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
public static async Task<IResult> FileUpload(
    HttpContext context,
    IRxDriver rxDriver,
    IFormFile uploadedFile)
{
    // Process file
    var stream = new MemoryStream();
    await uploadedFile.CopyToAsync(stream);

    var model = new FileUploadModel(
        Guid.NewGuid().ToString(),
        uploadedFile.FileName,
        stream);

    // Update UI with uploaded file info
    return await rxDriver
        .With(context)
        .AddFragment<FileUploaded, FileUploadModel>(
            model,
            "file-upload-container",
            FragmentMergeStrategyType.Swap)
        .Render();
}

## Special Triggers

Beyond DOM events, RazorX supports automatic triggers:

### Initialized Trigger
Fires once when element enters DOM:
```html
<!-- Example from TodoExample.razor -->
<div id="content"
     data-rx-method="GET"
     data-rx-action="/search-todos"
     data-rx-trigger='{ "type": "initialized", "delay": 100 }'
     data-rx-include-state="filter">
    <!-- Content loads here after 100ms delay -->
</div>
```

This is commonly used to:
- Load initial data after page render
- Restore previous state from browser storage
- Trigger deferred content loading

### Poll Trigger
Repeats at intervals:
```html
<!-- Poll every 5 seconds -->
<div data-rx-action="/api/status"
     data-rx-trigger='{"type":"poll","interval":5000}'>
```

### Revealed Trigger
Fires when element enters viewport:
```html
<!-- When visible -->
<div data-rx-action="/api/lazy-load"
     data-rx-trigger='{"type":"revealed"}'>

<!-- With margin -->
<div data-rx-action="/api/lazy-load"
     data-rx-trigger='{"type":"revealed","margin":"200px"}'>
```

### Combining Triggers
```html
<div data-rx-action="/api/content"
     data-rx-trigger='[
         {"type":"initialized","delay":500},
         "click",
         {"type":"poll","interval":30000}
     ]'>
```

## Common Patterns

### Search with Debounce
```html
<!-- Actual implementation from TodoSearch.razor -->
<input id="search-todos"
      type="search"
      name="filter"
      value="@Model"
      data-rx-method="GET"
      data-rx-action="/search-todos"
      data-rx-debounce="400"
      data-rx-trigger="input"
      placeholder="Search"
      aria-label="Search"
      autocomplete="off">
```

```csharp
// Handler for search with state persistence
public static async Task<IResult> SearchTodos(
    HttpContext context,
    IRxDriver rxDriver,
    string filter = "")
{
    var filtered = _todos
        .Where(x => x.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x => x.Id)
        .Take(5);

    var driver = rxDriver
        .With(context)
        // Persist search filter in session and URL
        .AddTriggerSetState("filter", filter, MetadataScope.Session, updateUrl: true)
        // Update search box to reflect current filter
        .AddFragment<TodoSearch, string>(filter, "search-todos", FragmentMergeStrategyType.Morph)
        // Replace list contents with filtered results
        .AddFragment<TodoList, IEnumerable<TodoModel>>(filtered, "todo-list", FragmentMergeStrategyType.SwapInner)
        // Update count to reflect filtered results
        .AddFragment<TodoCount, (int, int)>(GetCount(), "todo-count", FragmentMergeStrategyType.Swap);

    // Show filter notification only when filter is active
    if (!string.IsNullOrWhiteSpace(filter))
    {
        driver.AddTriggerToast("Filter applied!", ToastType.Warning, 3500, ToastVerticalPosition.Top, ToastHorizontalPosition.Left);
    }

    return await driver.Render();
}

### Infinite Scroll
```razor
@* TodoItem.razor with revealed trigger for infinite scroll *@
@implements IComponentModel<TodoModel>

<article id="todo-item-@Model.Id"
    data-rx-action="@(SetRevealedTrigger ? $"/todo/next/{Model.Id}" : false)"
    data-rx-trigger="@(SetRevealedTrigger ? "{ \"type\": \"revealed\", \"margin\": \"0px 0px 300px 0px\" }" : false)"
    data-rx-include-state="filter">
    <!-- Todo content -->
    @Model.Text
</article>

@code {
    [Parameter] public TodoModel Model { get; set; } = null!;
    [Parameter] public bool SetRevealedTrigger { get; set; }
}
```

```csharp
// Handler for loading more items
public static async Task<IResult> NextTodos(
    HttpContext context,
    IRxDriver rxDriver,
    int id,
    string filter = "")
{
    // Get next page of todos before this ID
    var page = _todos
        .Where(x => x.Id < id && x.Text.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
        .OrderByDescending(x => x.Id)
        .Take(5);

    return await rxDriver
        .With(context)
        // Append new items to the end of the list
        .AddFragment<TodoList, IEnumerable<TodoModel>>(
            page,
            "todo-list",
            FragmentMergeStrategyType.AppendBeforeEnd)
        // Show notification that items loaded
        .AddTriggerToast("More todos loaded!", ToastType.Info, 3500, ToastVerticalPosition.Bottom, ToastHorizontalPosition.Right)
        .Render();
}

### Modal Forms with Loading States
```razor
@* TodoForm.razor - Reusable form component *@
@implements IComponentModel<TodoFormModel>

<form id="@(Model.IsEdit ? "edit-todo-form" : "new-todo-form")"
    method="dialog"
    data-rx-method="@(Model.IsEdit ? "PUT" : "POST")"
    data-rx-action="@(Model.IsEdit ? $"/todo/{Model.Id}" : "/todo")"
    data-rx-loading-indicator="@(Model.IsEdit ? "edit-loading" : false)"
    data-rx-disable-in-flight
    novalidate>
    <textarea
        id="todo-text-@Model.Id"
        name="Text"
        placeholder="Describe something you need to do..."
        aria-label="Description of TODO"
        aria-invalid="@(Model.HasError ? "true" : false)"
        aria-describedby="todo-help">
        @Model.Text
    </textarea>
    <small id="todo-help">Required</small>
</form>

@code {
    [Parameter] public TodoFormModel Model { get; set; } = null!;
}
```

```csharp
// Handler with loading state and validation
public static async Task<IResult> SaveTodo(
    HttpContext context,
    IRxDriver rxDriver,
    TodoFormModel model,
    int id)
{
    // Simulate processing delay
    await Task.Delay(700);

    // Validate
    if (string.IsNullOrWhiteSpace(model.Text))
    {
        return await rxDriver
            .With(context)
            .AddTriggerToast("Validation error!", ToastType.Error)
            .AddFragment<TodoForm, TodoFormModel>(
                model with { HasError = true, IsEdit = true },
                "edit-todo-form",
                FragmentMergeStrategyType.Swap)
            .Render();
    }

    var todo = _todos.FirstOrDefault(x => x.Id == id);
    if (todo == null)
    {
        return Results.NotFound();
    }

    todo.Text = model.Text;

    return await rxDriver
        .With(context)
        // Close the modal
        .AddTriggerCloseDialog("edit-todo-modal")
        // Focus back to the trigger
        .AddTriggerFocusElement($"edit-todo-trigger-{id}")
        // Show success message
        .AddTriggerToast("Todo updated successfully!", ToastType.Success)
        // Show loading indicator in form container
        .AddFragment<TodoFormLoading>("edit-todo-form-container", FragmentMergeStrategyType.SwapInner)
        // Update the todo item in the list
        .AddFragment<TodoItem, TodoModel>(todo, $"todo-item-{todo.Id}", FragmentMergeStrategyType.Swap)
        .Render();
}

### Polling for Updates
```html
<!-- Poll endpoint every 4 seconds -->
<div id="poll-test"
     data-rx-action="/poll-test"
     data-rx-trigger='{ "type": "poll", "interval": 4000 }'>
</div>

<!-- Initialize and then poll -->
<div id="dashboard"
     data-rx-action="/api/dashboard/metrics"
     data-rx-trigger='[
         {"type":"initialized"},
         {"type":"poll","interval":10000}
     ]'>
    <!-- Metrics display updates every 10 seconds -->
</div>
```

**Important Notes on Special Triggers:**
- Special triggers (initialized, poll, revealed) must use GET method
- They don't respond to debounce or disable-queueing attributes
- Poll intervals continue until element is removed from DOM
- Revealed triggers fire once when element enters viewport

### Form with Dialog Pattern
```razor
@* Modal dialog with form *@
<dialog id="new-todo-modal">
    <article>
        <h2>New Todo</h2>
        <TodoForm Model="@(new TodoFormModel())" />
        <footer>
            <!-- Cancel button resets form via AJAX -->
            <button id="new-todo-modal-close"
                    type="button"
                    class="secondary"
                    data-rx-method="GET"
                    data-rx-action="/new-todo-reset">
                Cancel
            </button>
            <!-- Submit button submits the form -->
            <button id="new-todo-save" form="new-todo-form">Save</button>
        </footer>
    </article>
</dialog>

<script>
    // Handle modal open/close
    (function(){
        const modal = document.getElementById("new-todo-modal");
        const trigger = document.getElementById("new-todo-modal-trigger");
        const dismiss = document.getElementById("new-todo-modal-close");

        trigger.onclick = () => modal.showModal();
        dismiss.onclick = () => modal.close();
    })()
</script>
```

## Request Lifecycle

1. **Element Configuration**: Element with `data-rx-action` is initialized
2. **Trigger Fires**: User interaction or special trigger activates
3. **Request Building**:
   - Collect form data (if applicable)
   - Include state from `data-rx-include-state`
   - Add CSRF token from cookie
   - Set `rx-request` header
4. **Request Execution**:
   - Show loading indicator
   - Disable element (if configured)
   - Send AJAX request
5. **Response Processing**:
   - Parse `rx-merge` header for fragments
   - Update DOM based on merge strategies
   - Execute triggers (toast, focus, state, etc.)
6. **Cleanup**:
   - Hide loading indicator
   - Re-enable element
   - Dispatch completion events

## Error Handling

RazorX distinguishes between regular navigation and AJAX requests for error handling:

```csharp
// In Program.cs - Configure error handling
app.UseExceptionHandler(handler => {
    handler.Run(context => {
        // Check if this is an AJAX request
        IResult result = context.Request.IsRxRequest()
            ? TypedResults.Accepted("/error?code=500")  // Returns 202 with location header
            : TypedResults.Redirect("/error?code=500");  // Regular redirect
        return result.ExecuteAsync(context);
    });
});

// In handler - Return error for missing resource
public static async Task<IResult> EditTodo(
    HttpContext context,
    IRxDriver rxDriver,
    int id)
{
    var todo = _todos.FirstOrDefault(x => x.Id == id);
    if (todo == null)
    {
        // For AJAX requests, return 202 Accepted with location header
        // Client will navigate to error page
        return TypedResults.Accepted("/error?code=404");
    }

    // Process normally...
}
```

For validation errors in AJAX requests, use the response builder:

```csharp
if (string.IsNullOrWhiteSpace(model.Text))
{
    return await rxDriver
        .With(context)
        .AddTriggerToast("Validation error!", ToastType.Error)
        .AddFragment<TodoForm, TodoFormModel>(
            model with { HasError = true },
            "todo-form",
            FragmentMergeStrategyType.Swap)
        .Render();
}

## No-Content Responses

For actions without UI updates (triggers only):

```csharp
// Example: Poll endpoint that just checks status
public static IResult PollTest()
{
    // Return 204 No Content - no UI updates needed
    return TypedResults.NoContent();
}

// Or use response builder with triggers only
public static async Task<IResult> SaveDraft(
    HttpContext context,
    IRxDriver rxDriver,
    DraftModel model)
{
    // Save draft...

    // Return 204 with only triggers, no fragments
    return await rxDriver
        .With(context)
        .AddTriggerToast("Draft saved", ToastType.Info)
        .AddTriggerSetState("draftId", model.Id, MetadataScope.Session)
        .Render();
}

## View Transitions API

RazorX automatically uses the View Transitions API when available:

```csharp
// Smooth morphing with browser-native transitions
.AddFragment<Grid, Model>(model, "grid", FragmentMergeStrategyType.Morph)
```

The framework detects `document.startViewTransition` and wraps updates appropriately.
