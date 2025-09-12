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
