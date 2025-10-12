# RazorX.Framework

A Server-Driven User Interface (SDUI) framework for ASP.NET Core that implements HATEOAS principles through HTML-over-the-wire. RazorX uses Razor components for server-side HTML templating and a TypeScript client library for intelligent DOM manipulation.

## Table of Contents

- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [ID-Based Architecture](#id-based-architecture)
- [Installation](#installation)
- [Setup and Configuration](#setup-and-configuration)
- [Building Your First Feature](#building-your-first-feature)
- [Common Patterns](#common-patterns)
- [Client Attributes Reference](#client-attributes-reference)
- [Server API Reference](#server-api-reference)
- [Advanced Features](#advanced-features)
  - [Server-Sent Events](#server-sent-events)
  - [Multi-Client Broadcasting](#multi-client-broadcasting)
- [AOT Compilation](#aot-compilation)
- [Troubleshooting](#troubleshooting)
- [Requirements](#requirements)

---

## Quick Start

Get running in 5 minutes with a complete counter app.

### 1. Install the Package

```bash
dotnet new web -n MyRazorXApp
cd MyRazorXApp
dotnet add package RazorX.Framework
```

### 2. Create the File Structure

```
MyRazorXApp/
├── Program.cs
├── Components/
│   ├── Layout/
│   │   └── App.razor
│   ├── Counter/
│   │   ├── CounterHandler.cs
│   │   └── CounterPage.razor
```

### 3. Configure Services (Program.cs)

```csharp
using RazorX.Framework;

var builder = WebApplication.CreateBuilder(args);

// Required services
builder.Services.AddRxDriver();
builder.Services.AddAntiforgery();
builder.Services.AddRxAntiforgery();

var app = builder.Build();

// Middleware
app.UseStaticFiles();
app.UseAntiforgery();
app.UseRxAntiforgeryCookie();

// Register handlers
var routes = app.MapGroup(string.Empty);
new Components.Counter.CounterHandler().MapRoutes(routes);

app.Run();
```

### 4. Create Layout (Components/Layout/App.razor)

```razor
@using RazorX.Framework
@implements IRootComponent

<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>My RazorX App</title>
    <link rel="stylesheet" href="/css/razorx.css" />

    @if (HeadContent is not null)
    {
        <DynamicComponent Type="HeadContent" Parameters="HeadContentParameters" />
    }
</head>
<body>
    <DynamicComponent Type="MainContent" Parameters="MainContentParameters" />

    <script type="module">
        import { razorx } from '/js/razorx.js';
        razorx.init({
            addCookieToRequestHeader: 'RequestVerificationToken'
        });
    </script>
</body>
</html>

@code {
    [Parameter] public Type? HeadContent { get; set; }
    [Parameter] public Dictionary<string, object?> HeadContentParameters { get; set; } = [];
    [Parameter] public Type MainContent { get; set; } = null!;
    [Parameter] public Dictionary<string, object?> MainContentParameters { get; set; } = [];
}
```

### 5. Create Counter Page (Components/Counter/CounterPage.razor)

```razor
@using RazorX.Framework

<h1>Counter</h1>

<div id="counter-display">
    <p>Current count: <strong>0</strong></p>
</div>

<button id="increment-btn"
        data-rx-action="/counter/increment"
        data-rx-method="POST"
        data-rx-trigger="click"
        data-rx-disable-in-flight>
    Increment
</button>
```

### 6. Create Counter Handler (Components/Counter/CounterHandler.cs)

```csharp
using RazorX.Framework;

namespace MyRazorXApp.Components.Counter;

public class CounterHandler : RequestHandler
{
    private static int _count = 0;

    public override void MapRoutes(IEndpointRouteBuilder router)
    {
        router.MapGet("/", GetCounter);
        router.MapPost("/counter/increment", IncrementCounter);
    }

    public static async Task<IResult> GetCounter(
        HttpContext context,
        IRxDriver rxDriver)
    {
        return await rxDriver.RenderPage<
            MyRazorXApp.Components.Layout.App,
            CounterPage>(context);
    }

    public static async Task<IResult> IncrementCounter(
        HttpContext context,
        IRxDriver rxDriver)
    {
        _count++;

        return await rxDriver
            .With(context)
            .AddFragment<CounterDisplay, int>(
                _count,
                "counter-display",
                FragmentMergeStrategyType.Swap)
            .AddTriggerToast($"Count is now {_count}!", ToastType.Success)
            .Render();
    }
}
```

### 7. Create Counter Display Component (Components/Counter/CounterDisplay.razor)

```razor
@using RazorX.Framework
@implements IComponentModel<int>

<div id="counter-display">
    <p>Current count: <strong>@Model</strong></p>
</div>

@code {
    [Parameter] public int Model { get; set; }
}
```

### 8. Run the App

```bash
dotnet run
```

Visit `http://localhost:<PORT>` (echoed in the terminal) and click the "Increment" button. The counter updates without a full page reload!

**What just happened?**

1. User clicks "Increment" button
2. Client sends POST to `/counter/increment`
3. Server increments counter and renders `CounterDisplay` component with new value
4. Server responds with HTML fragment + merge instruction
5. Client replaces `#counter-display` with new HTML
6. Toast notification appears

This is HTML-over-the-wire in action!

---

## Core Concepts

### What is RazorX?

RazorX is a hypermedia-driven framework that brings together:

- **ASP.NET Core** - Familiar routing, dependency injection, and middleware
- **Razor Components** - Server-side HTML templating (NOT Blazor)
- **HTML-over-the-wire** - Server sends HTML, not JSON
- **HATEOAS** - Server controls client behavior through hypermedia

### This is NOT Blazor

RazorX uses Razor components as a **templating engine** for server-side HTML generation. It is not Blazor and does not use the Blazor runtime, SignalR, or client-side rendering.

**How RazorX differs:**
- Pure HTTP requests (not WebSockets)
- Server renders HTML and sends it to the client
- Client applies HTML to DOM using standard web APIs
- No interactive component model or event system
- No .NET runtime in the browser

### The Paradigm Shift

**Traditional SPA:**
```
Client ← JSON ← Server
  ↓
Client renders JSON into HTML
Client manages state
Client handles routing
```

**RazorX (HTML-over-the-wire):**
```
Client ← HTML ← Server
  ↓
Client applies HTML to DOM
Server manages state
Server controls behavior
```

**Benefits:**
- Simpler mental model (server controls everything)
- No state synchronization bugs
- Faster development (no duplicate logic)
- Better SEO (server-rendered HTML)

### Request Lifecycle

1. **User interaction** - User clicks button with `data-rx-action="/api/users"`
2. **HTTP request** - Client sends request to server
3. **Server processing** - Handler processes request, updates state
4. **Render component** - Server renders Razor component to HTML
5. **Response** - Server sends HTML + instructions (merge strategy, triggers)
6. **DOM update** - Client applies HTML to DOM using specified strategy
7. **Visual feedback** - Toasts, focus changes, etc.

### RenderPage vs AddFragment

**RenderPage** - Full page rendering:
```csharp
// Use for: Initial page loads, navigation, complete page refreshes
return await rxDriver.RenderPage<App, ProductPage, ProductModel>(
    context,
    product
);
```
- Returns complete HTML document
- Used when browser navigates to new URL
- Includes layout/root component
- Typically used in GET handlers

**AddFragment** - Partial DOM updates:
```csharp
// Use for: Button clicks, form submissions, real-time updates
return await rxDriver
    .With(context)
    .AddFragment<CartCount, int>(count, "cart-counter")
    .Render();
```
- Returns HTML fragment(s)
- URL stays same (unless you update it)
- Updates specific DOM elements
- Typically used in POST/PUT/DELETE handlers

**Rule of thumb:** Full page navigation? Use `RenderPage`. Everything else? Use `AddFragment`.

### ID-Based Architecture

RazorX uses **ID-based targeting** for all DOM manipulation. This is a fundamental design decision.

**How it works:**
```csharp
.AddFragment<Component>("user-123", ...)  // Targets element with id="user-123"
.RemoveElement("notification-5")           // Removes element with id="notification-5"
```

**ID requirements:**
- Elements with `data-rx-action` **must** have an `id` attribute (enforced at runtime)
- Elements being targeted by `AddFragment()` must have matching IDs
- Elements referenced by triggers (focus, loading indicators) must have IDs

**This differs from frameworks like htmx:**
- **RazorX:** ID-only targeting (`document.getElementById()`)
- **htmx:** CSS selector support (`.class`, `[data-attr]`, etc.)

**Design rationale:**
- **Performance:** `getElementById()` is fastest DOM lookup (O(1) hash table)
- **Clarity:** IDs are unique, unambiguous targets
- **Simplicity:** No selector parsing or matching logic

**Example - fragment must match target:**
```csharp
// Server
.AddFragment<UserCard, UserModel>(user, "user-123", ...)

// Component HTML - ID must match
<div id="user-123">...</div>  // ✅ Correct
<div id="user-card">...</div>  // ❌ Wrong - won't update
<div class="user-123">...</div>  // ❌ Wrong - no ID
```

### Component Types

**Root Component** (`IRootComponent`) - Page layout:
```razor
@implements IRootComponent
<!DOCTYPE html>
<html>
<head>...</head>
<body>
    <DynamicComponent Type="MainContent" Parameters="MainContentParameters" />
</body>
</html>
```
- Defines HTML document structure
- Contains `<html>`, `<head>`, `<body>` tags
- References razorx.js script
- Usually one per application

**Page Components** - Main content:
```razor
<h1>Product Page</h1>
<div id="product-details">
    <!-- Page content -->
</div>
```
- Implements `IComponentModel<T>` if it has a model
- Rendered inside root component's `DynamicComponent`
- May include `data-rx-*` attributes for interactivity

**Fragment Components** - Reusable pieces:
```razor
@implements IComponentModel<ProductModel>

<div id="product-card-@Model.Id">
    <h2>@Model.Name</h2>
    <p>@Model.Price</p>
</div>

@code {
    [Parameter] public ProductModel Model { get; set; } = null!;
}
```
- Small, focused components
- Implement `IComponentModel<T>` to receive data
- Used with `AddFragment<T, TModel>()`
- Root element must have ID matching the `targetId` parameter

---

## Installation

### NuGet Package

```bash
dotnet add package RazorX.Framework
```

Or add to your `.csproj`:

```xml
<PackageReference Include="RazorX.Framework" Version="1.0.0" />
```

### What Gets Installed

The package automatically copies client files to your project during build:

- `wwwroot/js/razorx.js` - Client library
- `wwwroot/js/razorx.js.map` - Source map
- `wwwroot/js/razorx.d.ts` - TypeScript definitions
- `wwwroot/css/razorx.css` - Default toast styles

These files are copied to your `wwwroot` folder and served via `app.UseStaticFiles()`.

---

## Setup and Configuration

### Minimal Setup

```csharp
using RazorX.Framework;

var builder = WebApplication.CreateBuilder(args);

// Required services
builder.Services.AddRxDriver();
builder.Services.AddAntiforgery();
builder.Services.AddRxAntiforgery();

var app = builder.Build();

// Middleware
app.UseStaticFiles();
app.UseAntiforgery();
app.UseRxAntiforgeryCookie();

// Register handlers (manual registration - recommended)
var routes = app.MapGroup(string.Empty);
new HomeHandler().MapRoutes(routes);
new UserHandler().MapRoutes(routes);

app.Run();
```

### Configuration Options

#### RxDriver Options

```csharp
builder.Services.AddRxDriver(options => {
    // Disable JSON encoding if you prefer form-encoded payloads
    options.AddJsonConverters = false;
});
```

**Both encoding methods are fully supported.** The choice affects ASP.NET model binding, not framework capability.

**JSON encoding (default: enabled):**
- Better support in ASP.NET minimal APIs (automatic model binding)
- Complex types bind directly to parameters
- Recommended for most applications

**Form encoding (when disabled):**
- Traditional `application/x-www-form-urlencoded`
- Works fine, but may require manual binding for complex types
- Use if integrating with existing form-based systems

Both methods support checkboxes, arrays, multi-select, and all HTML form inputs equally well.

#### Antiforgery Options

```csharp
builder.Services.AddRxAntiforgery(options => {
    // Customize CSRF cookie name
    options.RequestVerificationTokenCookieName = "MyApp-CSRF-Token";
});
```

### Route Registration

**Option 1: Manual Registration (Recommended for AOT)**

```csharp
var routes = app.MapGroup(string.Empty);
new HomeHandler().MapRoutes(routes);
new UserHandler().MapRoutes(routes);
new ProductHandler().MapRoutes(routes);
```

**Option 2: Automatic Discovery (Uses Assembly Scanning)**

```csharp
app.MapGroup(string.Empty).MapRoutes();
```

This automatically discovers all `RequestHandler` subclasses. For AOT compilation, use manual registration or add `<TrimmerRootAssembly Include="YourAppName" />` to your `.csproj`.

### Client Initialization

In your root layout (`App.razor`):

```html
<script type="module">
    import { razorx } from '/js/razorx.js';
    razorx.init({
        // Required: Include CSRF token in requests
        addCookieToRequestHeader: 'RequestVerificationToken',

        // Optional: Encode forms as JSON (default: true)
        encodeRequestFormDataAsJson: true,

        // Optional: Customize loading indicator classes
        loadingIndicatorClasses: {
            hidden: 'my-hidden-class',
            visible: 'my-visible-class'
        }
    });
</script>
```

---

## Building Your First Feature

Let's build a todo list with create, complete, and delete functionality. This tutorial demonstrates the complete request cycle.

### Step 1: Define the Model

```csharp
// Models/TodoItem.cs
namespace MyApp.Models;

public record TodoItem(int Id, string Title, bool IsCompleted);
```

### Step 2: Create the Handler

```csharp
// Components/Todos/TodoHandler.cs
using RazorX.Framework;
using MyApp.Models;

namespace MyApp.Components.Todos;

public class TodoHandler : RequestHandler
{
    // In-memory storage (use a database in production)
    private static List<TodoItem> _todos = new()
    {
        new(1, "Learn RazorX", false),
        new(2, "Build an app", false)
    };
    private static int _nextId = 3;

    public override void MapRoutes(IEndpointRouteBuilder router)
    {
        router.MapGet("/todos", GetTodos);
        router.MapPost("/todos", CreateTodo);
        router.MapPost("/todos/{id}/complete", CompleteTodo);
        router.MapDelete("/todos/{id}", DeleteTodo);
    }

    public static async Task<IResult> GetTodos(
        HttpContext context,
        IRxDriver rxDriver)
    {
        return await rxDriver.RenderPage<
            MyApp.Components.Layout.App,
            TodoListPage,
            List<TodoItem>>(
                context,
                _todos
            );
    }

    public static async Task<IResult> CreateTodo(
        HttpContext context,
        IRxDriver rxDriver,
        string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return await rxDriver
                .With(context)
                .AddTriggerToast("Title is required", ToastType.Error)
                .Render();
        }

        var todo = new TodoItem(_nextId++, title, false);
        _todos.Add(todo);

        return await rxDriver
            .With(context)
            .AddFragment<TodoCard, TodoItem>(
                todo,
                "todo-list",
                FragmentMergeStrategyType.AppendAfterBegin)
            .AddTriggerToast("Todo added!", ToastType.Success)
            .AddTriggerFocusElement("new-todo-input")
            .Render();
    }

    public static async Task<IResult> CompleteTodo(
        HttpContext context,
        IRxDriver rxDriver,
        int id)
    {
        var todo = _todos.FirstOrDefault(t => t.Id == id);
        if (todo == null)
        {
            return await rxDriver
                .With(context)
                .AddTriggerToast("Todo not found", ToastType.Error)
                .Render();
        }

        var updated = todo with { IsCompleted = true };
        _todos[_todos.FindIndex(t => t.Id == id)] = updated;

        return await rxDriver
            .With(context)
            .AddFragment<TodoCard, TodoItem>(
                updated,
                $"todo-{id}",
                FragmentMergeStrategyType.Morph)
            .AddTriggerToast("Todo completed!", ToastType.Success)
            .Render();
    }

    public static async Task<IResult> DeleteTodo(
        HttpContext context,
        IRxDriver rxDriver,
        int id)
    {
        _todos.RemoveAll(t => t.Id == id);

        return await rxDriver
            .With(context)
            .RemoveElement($"todo-{id}")
            .AddTriggerToast("Todo deleted", ToastType.Info)
            .Render();
    }
}
```

### Step 3: Create the Page Component

```razor
@* Components/Todos/TodoListPage.razor *@
@using RazorX.Framework
@using MyApp.Models
@implements IComponentModel<List<TodoItem>>

<h1>My Todos</h1>

<form id="new-todo-form"
      data-rx-action="/todos"
      data-rx-method="POST"
      data-rx-trigger="submit">
    <input type="text"
           id="new-todo-input"
           name="title"
           placeholder="What needs to be done?"
           required />
    <button type="submit">Add</button>
</form>

<div id="todo-list">
    @foreach (var todo in Model)
    {
        <TodoCard Model="todo" />
    }
</div>

@if (!Model.Any())
{
    <p>No todos yet. Add one above!</p>
}

@code {
    [Parameter] public List<TodoItem> Model { get; set; } = null!;
}
```

### Step 4: Create the Card Component

```razor
@* Components/Todos/TodoCard.razor *@
@using RazorX.Framework
@using MyApp.Models
@implements IComponentModel<TodoItem>

<div id="todo-@Model.Id" class="todo-card @(Model.IsCompleted ? "completed" : "")">
    <input type="checkbox"
           checked="@Model.IsCompleted"
           data-rx-action="/todos/@Model.Id/complete"
           data-rx-method="POST"
           data-rx-trigger="change"
           data-rx-allow-event-default
           disabled="@Model.IsCompleted" />

    <span>@Model.Title</span>

    <button data-rx-action="/todos/@Model.Id"
            data-rx-method="DELETE"
            data-rx-trigger="click">
        Delete
    </button>
</div>

@code {
    [Parameter] public TodoItem Model { get; set; } = null!;
}
```

### Step 5: Register and Run

```csharp
// In Program.cs
var routes = app.MapGroup(string.Empty);
new TodoHandler().MapRoutes(routes);
```

**Try it out:**
1. Visit `/todos`
2. Add a todo - It appears at the top of the list
3. Check the checkbox - Visual update without reload
4. Delete a todo - Smoothly removed from DOM

### Key Patterns Demonstrated

1. **Full page load** (`GetTodos`) - Uses `RenderPage`
2. **Create** (`CreateTodo`) - Appends new fragment to list
3. **Update** (`CompleteTodo`) - Morphs existing element
4. **Delete** (`DeleteTodo`) - Removes element from DOM
5. **Toast notifications** - Visual feedback
6. **Focus management** - Returns focus to input after add
7. **Form reset** - Form clears after submission (browser default)

---

## Common Patterns

### CRUD Operations

#### Create - Add to List

```csharp
public static async Task<IResult> CreateUser(
    HttpContext context,
    IRxDriver rxDriver,
    string name,
    string email)
{
    var user = new UserModel(Guid.NewGuid(), name, email);
    _users.Add(user);  // Your storage mechanism

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(
            user,
            "user-list",
            FragmentMergeStrategyType.AppendAfterBegin)
        .AddTriggerToast("User created", ToastType.Success)
        .Render();
}
```

**HTML:**
```html
<div id="user-list">
    <!-- New user card inserted here -->
</div>
```

#### Read - Display Details

```csharp
public static async Task<IResult> GetUser(
    HttpContext context,
    IRxDriver rxDriver,
    Guid id)
{
    var user = _users.FirstOrDefault(u => u.Id == id);

    if (user == null)
    {
        return await rxDriver
            .With(context)
            .AddFragment<NotFound>("content", FragmentMergeStrategyType.Swap)
            .AddTriggerToast("User not found", ToastType.Error)
            .Render();
    }

    return await rxDriver.RenderPage<App, UserDetailsPage, UserModel>(
        context,
        user
    );
}
```

#### Update - Modify Existing

```csharp
public static async Task<IResult> UpdateUser(
    HttpContext context,
    IRxDriver rxDriver,
    Guid id,
    string name,
    string email)
{
    var index = _users.FindIndex(u => u.Id == id);
    if (index == -1)
    {
        return await rxDriver
            .With(context)
            .AddTriggerToast("User not found", ToastType.Error)
            .Render();
    }

    var updated = new UserModel(id, name, email);
    _users[index] = updated;

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(
            updated,
            $"user-{id}",
            FragmentMergeStrategyType.Morph)  // Smooth update
        .AddTriggerToast("User updated", ToastType.Success)
        .Render();
}
```

#### Delete - Remove from DOM

```csharp
public static async Task<IResult> DeleteUser(
    HttpContext context,
    IRxDriver rxDriver,
    Guid id)
{
    _users.RemoveAll(u => u.Id == id);

    return await rxDriver
        .With(context)
        .RemoveElement($"user-{id}")
        .AddTriggerToast("User deleted", ToastType.Info)
        .Render();
}
```

### Real-Time Search

```html
<input type="search"
       name="query"
       data-rx-action="/search"
       data-rx-trigger="input"
       data-rx-debounce="300"
       data-rx-disable-queueing
       placeholder="Search...">

<div id="search-results">
  <!-- Server updates this with results -->
</div>
```

```csharp
public static async Task<IResult> Search(
    HttpContext context,
    IRxDriver rxDriver,
    string? query)
{
    var results = string.IsNullOrWhiteSpace(query)
        ? new List<Product>()
        : _products.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                   .ToList();

    return await rxDriver
        .With(context)
        .AddFragment<SearchResults, List<Product>>(
            results,
            "search-results",
            FragmentMergeStrategyType.SwapInner)
        .Render();
}
```

**Key attributes:**
- `data-rx-debounce="300"` - Wait 300ms after typing stops
- `data-rx-disable-queueing` - Don't wait for previous requests

### Infinite Scroll

```html
<div id="items-container">
  @foreach (var item in Model.Items)
  {
      <ProductCard Model="item" />
  }
</div>

<!-- Sentinel element triggers load when visible -->
<div id="load-more"
     data-rx-action="/products?page=@(Model.Page + 1)"
     data-rx-trigger='{"type":"revealed","margin":"200px"}'>
  Loading more...
</div>
```

```csharp
public static async Task<IResult> GetProducts(
    HttpContext context,
    IRxDriver rxDriver,
    int page = 1,
    int pageSize = 20)
{
    var products = _products
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToList();

    var builder = rxDriver.With(context);

    // Add each product to the list
    foreach (var product in products)
    {
        builder.AddFragment<ProductCard, Product>(
            product,
            "items-container",
            FragmentMergeStrategyType.AppendBeforeEnd);
    }

    // Remove or update the sentinel
    if (products.Count < pageSize)
    {
        // No more items
        builder.RemoveElement("load-more");
    }
    else
    {
        // Update sentinel for next page
        builder.AddFragment<LoadMoreSentinel, int>(
            page + 1,
            "load-more",
            FragmentMergeStrategyType.Swap);
    }

    return await builder.Render();
}
```

### Auto-Save Form

```html
<form id="draft-form">
  <textarea name="content"
            data-rx-action="/drafts/save"
            data-rx-method="POST"
            data-rx-trigger="input"
            data-rx-debounce="2000"
            data-rx-loading-indicator="save-status">
    @Model.Content
  </textarea>

  <span id="save-status" class="rx-loading-hidden">Saving...</span>
</form>
```

```csharp
public static async Task<IResult> SaveDraft(
    HttpContext context,
    IRxDriver rxDriver,
    string content)
{
    // Save to database
    await _db.SaveDraftAsync(content);

    return await rxDriver
        .With(context)
        .AddTriggerSetState("last-save", DateTime.UtcNow.ToString("o"), MetadataScope.Session)
        .Render();
}
```

### Modal Form Submission

```html
<dialog id="edit-dialog">
  <form id="edit-form"
        data-rx-action="/users/@Model.Id"
        data-rx-method="PUT"
        data-rx-disable-in-flight
        data-rx-loading-indicator="submit-spinner">
    <input name="name" value="@Model.Name" />
    <input name="email" value="@Model.Email" />
  </form>

  <footer>
    <button type="button" onclick="this.closest('dialog').close()">
      Cancel
    </button>
    <button data-rx-delegate-action-to="edit-form">
      Save
      <span id="submit-spinner" class="rx-loading-hidden">⏳</span>
    </button>
  </footer>
</dialog>
```

```csharp
public static async Task<IResult> UpdateUser(
    HttpContext context,
    IRxDriver rxDriver,
    Guid id,
    string name,
    string email)
{
    var updated = new UserModel(id, name, email);
    // Save to database

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(
            updated,
            $"user-{id}",
            FragmentMergeStrategyType.Morph)
        .AddTriggerCloseDialog("edit-dialog", resetFormId: "edit-form")
        .AddTriggerToast("User updated", ToastType.Success)
        .Render();
}
```

### Master-Detail with State Management

```html
<!-- Product list -->
<div id="product-list">
  @foreach (var product in Model.Products)
  {
      <button data-rx-action="/products/@product.Id"
              data-rx-trigger="click"
              data-rx-include-state='["selected-category"]'>
          @product.Name
      </button>
  }
</div>

<!-- Detail panel -->
<div id="product-details">
  <!-- Selected product details appear here -->
</div>
```

```csharp
public static async Task<IResult> GetProductDetails(
    HttpContext context,
    IRxDriver rxDriver,
    Guid id,
    string? selectedCategory = null)  // From state
{
    var product = _products.FirstOrDefault(p => p.Id == id);

    return await rxDriver
        .With(context)
        .AddFragment<ProductDetails, Product>(
            product,
            "product-details",
            FragmentMergeStrategyType.Swap)
        .AddTriggerSetState("selected-product", id.ToString(), MetadataScope.Session, updateUrl: true)
        .Render();
}
```

---

## Client Attributes Reference

### Request Configuration

#### data-rx-action

Specifies the URL to send the request to.

**Type:** `string` (URL or path)

**Required:** Yes (for elements with triggers)

**Examples:**
```html
<!-- Relative path -->
<button data-rx-action="/api/users">Load Users</button>

<!-- Absolute URL -->
<button data-rx-action="https://api.example.com/data">External</button>

<!-- Dynamic (from Razor) -->
<button data-rx-action="/api/users/@userId">Load User</button>
```

#### data-rx-method

Specifies the HTTP method for the request.

**Type:** `string`

**Valid Values:** `GET`, `POST`, `PUT`, `PATCH`, `DELETE`

**Default:**
- Forms (or elements inside forms): `POST`
- All other elements: `GET`

**Examples:**
```html
<!-- Explicit DELETE -->
<button data-rx-action="/api/users/123" data-rx-method="DELETE">
  Delete
</button>

<!-- PUT for updates -->
<form data-rx-action="/api/users/123" data-rx-method="PUT">
    <input name="name">
    <button>Update</button>
</form>
```

#### data-rx-trigger

Defines when the request should be sent.

**Type:** `string` (event name) or `JSON` (special trigger)

**Default:** `click` for buttons, `submit` for forms

**DOM Event Triggers:**

```html
<!-- Click (default for buttons) -->
<button data-rx-action="/click" data-rx-trigger="click">

<!-- Input event (every keystroke) -->
<input data-rx-action="/search" data-rx-trigger="input">

<!-- Change event (on blur/selection) -->
<select data-rx-action="/filter" data-rx-trigger="change">

<!-- Submit (default for forms) -->
<form data-rx-action="/submit" data-rx-trigger="submit">

<!-- Mouse hover -->
<div data-rx-action="/preview" data-rx-trigger="mouseover">

<!-- Focus -->
<input data-rx-action="/activate" data-rx-trigger="focus">

<!-- Blur -->
<input data-rx-action="/validate" data-rx-trigger="blur">

<!-- Keyup/Keydown -->
<input data-rx-action="/autocomplete" data-rx-trigger="keyup">
```

**Special Trigger: initialized**

Fires once when element is added to DOM.

```typescript
{
  "type": "initialized",
  "delay": number  // Optional delay in ms (default: 0)
}
```

**Examples:**
```html
<!-- Load immediately -->
<div data-rx-action="/api/stats"
     data-rx-trigger='{"type":"initialized"}'>
</div>

<!-- Load after 500ms delay -->
<div data-rx-action="/api/recommendations"
     data-rx-trigger='{"type":"initialized","delay":500}'>
</div>
```

**Use cases:**
- Load widget data on page load
- Prefetch data
- Initialize components

**Special Trigger: poll**

Fires repeatedly at intervals.

```typescript
{
  "type": "poll",
  "interval": number  // Interval in ms (default: 1000)
}
```

**Examples:**
```html
<!-- Poll every 5 seconds -->
<div data-rx-action="/api/status"
     data-rx-trigger='{"type":"poll","interval":5000}'>
  Loading...
</div>

<!-- Live metrics (every 2 seconds) -->
<div data-rx-action="/api/metrics"
     data-rx-trigger='{"type":"poll","interval":2000}'>
</div>
```

⚠️ **Note:** Consider using [Server-Sent Events](#server-sent-events) instead of polling for better performance and lower server load.

**Special Trigger: revealed**

Fires when element enters viewport (uses IntersectionObserver).

```typescript
{
  "type": "revealed",
  "margin": string  // IntersectionObserver rootMargin (default: "0px")
}
```

**Examples:**
```html
<!-- Infinite scroll - load when visible -->
<div id="load-more-sentinel"
     data-rx-action="/api/items/next"
     data-rx-trigger='{"type":"revealed"}'>
</div>

<!-- Load earlier (200px before visible) -->
<div data-rx-action="/api/images"
     data-rx-trigger='{"type":"revealed","margin":"200px"}'>
</div>

<!-- Lazy load image -->
<div data-rx-action="/images/load/photo-123"
     data-rx-trigger='{"type":"revealed","margin":"100px"}'>
  <div class="placeholder"></div>
</div>
```

### Request Behavior

#### data-rx-debounce

Delays request execution until user stops triggering events.

**Type:** `number` (milliseconds)

**Default:** `0` (no debounce)

**Examples:**
```html
<!-- Search as user types (300ms delay) -->
<input type="search"
       data-rx-action="/search"
       data-rx-trigger="input"
       data-rx-debounce="300"
       placeholder="Search...">

<!-- Auto-save (2 seconds) -->
<textarea data-rx-action="/save-draft"
          data-rx-trigger="input"
          data-rx-debounce="2000">
</textarea>

<!-- Email validation (500ms) -->
<input type="email"
       data-rx-action="/validate-email"
       data-rx-trigger="input"
       data-rx-debounce="500">
```

**How it works:**
```
User types: "h" → Timer starts (300ms)
User types: "he" → Timer resets (300ms)
User types: "hel" → Timer resets (300ms)
User stops typing → Wait 300ms → Request sent
```

**Recommended values:**
- Search: 300-500ms
- Auto-save: 1000-2000ms
- Validation: 500ms

#### data-rx-disable-in-flight

Disables element while request is in progress.

**Type:** Boolean (presence = enabled)

**Examples:**
```html
<!-- Disable button during request -->
<button data-rx-action="/api/submit"
        data-rx-method="POST"
        data-rx-disable-in-flight>
  Submit
</button>

<!-- Disable entire form -->
<form data-rx-action="/api/save"
      data-rx-method="POST"
      data-rx-disable-in-flight>
  <!-- All inputs disabled during submission -->
  <input name="title">
  <button>Save</button>
</form>

<!-- Combine with loading indicator -->
<button data-rx-action="/api/process"
        data-rx-disable-in-flight
        data-rx-loading-indicator="spinner">
  Process
</button>
```

**Behavior:**
- Adds `disabled` attribute to element
- For forms, all child inputs are disabled
- Automatically re-enabled when response received
- Prevents double-submission

#### data-rx-disable-queueing

Allows this element's requests to bypass the global queue and execute immediately.

**Type:** Boolean (presence = enabled)

**Default:** All requests use a single global queue (execute sequentially)

**When queueing is enabled (default):**
```html
<button data-rx-action="/save-user">Save User</button>
<button data-rx-action="/save-settings">Save Settings</button>

<!-- User clicks both quickly -->
<!-- → Save User runs first -->
<!-- → Save Settings waits in queue -->
<!-- → Save Settings runs after Save User completes -->
```

**When queueing is disabled:**
```html
<button data-rx-action="/update-sidebar"
        data-rx-disable-queueing>
  Update Sidebar
</button>

<button data-rx-action="/refresh-stats"
        data-rx-disable-queueing>
  Refresh Stats
</button>

<!-- User clicks both quickly -->
<!-- → BOTH requests run concurrently -->
```

**Use when:**
- Operations are independent (don't affect same data)
- Real-time widgets that shouldn't wait
- Background updates during user interaction

**Don't use when:**
- Operations modify shared state
- Order matters
- Concurrent updates could cause conflicts

#### data-rx-allow-event-default

Allows default browser behavior for the event.

**Type:** Boolean (presence = enabled)

**Default:** `false` (preventDefault() called)

**Examples:**
```html
<!-- Let checkbox state update -->
<input type="checkbox"
       data-rx-action="/toggle"
       data-rx-trigger="change"
       data-rx-allow-event-default>

<!-- Form with native HTML validation -->
<form data-rx-action="/submit"
      data-rx-allow-event-default>
  <input required>
  <button>Submit</button>
</form>
```

### State Management

#### data-rx-include-state

Includes stored state values in the request as query parameters.

**Type:** `string` (JSON array) or `string` (single key)

**Storage priority:**
1. sessionStorage (checked first)
2. localStorage (fallback if key not in session)

**Examples:**
```html
<!-- Include single state key -->
<button data-rx-action="/search"
        data-rx-include-state="filter">
  Search
</button>

<!-- Include multiple keys -->
<button data-rx-action="/api/data"
        data-rx-include-state='["filter", "sort", "page"]'>
  Load Data
</button>

<!-- Include instance ID (for SSE) -->
<form data-rx-action="/submit"
      data-rx-include-state='["rx-instance-id"]'>
</form>
```

**Complete flow:**

**1. Server sets state:**
```csharp
return await rxDriver
    .With(context)
    .AddTriggerSetState("filter", "active", MetadataScope.Session, updateUrl: true)
    .Render();
```

**2. Browser stores state:**
```javascript
sessionStorage.setItem('filter', 'active');
```

**3. Client includes in next request:**
```html
<button data-rx-action="/search"
        data-rx-include-state='["filter"]'>
```

**4. Request URL becomes:**
```
GET /search?filter=active
```

**5. Server reads from query:**
```csharp
public static async Task<IResult> Search(
    HttpContext context,
    IRxDriver rxDriver,
    string? filter = null)  // ← Bound from query string
{
    // filter = "active"
}
```

**Special state keys:**
- `rx-instance-id` - Unique page instance UUID (auto-generated)
- Custom keys - Set via `AddTriggerSetState()`

### UI Feedback

#### data-rx-loading-indicator

Shows/hides element while request is in progress.

**Type:** `string` (element ID) or `false` (disable)

**Examples:**
```html
<!-- Simple spinner -->
<button data-rx-action="/api/save"
        data-rx-loading-indicator="spinner">
  Save
  <span id="spinner" class="rx-loading-hidden">⏳</span>
</button>

<!-- Loading text -->
<form data-rx-action="/submit"
      data-rx-loading-indicator="loading-text">
  <button>Submit</button>
</form>
<div id="loading-text" class="rx-loading-hidden">
  Submitting...
</div>

<!-- Disable indicator -->
<button data-rx-action="/api/action"
        data-rx-loading-indicator="false">
  No Indicator
</button>
```

**CSS classes:**

```css
/* Hidden state (default) */
.rx-loading-hidden {
  display: none;
}

/* Visible state (during request) */
.rx-loading-visible {
  display: inline-block;
}
```

**Customize classes** via `razorx.init()`:
```javascript
razorx.init({
  loadingIndicatorClasses: {
    hidden: 'my-hidden-class',
    visible: 'my-visible-class'
  }
});
```

### Advanced Attributes

#### data-rx-delegate-action-to

Transfers `data-rx-action` and `data-rx-method` from target element to clicked element.

**Type:** `string` (target element ID)

**Use cases:**
- External submit buttons
- Dialog buttons that submit forms
- Multiple actions for same form

**Examples:**

**External submit button:**
```html
<form id="user-form" data-rx-action="/api/users" data-rx-method="POST">
  <input name="name">
  <!-- No submit button inside form -->
</form>

<!-- Button outside form submits it -->
<button data-rx-delegate-action-to="user-form">
  Save User
</button>
```

**Dialog pattern:**
```html
<dialog id="edit-dialog">
  <form id="edit-form" data-rx-action="/edit" data-rx-method="PUT">
    <textarea name="content"></textarea>
  </form>
  <footer>
    <button data-rx-delegate-action-to="edit-form">Save</button>
    <button onclick="this.closest('dialog').close()">Cancel</button>
  </footer>
</dialog>
```

**Multiple actions:**
```html
<form id="draft-form" data-rx-action="/save-draft">
  <textarea name="content"></textarea>
</form>

<button data-rx-delegate-action-to="draft-form">
  Save Draft
</button>

<!-- Override action for publish -->
<button data-rx-delegate-action-to="draft-form"
        data-rx-action="/publish"
        data-rx-method="POST">
  Publish Now
</button>
```

**How it works:**
1. Find target element by ID
2. Copy `data-rx-action` and `data-rx-method` from target
3. Collect form data from target (if form)
4. Send request with target's configuration

#### File Upload Attributes

**data-rx-file-upload-progress-id**

Progress element ID for upload tracking.

```html
<form data-rx-action="/upload" data-rx-method="POST">
  <input type="file"
         name="file"
         data-rx-file-upload-progress-id="upload-progress">
  <button>Upload</button>
</form>

<progress id="upload-progress" value="0" max="100"></progress>
```

**data-rx-file-upload-timeout**

Upload timeout in milliseconds.

```html
<!-- 60 second timeout -->
<input type="file"
       data-rx-action="/upload"
       data-rx-file-upload-timeout="60000">
```

**data-rx-file-upload-max-size**

Maximum file size in bytes (client-side validation).

```html
<!-- 5MB max -->
<input type="file"
       data-rx-action="/upload"
       data-rx-file-upload-max-size="5242880">

<!-- Complete example: 10MB, 30s timeout, progress bar -->
<input type="file"
       data-rx-action="/upload"
       data-rx-file-upload-max-size="10485760"
       data-rx-file-upload-timeout="30000"
       data-rx-file-upload-progress-id="progress"
       accept="image/*">

<progress id="progress" value="0" max="100"></progress>
```

### Server-Sent Events Attributes

#### data-rx-sse-connect

Establishes a Server-Sent Events connection.

**Type:** `string` (URL)

**Examples:**
```html
<!-- Basic SSE connection -->
<div data-rx-sse-connect="/stream"></div>

<!-- With instance ID for correlation -->
<div data-rx-sse-connect="/stream"
     data-rx-include-state='["rx-instance-id"]'>
</div>
```

**Connection behavior:**
- Auto-reconnect with exponential backoff: 1s → 2s → 4s → 8s → 16s → 30s max
- Stays at 30s until reconnected
- Resets to 1s on successful connection
- State tracked via `data-sse-state` attribute: `"connected"` or `"error"`
- Automatic cleanup when element removed from DOM

#### data-rx-sse-events

Filter which SSE event types to listen for.

**Type:** `string` (single event) or `JSON array` (multiple events)

**Default:** Listens to all events

**Examples:**
```html
<!-- Single event type -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-events="user-update">
</div>

<!-- Multiple event types -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-events='["user-update", "comment-added", "notification"]'>
</div>
```

#### data-rx-sse-connect-delay

Delays the SSE connection by specified milliseconds.

**Type:** `number` (milliseconds)

**Default:** `0` (connect immediately)

**Example:**
```html
<!-- Connect after 500ms -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-connect-delay="500">
</div>
```

**Use cases:**
- Stagger connections on page load
- Wait for other initializations
- Reduce initial load spike

---

## Server API Reference

### IRxDriver Interface

Main framework interface, injected via DI (scoped per HTTP request).

#### With()

Creates a response builder for composing fragment responses.

```csharp
IRxResponseBuilder With(HttpContext context)
```

**Example:**
```csharp
public static async Task<IResult> MyHandler(
    HttpContext context,
    IRxDriver rxDriver)
{
    return await rxDriver
        .With(context)
        .AddFragment<Component, Model>(model, "target")
        .Render();
}
```

#### RenderPage()

Renders a complete HTML page with layout and content. Has 5 overloads:

**1. Page with Model**
```csharp
Task<IResult> RenderPage<TRoot, TComponent, TModel>(
    HttpContext context,
    TModel model,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where TComponent : IComponent, IComponentModel<TModel>
```

**Example:**
```csharp
return await rxDriver.RenderPage<App, ProductPage, ProductModel>(
    context,
    product
);
```

**2. Page without Model**
```csharp
Task<IResult> RenderPage<TRoot, TComponent>(
    HttpContext context,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where TComponent : IComponent
```

**3. Page with Custom Head (No Model) and Body with Model**
```csharp
Task<IResult> RenderPage<TRoot, THead, TComponent, TModel>(
    HttpContext context,
    TModel model,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where THead : IComponent
where TComponent : IComponent, IComponentModel<TModel>
```

**4. Page with Custom Head and Body (No Models)**
```csharp
Task<IResult> RenderPage<TRoot, THead, TComponent>(
    HttpContext context,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where THead : IComponent
where TComponent : IComponent
```

**5. Page with Separate Head and Body Models**
```csharp
Task<IResult> RenderPage<TRoot, THead, TComponent, THeadModel, TModel>(
    HttpContext context,
    THeadModel headModel,
    TModel model,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where THead : IComponent, IComponentModel<THeadModel>
where TComponent : IComponent, IComponentModel<TModel>
```

**Example with custom head content (SEO meta tags):**

Define your models:
```csharp
public record ProductSeoModel(string Title, string Description, string ImageUrl);
public record ProductModel(string Name, decimal Price, string Description);
```

Create a head component:
```razor
@* Components/Products/ProductHead.razor *@
@using RazorX.Framework
@implements IComponentModel<ProductSeoModel>

<title>@Model.Title</title>
<meta name="description" content="@Model.Description" />
<meta property="og:title" content="@Model.Title" />
<meta property="og:description" content="@Model.Description" />
<meta property="og:image" content="@Model.ImageUrl" />

@code {
    [Parameter] public ProductSeoModel Model { get; set; } = null!;
}
```

Use it in your handler:
```csharp
public static async Task<IResult> GetProduct(
    HttpContext context,
    IRxDriver rxDriver,
    string id)
{
    var product = _products.Find(p => p.Id == id);

    var seoData = new ProductSeoModel(
        Title: $"{product.Name} - Buy Online",
        Description: product.Description,
        ImageUrl: product.ImageUrl
    );

    return await rxDriver.RenderPage<
        App,
        ProductHead,      // Custom head with SEO tags
        ProductPage,
        ProductSeoModel,  // Head model
        ProductModel>(    // Body model
            context,
            seoData,
            product
        );
}
```

The layout renders it:
```razor
@* App.razor (from Quick Start) *@
<head>
    <meta charset="utf-8" />
    <title>My RazorX App</title>
    <link rel="stylesheet" href="/css/razorx.css" />

    @if (HeadContent is not null)
    {
        <DynamicComponent Type="HeadContent" Parameters="HeadContentParameters" />
    }
</head>
```

Result in browser:
```html
<head>
    <meta charset="utf-8" />
    <title>My RazorX App</title>
    <link rel="stylesheet" href="/css/razorx.css" />

    <!-- ProductHead component rendered here -->
    <title>Product XYZ - Buy Online</title>
    <meta name="description" content="Amazing product..." />
    <meta property="og:title" content="Product XYZ - Buy Online" />
    <meta property="og:description" content="Amazing product..." />
    <meta property="og:image" content="/images/product.jpg" />
</head>
```

### IRxResponseBuilder Interface

Fluent API for building fragment responses.

#### AddFragment()

**With Model:**
```csharp
IRxResponseBuilder AddFragment<TComponent, TModel>(
    TModel model,
    string targetId,
    FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
)
where TComponent : IComponent, IComponentModel<TModel>
```

**Without Model:**
```csharp
IRxResponseBuilder AddFragment<TComponent>(
    string targetId,
    FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
)
where TComponent : IComponent
```

**Example:**
```csharp
return await rxDriver
    .With(context)
    .AddFragment<UserCard, UserModel>(
        user,
        "user-123",
        FragmentMergeStrategyType.Morph)
    .Render();
```

#### RemoveElement()

Removes element from DOM.

```csharp
IRxResponseBuilder RemoveElement(string targetId)
```

**Example:**
```csharp
return await rxDriver
    .With(context)
    .RemoveElement($"todo-{id}")
    .AddTriggerToast("Deleted", ToastType.Success)
    .Render();
```

#### AddTriggerToast()

Display toast notification.

```csharp
IRxResponseBuilder AddTriggerToast(
    string message,
    ToastType type = ToastType.Success,
    int duration = 3500,
    ToastVerticalPosition verticalPosition = ToastVerticalPosition.Top,
    ToastHorizontalPosition horizontalPosition = ToastHorizontalPosition.Right,
    bool clickToDismiss = true
)
```

**Parameters:**
- `message` - Text to display (HTML-escaped automatically)
- `type` - `Info`, `Success`, `Warning`, `Error`
- `duration` - Milliseconds (0 = permanent)
- `verticalPosition` - `Top`, `Center`, `Bottom`
- `horizontalPosition` - `Left`, `Middle`, `Right`
- `clickToDismiss` - Allow click to dismiss

**Examples:**
```csharp
// Default (top-right, 3.5s)
.AddTriggerToast("User created", ToastType.Success)

// Bottom-left, 5s
.AddTriggerToast(
    "Failed to save",
    ToastType.Error,
    duration: 5000,
    verticalPosition: ToastVerticalPosition.Bottom,
    horizontalPosition: ToastHorizontalPosition.Left)

// Permanent warning
.AddTriggerToast(
    "Session expiring soon",
    ToastType.Warning,
    duration: 0,
    clickToDismiss: true)
```

#### AddTriggerFocusElement()

Set focus to element.

```csharp
IRxResponseBuilder AddTriggerFocusElement(
    string elementId,
    bool positionCursorEnd = false
)
```

**Examples:**
```csharp
// Focus username field
.AddTriggerFocusElement("username")

// Focus and position cursor at end
.AddTriggerFocusElement("comment-input", positionCursorEnd: true)
```

#### AddTriggerSetState()

Set single state value in browser storage.

```csharp
IRxResponseBuilder AddTriggerSetState(
    string key,
    string value,
    MetadataScope scope = MetadataScope.Session,
    bool updateUrl = false
)
```

**Parameters:**
- `key` - Storage key (alphanumeric, hyphens, underscores only)
- `value` - Value to store
- `scope` - `Session` (sessionStorage) or `Persistent` (localStorage)
- `updateUrl` - Also update URL query parameters

**Examples:**
```csharp
// Session storage
.AddTriggerSetState("filter", "active", MetadataScope.Session)

// With URL sync
.AddTriggerSetState("page", "2", MetadataScope.Session, updateUrl: true)
// URL becomes: /products?page=2

// Persistent preference
.AddTriggerSetState("theme", "dark", MetadataScope.Persistent)
```

#### AddTriggerSetStateBatch()

Set multiple state values.

```csharp
IRxResponseBuilder AddTriggerSetStateBatch(
    Dictionary<string, string> state,
    MetadataScope scope,
    bool updateUrl = false
)
```

**Example:**
```csharp
.AddTriggerSetStateBatch(
    new Dictionary<string, string> {
        { "filter", "active" },
        { "sort", "date" },
        { "page", "1" }
    },
    MetadataScope.Session,
    updateUrl: true)
// URL: /products?filter=active&sort=date&page=1
```

#### AddTriggerCloseDialog()

Close HTML dialog element.

```csharp
IRxResponseBuilder AddTriggerCloseDialog(
    string dialogId,
    string? onCloseData = null,
    string? resetFormId = null
)
```

**Examples:**
```csharp
// Simple close
.AddTriggerCloseDialog("edit-dialog")

// Close and reset form
.AddTriggerCloseDialog("edit-dialog", resetFormId: "edit-form")

// Pass data to close handler
.AddTriggerCloseDialog("confirm-dialog", onCloseData: "cancelled")
```

#### Render()

Execute builder and return response.

```csharp
Task<IResult> Render(
    bool ignoreActiveElementValueOnMorph = false,
    CancellationToken cancellationToken = default
)
```

**Parameters:**
- `ignoreActiveElementValueOnMorph` - Preserve focused input value during morph (default: false)
- `cancellationToken` - Cancellation token

**Behavior:**
1. Renders all fragments in parallel (`Task.WhenAll`)
2. Sets response headers (`rx-merge`, `rx-trigger-*`)
3. Returns `IResult` with HTML content

**Examples:**
```csharp
// Basic render
return await rxDriver
    .With(context)
    .AddFragment<Component>("target")
    .Render();

// Preserve input value during morph
return await rxDriver
    .With(context)
    .AddFragment<EditForm, Model>(model, "form", FragmentMergeStrategyType.Morph)
    .Render(ignoreActiveElementValueOnMorph: true);
```

#### RenderSse()

Stream fragments via Server-Sent Events.

```csharp
IResult RenderSse<TModel>(
    IAsyncEnumerable<TModel> models,
    Func<TModel, IRxResponseBuilder, Task> configureEvent,
    string eventType = "rx-server-sent-event",
    CancellationToken cancellationToken = default
)
```

**Parameters:**
- `models` - Async stream of data
- `configureEvent` - Callback to build response for each model
- `eventType` - SSE event type for client filtering
- `cancellationToken` - Stops stream when cancelled

**Example:**
```csharp
return rxDriver.With(context).RenderSse(
    GetNotificationsAsync(userId, ct),
    async (notification, builder) => {
        builder
            .AddFragment<NotificationCard, Notification>(
                notification,
                "notifications",
                FragmentMergeStrategyType.AppendAfterBegin)
            .AddTriggerToast(notification.Message, ToastType.Info);
    },
    eventType: "notification",
    cancellationToken: ct
);
```

### Fragment Merge Strategies

#### Swap

Replaces entire element.

```csharp
FragmentMergeStrategyType.Swap
```

**Before:**
```html
<div id="user-123" class="user-card">
    <h3>John Doe</h3>
</div>
```

**Server sends:**
```html
<div id="user-123" class="user-card updated">
    <h3>Jane Smith</h3>
</div>
```

**After:**
```html
<div id="user-123" class="user-card updated">  <!-- Entire element replaced -->
    <h3>Jane Smith</h3>
</div>
```

**Use when:**
- Element attributes change
- Complete replacement needed
- Element structure changes

#### SwapInner

Replaces innerHTML only, preserves element.

```csharp
FragmentMergeStrategyType.SwapInner
```

**Before:**
```html
<div id="container" class="my-class" data-x="y">
    <p>Old content</p>
</div>
```

**Server sends:**
```html
<div id="container">
    <p>New content</p>
</div>
```

**After:**
```html
<div id="container" class="my-class" data-x="y">  <!-- Element kept -->
    <p>New content</p>
</div>
```

**Use when:**
- Preserving wrapper element
- Keeping element ID and classes
- Element has event listeners you want to keep

#### Morph

Intelligently updates DOM using [Idiomorph](https://github.com/bigskysoftware/idiomorph), preserving state.

```csharp
FragmentMergeStrategyType.Morph
```

**Preserves:**
- Focused element
- Input values (if focused)
- Scroll position
- CSS transitions
- Event listeners

**Updates:**
- Attributes
- Text content
- Non-focused elements
- Classes

**Example:**
```html
<!-- Before -->
<form id="edit-form">
    <input id="name" value="John" class="valid">
    <input id="email" value="john@example.com" class="valid">  <!-- User typing here -->
</form>

<!-- Server sends (user changed to Jane) -->
<form id="edit-form">
    <input id="name" value="Jane" class="valid">
    <input id="email" value="john@example.com" class="valid">
</form>

<!-- After (name updated, email preserved because focused) -->
<form id="edit-form">
    <input id="name" value="Jane" class="valid">
    <input id="email" value="john@example.com" class="valid">  <!-- Still typing -->
</form>
```

**Control morph behavior:**
```csharp
// Default: Preserve focused input values
.Render(ignoreActiveElementValueOnMorph: false)

// Force server value even if focused
.Render(ignoreActiveElementValueOnMorph: true)
```

**Use when:**
- Forms while user is editing
- Real-time collaboration
- Live updates without disruption
- Preserving input state is important

#### AppendAfterBegin

Inserts fragment as first child.

```csharp
FragmentMergeStrategyType.AppendAfterBegin
```

**Visual:**
```html
<!-- Before -->
<div id="list">
    <div>Item 2</div>
    <div>Item 3</div>
</div>

<!-- Server sends -->
<div>Item 1</div>

<!-- After -->
<div id="list">
    <div>Item 1</div>  <!-- ← Inserted here -->
    <div>Item 2</div>
    <div>Item 3</div>
</div>
```

**Use for:**
- Prepend to list (newest first)
- Add notifications at top
- Recent activity feed

#### AppendBeforeEnd

Inserts fragment as last child.

```csharp
FragmentMergeStrategyType.AppendBeforeEnd
```

**Visual:**
```html
<!-- Before -->
<div id="list">
    <div>Item 1</div>
    <div>Item 2</div>
</div>

<!-- Server sends -->
<div>Item 3</div>

<!-- After -->
<div id="list">
    <div>Item 1</div>
    <div>Item 2</div>
    <div>Item 3</div>  <!-- ← Inserted here -->
</div>
```

**Use for:**
- Append to list (oldest first)
- Infinite scroll (load more)
- Chat messages

#### AppendAfterEnd

Inserts fragment after target element (sibling).

```csharp
FragmentMergeStrategyType.AppendAfterEnd
```

**Visual:**
```html
<!-- Before -->
<div id="field-1"><input></div>

<!-- Server sends -->
<div id="field-2"><input></div>

<!-- After -->
<div id="field-1"><input></div>
<div id="field-2"><input></div>  <!-- ← Inserted here -->
```

**Use for:**
- Adding sibling elements
- Dynamic form fields
- Insert after specific element

#### AppendBeforeBegin

Inserts fragment before target element (sibling).

```csharp
FragmentMergeStrategyType.AppendBeforeBegin
```

**Visual:**
```html
<!-- Before -->
<div id="main-content">Content</div>

<!-- Server sends -->
<div id="alert">Warning!</div>

<!-- After -->
<div id="alert">Warning!</div>  <!-- ← Inserted here -->
<div id="main-content">Content</div>
```

**Use for:**
- Inserting before specific element
- Adding headers dynamically
- Alerts above content

---

## Advanced Features

### Server-Sent Events

SSE enables real-time server-to-client updates over HTTP. RazorX provides native SSE support using .NET's `ServerSentEvents` API.

#### Basic SSE Stream

**Client:**
```html
<div data-rx-sse-connect="/stream/notifications"></div>

<div id="notifications">
    <!-- Updates appear here -->
</div>
```

**Server:**
```csharp
public static IResult StreamNotifications(
    HttpContext context,
    IRxDriver rxDriver,
    CancellationToken ct)
{
    var userId = context.User.Identity?.Name ?? "anonymous";

    return rxDriver
        .With(context)
        .RenderSse(
            GetNotificationsAsync(userId, ct),
            async (notification, builder) => {
                builder
                    .AddFragment<NotificationCard, Notification>(
                        notification,
                        "notifications",
                        FragmentMergeStrategyType.AppendAfterBegin)
                    .AddTriggerToast(
                        $"New: {notification.Title}",
                        ToastType.Success,
                        3000);
            },
            ct
        );
}

private static async IAsyncEnumerable<Notification> GetNotificationsAsync(
    string userId,
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var notification in _notificationService.WatchAsync(userId, ct))
    {
        yield return notification;
    }
}
```

#### Full Builder API in SSE

You can use ALL `IRxResponseBuilder` methods in SSE events:

```csharp
async (update, builder) => {
    // Multiple fragments
    builder
        .AddFragment<CpuGauge, CpuMetric>(update.Cpu, "cpu-gauge", FragmentMergeStrategyType.Morph)
        .AddFragment<MemoryGauge, MemoryMetric>(update.Memory, "memory-gauge", FragmentMergeStrategyType.Morph)
        .AddFragment<ActivityLog, LogEntry>(update.LatestLog, "activity-log", FragmentMergeStrategyType.AppendBeforeEnd);

    // All triggers
    builder
        .AddTriggerToast("Update received", ToastType.Info)
        .AddTriggerFocusElement("input-field")
        .AddTriggerSetState("last-update", DateTime.UtcNow.ToString("o"), MetadataScope.Session)
        .AddTriggerCloseDialog("loading-dialog");

    // Remove elements
    builder.RemoveElement("old-notification");

    // Conditional logic
    if (update.IsUrgent)
    {
        builder.AddTriggerToast("URGENT!", ToastType.Warning);
    }
}
```

#### Event Type Filtering

**Server:**
```csharp
// Send different event types
return rxDriver.With(context).RenderSse(
    GetUpdatesAsync(ct),
    async (update, builder) => {
        builder.AddFragment<UpdateCard, Update>(update, "updates", FragmentMergeStrategyType.AppendBeforeEnd);
    },
    eventType: update.Priority == "high" ? "urgent-update" : "normal-update",
    cancellationToken: ct
);
```

**Client:**
```html
<!-- Listen only to urgent updates -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-events="urgent-update">
</div>

<!-- Listen to multiple types -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-events='["urgent-update", "normal-update"]'>
</div>
```

#### Polling vs SSE Comparison

**Before (Polling):**
```html
<div id="status"
     data-rx-action="/api/status"
     data-rx-trigger='{"type":"poll","interval":5000}'>
</div>
```
- ❌ 5 second delay for updates
- ❌ Constant server load (12 requests/minute)
- ❌ High bandwidth (repeated requests)
- ❌ Only one fragment per poll

**After (SSE):**
```html
<div data-rx-sse-connect="/api/status/stream"></div>
```
- ✅ Instant updates (no delay)
- ✅ Significantly lower server load (single persistent connection)
- ✅ Lower bandwidth (server sends only when data changes)
- ✅ Multiple fragments + triggers per event

### Multi-Client Broadcasting

For scenarios where multiple clients should receive the same updates (notifications, dashboards, collaborative editing), use `RxSseBroadcastService<TModel, TMetadata>`.

#### Setup

**1. Define Metadata Type:**

Metadata can be any JSON-serializable record. No interface required.

```csharp
// Models/TodoMetadata.cs
public record TodoMetadata
{
    public string? SubscriberId { get; init; }
    public string? TenantId { get; init; }
    public string? Role { get; init; }
}
```

**2. Register Service:**

```csharp
// Program.cs
builder.Services.AddSingleton(sp => {
    var logger = sp.GetRequiredService<ILogger<RxSseBroadcastService<TodoModel, TodoMetadata>>>();
    return new RxSseBroadcastService<TodoModel, TodoMetadata>(logger);
});
```

**3. SSE Stream Handler:**

Subscribers provide a filter function that examines broadcaster metadata.

```csharp
public static IResult StreamUpdates(
    HttpContext context,
    [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
    IRxDriver rxDriver,
    RxSseBroadcastService<TodoModel, TodoMetadata> broadcast,
    CancellationToken ct)
{
    var tenantId = context.User.FindFirst("TenantId")!.Value;

    // Subscribe with filter: only my tenant, exclude my own updates
    broadcast.Subscribe(
        rxInstanceId,
        filter: meta =>
            meta?.TenantId == tenantId &&
            meta?.SubscriberId != rxInstanceId
    );

    ct.Register(() => broadcast.Unsubscribe(rxInstanceId));

    return rxDriver
        .With(context)
        .RenderSse(
            broadcast.GetUpdates(rxInstanceId, ct),
            async (todo, builder) => {
                builder.AddFragment<TodoCard, TodoModel>(
                    todo,
                    "todo-list",
                    FragmentMergeStrategyType.AppendBeforeEnd);
            },
            ct
        );
}
```

**4. Broadcast Handler:**

Broadcasts include metadata that subscriber filters examine.

```csharp
public static async Task<IResult> UpdateTodo(
    HttpContext context,
    [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
    IRxDriver rxDriver,
    RxSseBroadcastService<TodoModel, TodoMetadata> broadcast,
    TodoModel todo)
{
    var tenantId = context.User.FindFirst("TenantId")!.Value;

    // Save to database
    _todos[_todos.FindIndex(t => t.Id == todo.Id)] = todo;

    // Broadcast to other clients (subscribers' filters decide who receives)
    await broadcast.BroadcastUpdate(
        todo,
        new TodoMetadata {
            SubscriberId = rxInstanceId,
            TenantId = tenantId
        }
    );

    // Update triggering client
    return await rxDriver
        .With(context)
        .AddFragment<TodoCard, TodoModel>(
            todo,
            $"todo-{todo.Id}",
            FragmentMergeStrategyType.Swap)
        .Render();
}
```

#### Common Filtering Patterns

**Echo Suppression Only:**
```csharp
// Subscriber
broadcast.Subscribe(
    myId,
    filter: meta => meta?.SubscriberId != myId
);

// Broadcaster
await broadcast.BroadcastUpdate(
    model,
    new Metadata { SubscriberId = myId }
);
```

**Tenant Isolation:**
```csharp
// Subscriber
broadcast.Subscribe(
    id,
    filter: meta => meta?.TenantId == myTenantId
);

// Broadcaster
await broadcast.BroadcastUpdate(
    model,
    new Metadata { TenantId = tenantId }
);
```

**Role-Based:**
```csharp
// Subscriber (admins only)
broadcast.Subscribe(
    id,
    filter: meta => meta?.Role == "Admin"
);

// Broadcaster
await broadcast.BroadcastUpdate(
    adminAlert,
    new Metadata { Role = "Admin" }
);
```

**Broadcast to All:**
```csharp
// Subscriber (no filter)
broadcast.Subscribe(id, filter: null);

// Broadcaster (no metadata needed)
await broadcast.BroadcastUpdate(globalAnnouncement);
```

#### Distributed Broadcasting (Multi-Server)

For multi-server deployments, use a transport like Redis:

```csharp
builder.Services.AddRxSseBroadcast<TodoModel, TodoMetadata>(
    MyAppJsonContext.Default.TodoModel,  // Your source-generated JsonTypeInfo
    options => options.UseRedis("redis-connection-string")
);
```

**How it works:**
```
Server A                    Redis                    Server B
   ↓                         ↓                          ↓
[HTTP POST]           [Pub/Sub Channel]       [SSE Client B1]
   ↓                         ↓                          ↓
BroadcastUpdate() ──────> Publish ─────────────────> Subscribe
   ↓                                                    ↓
[Local SSE Clients]                            [Deliver to B1]
[Deliver immediately]
```

**Key points:**
- Local clients receive updates immediately (same-server delivery)
- Remote clients receive updates via transport (latency depends on transport and network)
- Subscription-time filters work across servers
- Requires AOT-compatible `JsonTypeInfo<T>`

---

## AOT Compilation

RazorX.Framework supports Native AOT compilation for faster startup and smaller deployments.

### Requirements

#### 1. Route Handler Preservation

**Option A: Root Assembly Preservation (Simplest)**

```xml
<!-- In your .csproj -->
<ItemGroup>
  <TrimmerRootAssembly Include="YourApplicationName" />
</ItemGroup>
```

**Option B: Manual Registration (Most AOT-Friendly)**

```csharp
// Instead of assembly scanning:
// app.MapGroup(string.Empty).MapRoutes();

// Do manual registration:
var routes = app.MapGroup(string.Empty);
new HomeHandler().MapRoutes(routes);
new UserHandler().MapRoutes(routes);
new ProductHandler().MapRoutes(routes);
```

#### 2. Component Preservation

Razor components are automatically preserved by the Razor compiler. No action needed.

#### 3. Model Preservation

Models used with components are automatically tracked when used in generic method calls. No special action needed.

### Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- AOT Configuration -->
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>false</InvariantGlobalization>

    <!-- Optional: Aggressive trimming -->
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>

    <!-- Optional: Single file -->
    <PublishSingleFile>true</PublishSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="RazorX.Framework" Version="1.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Preserve your application assembly -->
    <TrimmerRootAssembly Include="YourApplicationName" />
  </ItemGroup>
</Project>
```

### Publishing

```bash
# Development build
dotnet publish -c Release -r linux-x64

# Production build (Full AOT)
dotnet publish -c Release -r linux-x64 -p:PublishAot=true

# Platform-specific
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
dotnet publish -c Release -r win-x64 -p:PublishAot=true
dotnet publish -c Release -r osx-arm64 -p:PublishAot=true
```

### Expected Results

**Binary Size:**

| Configuration | Size (Approx) |
|--------------|---------------|
| Normal .NET | ~80-100 MB |
| Trimmed | ~30-50 MB |
| AOT | ~15-25 MB |

**Startup Time:**

| Configuration | Cold Start |
|--------------|------------|
| Normal .NET | ~500-1000ms |
| Trimmed | ~300-600ms |
| AOT | ~50-150ms |

**Memory:**
- Lower baseline (no JIT compiler)
- Faster warm-up (no JIT compilation)
- More predictable (all code pre-compiled)

### Verification Checklist

- [ ] Build completes without warnings
- [ ] All route handlers discovered/registered
- [ ] All components render correctly
- [ ] Forms submit correctly
- [ ] Application starts quickly
- [ ] Binary size acceptable
- [ ] All tests pass

---

## Troubleshooting

### My handler isn't being found

**Symptom:** 404 errors or routes not registered

**Cause:** AOT trimming or assembly scanning issue

**Solution 1: Use manual registration**
```csharp
var routes = app.MapGroup(string.Empty);
new MyHandler().MapRoutes(routes);
```

**Solution 2: Preserve assembly**
```xml
<ItemGroup>
  <TrimmerRootAssembly Include="YourApplicationName" />
</ItemGroup>
```

### My component says "Model is null"

**Symptom:** NullReferenceException when accessing `Model`

**Cause:** Component doesn't implement `IComponentModel<T>`

**Solution:** Implement the interface
```razor
@using RazorX.Framework
@implements IComponentModel<MyModel>

@code {
    [Parameter] public MyModel Model { get; set; } = null!;
}
```

### Fragments not updating

**Symptom:** DOM doesn't change after request

**Causes:**
1. Target ID doesn't exist
2. Fragment has wrong ID
3. JavaScript error

**Debug steps:**

1. **Verify target exists:**
```javascript
// In browser console
document.getElementById('target-id')
// Should return element, not null
```

2. **Check response headers:**
```
Network tab → Select request → Headers → Response Headers
Look for: rx-merge
```

3. **Check console for errors:**
```
Console tab → Look for RazorX errors
```

4. **Verify fragment ID matches target:**
```csharp
// Server
.AddFragment<Component>("target-id", ...)

// HTML - Fragment must have matching ID
<div id="target-id">...</div>
```

### Forms not submitting

**Symptom:** Form submission does nothing

**Cause:** Missing `data-rx-action` or `data-rx-trigger`

**Solution:** Add required attributes
```html
<form data-rx-action="/submit"
      data-rx-method="POST"
      data-rx-trigger="submit">
  <button>Submit</button>
</form>
```

### CSRF token errors

**Symptom:** 400 Bad Request with antiforgery error

**Cause:** Missing token cookie in requests

**Solution:** Add cookie to requests
```javascript
razorx.init({
    addCookieToRequestHeader: 'RequestVerificationToken'
});
```

And ensure middleware is registered:
```csharp
builder.Services.AddAntiforgery();
builder.Services.AddRxAntiforgery();
app.UseAntiforgery();
app.UseRxAntiforgeryCookie();
```

### SSE connection keeps disconnecting

**Symptom:** Connection state changes to "error" repeatedly

**Causes:**
1. Server endpoint returns non-SSE response
2. Endpoint throws exception
3. Network issues

**Debug steps:**

1. **Check endpoint returns SSE:**
```csharp
// Must return RenderSse
return rxDriver.With(context).RenderSse(...);
// NOT Render()
```

2. **Check for exceptions:**
```csharp
// Add try-catch in async enumerable
private static async IAsyncEnumerable<T> GetUpdatesAsync(...)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            yield return await GetNextUpdate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SSE stream");
            // Don't rethrow - kills connection
        }
    }
}
```

3. **Check connection state:**
```javascript
// In browser console
document.querySelector('[data-rx-sse-connect]').dataset.sseState
// Should be: "connected"
```

### Toasts not appearing

**Symptom:** No visual notification

**Causes:**
1. Missing razorx.css
2. Custom CSS overriding styles
3. Toast duration too short

**Debug steps:**

1. **Verify CSS loaded:**
```html
<!-- In App.razor -->
<link rel="stylesheet" href="/css/razorx.css" />
```

2. **Check element exists:**
```javascript
// In browser console after toast triggers
document.querySelector('[role="dialog"][popover]')
// Should show toast element while visible
```

3. **Increase duration:**
```csharp
.AddTriggerToast("Test", ToastType.Info, duration: 10000)
```

### Morph not preserving input values

**Symptom:** Typed input disappears on update

**Cause:** Default morph behavior overwrites focused input

**Solution:** Enable preservation
```csharp
.Render(ignoreActiveElementValueOnMorph: true)
```

Or use different merge strategy:
```csharp
// Update specific fields, not entire form
.AddFragment<FieldError>(error, "error-message", FragmentMergeStrategyType.Swap)
```

---

## Requirements

**Server:**
- .NET 10.0 or later
- ASP.NET Core

**Browser:**
- Chrome/Edge 114+ (May 2023)
- Safari 17+ (September 2023)
- Firefox 125+ (March 2024)

These versions provide required APIs: Popover (toasts), EventSource (SSE), and ES modules. View Transitions API (Chrome/Safari only) is used when available but not required.

---

## License

MIT

## Repository

https://github.com/ranzlee/razorx-framework
