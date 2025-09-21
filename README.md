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
        razorx.init();
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

### Step 2: Create a Page Component

Create a page component that will be rendered within the layout:

```razor
@* Components/Home/HomePage.razor *@
@implements IComponentModel<HomeModel>

<main id="main">
    <h1>@Model.Title</h1>
    <p>@Model.Message</p>
    
    <button data-rx-action="/api/update"
            data-rx-method="POST"
            data-rx-trigger="click">
        Update Message
    </button>
</main>

@code {
    [Parameter] public HomeModel Model { get; set; } = null!;
}
```

### Step 3: Create a Request Handler

Create a handler that serves both full page requests and fragment updates:

```csharp
using RazorX.Framework;

// Model definition
public record HomeModel(string Title, string Message);

public class HomeHandler : RequestHandler
{
    public override void MapRoutes(IEndpointRouteBuilder router)
    {
        router.MapGet("/", GetHomePage);
        router.MapPost("/api/update", UpdateMessage);
    }
    
    // Full page render (initial load or direct navigation)
    public static async Task<IResult> GetHomePage(IRxDriver rxDriver, HttpContext context)
    {
        var model = new HomeModel("Welcome", "Click the button to update this message");
        
        // RenderPage uses the IRootComponent layout for full page renders
        return await rxDriver.RenderPage<App, HomePage, HomeModel>(
            context, 
            model, 
            "RazorX - Home"
        );
    }
    
    // Fragment update (AJAX request from client)
    public static async Task<IResult> UpdateMessage(IRxDriver rxDriver, HttpContext context)
    {
        var model = new HomeModel("Welcome", $"Updated at {DateTime.Now:T}");
        
        // Fragment responses update specific parts of the page
        return await rxDriver
            .With(context)
            .AddFragment<HomePage, HomeModel>(model, "main", FragmentMergeStrategyType.Morph)
            .AddTriggerToast("Message updated!", ToastType.Success)
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

### Notes

- **Templating**: RazorX uses Razor Components (.razor files) exclusively. Traditional Razor Pages (.cshtml) are not supported.
  
- **Routing**: RazorX uses the `RequestHandler` pattern with minimal APIs by default. Traditional MVC controllers can coexist or be used instead - simply add `services.AddControllers()` and `app.MapControllers()`. Controllers can return Razor Components using the same `IRxDriver` methods.

- **Request Detection**: RazorX automatically detects whether to return a full page or fragment based on the presence of the "rx-request" header, which is added by the client library for AJAX requests.

- **Element IDs**: Any element with a `data-rx-action` attribute must have a unique ID for proper request tracking.
# Framework Mechanics

## Overview

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
public static async Task<IResult> GetHomePage(
    HttpContext context,
    IRxDriver driver)
{
    var model = new HomePageModel {
        Title = "Welcome",
        Items = await LoadItemsAsync()
    };

    // Renders complete HTML page with layout
    return await driver.RenderPage<RootLayout, HomePage, HomePageModel>(
        context,
        model,
        title: "Home"
    );
}

// With custom head content
return await driver.RenderPage<RootLayout, CustomHead, HomePage, HomePageModel>(
    context,
    model,
    title: "Home"
);

// Without a model
return await driver.RenderPage<RootLayout, AboutPage>(
    context,
    title: "About"
);
```

### Response Builder Pattern

The response builder provides a fluent API for composing AJAX responses with fragments and triggers:

```csharp
public static async Task<IResult> HandleAction(
    HttpContext context,
    IRxDriver driver,
    UpdateRequest request)
{
    // Start building response
    return await driver
        .With(context)

        // Add fragments to update DOM
        .AddFragment<ItemList, ItemListModel>(
            model: new ItemListModel { Items = items },
            targetId: "item-list",
            fragmentMergeStrategy: FragmentMergeStrategyType.Morph
        )

        // Add notification fragment
        .AddFragment<NotificationBanner>(
            targetId: "notifications",
            fragmentMergeStrategy: FragmentMergeStrategyType.SwapInner
        )

        // Trigger side effects
        .AddTriggerToast("Items updated successfully", ToastType.Success)
        .AddTriggerFocusElement("search-input", positionCursorEnd: true)
        .AddTriggerSetState("lastUpdate", DateTime.Now.ToString(), MetadataScope.Session)

        // Render the response
        .Render(ignoreActiveElementValueOnMorph: true);
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
Transfer action to another element:
```html
<tr data-rx-action="/api/items/1"
    data-rx-trigger="click"
    data-rx-delegate-action-to="row-menu">
    <td>Item 1</td>
</tr>

<div id="row-menu">
    <!-- Receives the delegated action -->
</div>
```

### File Upload Attributes

For file inputs only:

```html
<input type="file"
       name="documents"
       data-rx-action="/api/upload"
       data-rx-file-upload-max-size="5242880"
       data-rx-file-upload-timeout="60000"
       data-rx-file-upload-progress-id="upload-progress">

<progress id="upload-progress" max="100" value="0"></progress>
```

## Special Triggers

Beyond DOM events, RazorX supports automatic triggers:

### Initialized Trigger
Fires once when element enters DOM:
```html
<!-- Immediate -->
<div data-rx-action="/api/data"
     data-rx-trigger='{"type":"initialized"}'>

<!-- With delay -->
<div data-rx-action="/api/data"
     data-rx-trigger='{"type":"initialized","delay":1000}'>
```

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
<input type="search"
       placeholder="Search products..."
       data-rx-action="/api/search"
       data-rx-trigger="input"
       data-rx-debounce="300"
       data-rx-loading-indicator="search-spinner">
```

### Infinite Scroll
```html
<div class="scroll-container">
    <div id="items">...</div>

    <div data-rx-action="/api/items/next"
         data-rx-trigger='{"type":"revealed","margin":"100px"}'
         data-rx-include-state="page">
        Loading more...
    </div>
</div>
```

### Modal Form
```html
<dialog id="edit-modal">
    <form data-rx-action="/api/items/update"
          data-rx-method="PUT"
          data-rx-disable-in-flight>
        <!-- Form fields -->
        <button type="submit">Save</button>
    </form>
</dialog>
```

```csharp
// Server response after save
return await driver
    .With(context)
    .AddFragment<ItemRow, Item>(updatedItem, $"item-{item.Id}",
                                 FragmentMergeStrategyType.Morph)
    .AddTriggerCloseDialog("edit-modal", resetFormId: "edit-form")
    .AddTriggerToast("Item updated", ToastType.Success)
    .Render();
```

### Real-time Dashboard
```html
<div id="dashboard"
     data-rx-action="/api/dashboard/metrics"
     data-rx-trigger='[
         {"type":"initialized"},
         {"type":"poll","interval":10000}
     ]'>
    <!-- Metrics display -->
</div>
```

### Progressive Enhancement
```html
<!-- Works without JavaScript -->
<form action="/search" method="get">
    <input name="q"
           data-rx-action="/api/search"
           data-rx-trigger="input"
           data-rx-debounce="300">
    <button type="submit">Search</button>
</form>

<div id="search-results">
    <!-- Results render here -->
</div>
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

Errors (4xx/5xx responses) replace the entire page content:

```csharp
// Custom error response
if (!isValid) {
    return Results.BadRequest(new {
        error = "Validation failed",
        fields = validationErrors
    });
}

// Framework displays as formatted JSON or text
```

## No-Content Responses

For actions without UI updates:

```csharp
return await driver
    .With(context)
    .AddTriggerToast("Saved to drafts", ToastType.Info)
    .Render();  // Returns 204 with triggers only
```

## View Transitions API

RazorX automatically uses the View Transitions API when available:

```csharp
// Smooth morphing with browser-native transitions
.AddFragment<Grid, Model>(model, "grid", FragmentMergeStrategyType.Morph)
```

The framework detects `document.startViewTransition` and wraps updates appropriately.