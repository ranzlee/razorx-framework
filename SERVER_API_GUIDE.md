# RazorX.Framework Server API Guide

## 📖 About This Guide

This guide documents the server-side API for building RazorX responses using **IRxDriver** and **IRxResponseBuilder**. These are the core interfaces you'll use in every request handler to render components, send triggers, and build interactive responses.

**Who should read this:** .NET developers building RazorX applications.

**Prerequisites:**
- C# and ASP.NET Core knowledge
- Understanding of dependency injection
- Familiarity with Razor components (`.razor` files)
- Basic HTTP concepts (requests, responses, methods)

---

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [IRxDriver API](#irxdriver-api)
   - [With()](#with)
   - [RenderPage()](#renderpage)
3. [IRxResponseBuilder API](#irxresponsebuilder-api)
   - [Fragment Management](#fragment-management)
   - [Server Triggers](#server-triggers)
   - [Rendering](#rendering)
4. [Fragment Merge Strategies](#fragment-merge-strategies)
5. [Server-Sent Events (SSE)](#server-sent-events-sse)
6. [Common Patterns](#common-patterns)
7. [Best Practices](#best-practices)
8. [Troubleshooting](#troubleshooting)

---

## Core Concepts

### The RazorX Request/Response Flow

```
1. Client triggers action (click, input, etc.)
     ↓
2. HTTP request sent to server
     ↓
3. RequestHandler method invoked
     ↓
4. IRxDriver injected via DI
     ↓
5. Call driver.With(context) → IRxResponseBuilder
     ↓
6. Build response with fragments + triggers
     ↓
7. Call builder.Render() → IResult
     ↓
8. Server returns HTTP response with:
   - HTML fragments (in response body)
   - Merge instructions (in rx-merge header)
   - Triggers (in rx-trigger-* headers)
     ↓
9. Client receives response
     ↓
10. RazorX.js updates DOM automatically
```

### Key Terminology

| Term | Definition |
|------|------------|
| **IRxDriver** | Main framework interface (injected via DI) |
| **IRxResponseBuilder** | Fluent API for building responses |
| **Fragment** | HTML snippet from Razor component |
| **Merge Strategy** | How fragment updates DOM (swap, morph, append, etc.) |
| **Trigger** | Server instruction for client behavior (toast, focus, etc.) |
| **Component** | Razor template (`.razor` file) |
| **Model** | Data passed to component |

### RequestHandler Pattern

All RazorX routes are defined in `RequestHandler` classes:

```csharp
public class UserHandler : RequestHandler {
    public override void MapRoutes(IEndpointRouteBuilder router) {
        router.MapGet("/users", GetUsers);
        router.MapGet("/users/{id:int}", GetUser);
        router.MapPost("/users", CreateUser);
        router.MapPut("/users/{id:int}", UpdateUser);
        router.MapDelete("/users/{id:int}", DeleteUser);
    }

    private static async Task<IResult> GetUser(
        HttpContext context,
        IRxDriver rxDriver,  // ← Injected via DI
        int id)
    {
        var user = await db.Users.FindAsync(id);

        return await rxDriver
            .With(context)
            .AddFragment<UserCard, UserModel>(user, "user-container")
            .Render();
    }
}
```

**Key Points:**
- `IRxDriver` is **scoped** to the HTTP request
- `With(context)` returns `IRxResponseBuilder`
- Builder pattern enables method chaining
- `Render()` executes and returns `IResult`

---

## IRxDriver API

The `IRxDriver` interface provides two primary capabilities:
1. **Building custom responses** via `With(context)`
2. **Rendering full pages** via `RenderPage<>()`

### With()

**Purpose:** Creates a response builder for constructing the response.

**Signature:**
```csharp
IRxResponseBuilder With(HttpContext context)
```

**Returns:** `IRxResponseBuilder` for fluent API chaining

**Usage:**
```csharp
public static async Task<IResult> MyHandler(
    HttpContext context,
    IRxDriver rxDriver)
{
    return await rxDriver
        .With(context)           // ← Start building response
        .AddFragment<Component>(targetId)
        .AddTriggerToast("Success!")
        .Render();              // ← Execute and return
}
```

**When to Use:**
- ✅ AJAX requests (fragment updates)
- ✅ Form submissions
- ✅ Partial page updates
- ✅ Any non-full-page response

---

### RenderPage()

**Purpose:** Renders a complete HTML page with layout, head, and content.

**Use Cases:**
- Initial page loads
- Full page refreshes
- Traditional GET requests

#### Overload 1: Page with Model

**Signature:**
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
public static async Task<IResult> GetProductPage(
    HttpContext context,
    IRxDriver rxDriver,
    int productId)
{
    var product = await db.Products.FindAsync(productId);

    return await rxDriver.RenderPage<
        App,           // Root layout component
        ProductPage,   // Main content component
        ProductModel>( // Model type
        context,
        product        // Model instance
    );
}
```

**Component Structure:**

**App.razor (Root Layout):**
```razor
@implements IRootComponent

<!DOCTYPE html>
<html>
<head>
    @if (HeadContent != null) {
        <DynamicComponent Type="HeadContent" Parameters="HeadContentParameters" />
    }
</head>
<body>
    <DynamicComponent Type="MainContent" Parameters="MainContentParameters" />
    <script type="module" src="/js/razorx.js"></script>
</body>
</html>

@code {
    public Type? HeadContent { get; set; }
    public Dictionary<string, object?> HeadContentParameters { get; set; } = [];
    public Type MainContent { get; set; } = null!;
    public Dictionary<string, object?> MainContentParameters { get; set; } = [];
}
```

**ProductPage.razor (Content):**
```razor
@implements IComponentModel<ProductModel>

<h1>@Model.Name</h1>
<p>@Model.Description</p>
<p>Price: @Model.Price.ToString("C")</p>

@code {
    [Parameter] public ProductModel Model { get; set; } = null!;
}
```

#### Overload 2: Page without Model

**Signature:**
```csharp
Task<IResult> RenderPage<TRoot, TComponent>(
    HttpContext context,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where TComponent : IComponent
```

**Example:**
```csharp
public static async Task<IResult> GetHomePage(
    HttpContext context,
    IRxDriver rxDriver)
{
    return await rxDriver.RenderPage<App, HomePage>(context);
}
```

**HomePage.razor (no model):**
```razor
<h1>Welcome to RazorX</h1>
<p>This component doesn't need a model.</p>
```

#### Overload 3: Page with Custom Head (No Head Model) and Body with Model

**Signature:**
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

**Use Case:** Custom head component without a model, body component with model

**Example:**
```csharp
return await rxDriver.RenderPage<
    App,           // Root layout
    StandardHead,  // Head component (no model - static meta tags)
    ProductPage,   // Body component with model
    ProductModel>( // Body model type
    context,
    product        // Body model instance
);
```

**StandardHead.razor (no model):**
```razor
<title>My Store - Products</title>
<meta name="description" content="Browse our products" />
```

**ProductPage.razor (with model):**
```razor
@implements IComponentModel<ProductModel>

<h1>@Model.Name</h1>
<p>@Model.Description</p>

@code {
    [Parameter] public ProductModel Model { get; set; } = null!;
}
```

#### Overload 4: Page with Custom Head and Body (No Models)

**Signature:**
```csharp
Task<IResult> RenderPage<TRoot, THead, TComponent>(
    HttpContext context,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where THead : IComponent
where TComponent : IComponent
```

**Use Case:** Both head and body components are static (no models)

**Example:**
```csharp
return await rxDriver.RenderPage<
    App,        // Root layout
    AboutHead,  // Static head
    AboutPage>( // Static body
    context
);
```

#### Overload 5: Page with Separate Head and Body Models

**Signature:**
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

**Use Case:** Different data for head (SEO metadata) and body (page content)

**Example:**
```csharp
// Prepare separate models for head and body
var headData = new ProductSeoModel {
    Title = $"{product.Name} - Buy Online",
    Description = product.ShortDescription,
    ImageUrl = product.PrimaryImage,
    Keywords = string.Join(", ", product.Tags)
};

var bodyData = new ProductPageModel {
    Product = product,
    Reviews = await db.Reviews.Where(r => r.ProductId == product.Id).ToListAsync(),
    RelatedProducts = await GetRelatedProductsAsync(product.CategoryId)
};

return await rxDriver.RenderPage<
    App,              // Root layout
    ProductHead,      // Head component with SEO model
    ProductPage,      // Body component with full page model
    ProductSeoModel,  // Head model type
    ProductPageModel>( // Body model type
    context,
    headData,         // Head model instance
    bodyData          // Body model instance
);
```

**ProductHead.razor:**
```razor
@implements IComponentModel<ProductSeoModel>

<title>@Model.Title</title>
<meta name="description" content="@Model.Description" />
<meta property="og:title" content="@Model.Title" />
<meta property="og:description" content="@Model.Description" />
<meta property="og:image" content="@Model.ImageUrl" />
<meta name="keywords" content="@Model.Keywords" />

@code {
    [Parameter] public ProductSeoModel Model { get; set; } = null!;
}
```

**ProductPage.razor:**
```razor
@implements IComponentModel<ProductPageModel>

<h1>@Model.Product.Name</h1>
<p>@Model.Product.Description</p>

<!-- Reviews -->
@foreach (var review in Model.Reviews) {
    <div class="review">@review.Text</div>
}

<!-- Related products -->
@foreach (var related in Model.RelatedProducts) {
    <a href="/products/@related.Id">@related.Name</a>
}

@code {
    [Parameter] public ProductPageModel Model { get; set; } = null!;
}
```

**Why Use Separate Models?**
- ✅ SEO data is separate from UI data
- ✅ Cleaner separation of concerns
- ✅ Head model can be minimal (just metadata)
- ✅ Body model can be complex (all page data)

---

## IRxResponseBuilder API

The response builder provides a fluent API for composing responses.

### Fragment Management

#### AddFragment<TComponent, TModel>()

**Purpose:** Adds a Razor component fragment with a model to the response.

**Signature:**
```csharp
IRxResponseBuilder AddFragment<TComponent, TModel>(
    TModel model,
    string targetId,
    FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
)
where TComponent : IComponent, IComponentModel<TModel>
```

**Parameters:**
- `model` - Data to pass to the component
- `targetId` - DOM element ID to target
- `fragmentMergeStrategy` - How to update the DOM (default: `Swap`)

**Examples:**
```csharp
// Replace entire element with user card
return await rxDriver
    .With(context)
    .AddFragment<UserCard, UserModel>(
        user,
        "user-container",
        FragmentMergeStrategyType.Swap
    )
    .Render();

// Append todo to list
return await rxDriver
    .With(context)
    .AddFragment<TodoItem, TodoModel>(
        newTodo,
        "todo-list",
        FragmentMergeStrategyType.AppendBeforeEnd
    )
    .Render();

// Morph existing item (preserve state)
return await rxDriver
    .With(context)
    .AddFragment<TodoItem, TodoModel>(
        updatedTodo,
        $"todo-{todo.Id}",
        FragmentMergeStrategyType.Morph
    )
    .Render();
```

**Component Example (UserCard.razor):**
```razor
@implements IComponentModel<UserModel>

<div id="user-card-@Model.Id" class="card">
    <h3>@Model.Name</h3>
    <p>@Model.Email</p>
</div>

@code {
    [Parameter] public UserModel Model { get; set; } = null!;
}
```

#### AddFragment<TComponent>()

**Purpose:** Adds a component fragment without a model.

**Signature:**
```csharp
IRxResponseBuilder AddFragment<TComponent>(
    string targetId,
    FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
)
where TComponent : IComponent
```

**Use Cases:**
- Static content
- Loading indicators
- Empty states

**Example:**
```csharp
// Show loading spinner
return await rxDriver
    .With(context)
    .AddFragment<LoadingSpinner>("content")
    .Render();
```

**LoadingSpinner.razor (no model):**
```razor
<div class="spinner">
    <span>Loading...</span>
</div>
```

#### RemoveElement()

**Purpose:** Instructs client to remove a DOM element.

**Signature:**
```csharp
IRxResponseBuilder RemoveElement(string targetId)
```

**Use Cases:**
- Delete operations
- Hiding content
- Clearing notifications

**Examples:**
```csharp
// Delete todo item
public static async Task<IResult> DeleteTodo(
    HttpContext context,
    IRxDriver rxDriver,
    int id)
{
    await db.Todos.Where(x => x.Id == id).ExecuteDeleteAsync();

    return await rxDriver
        .With(context)
        .RemoveElement($"todo-{id}")
        .AddTriggerToast("Deleted!", ToastType.Success)
        .Render();
}

// Close notification
return await rxDriver
    .With(context)
    .RemoveElement("notification-banner")
    .Render();
```

**Multiple Operations:**
```csharp
// Delete multiple items
return await rxDriver
    .With(context)
    .RemoveElement("item-1")
    .RemoveElement("item-2")
    .RemoveElement("item-3")
    .AddTriggerToast("3 items deleted")
    .Render();
```

---

### Server Triggers

Triggers instruct the client to perform actions after processing fragments.

#### AddTriggerCloseDialog()

**Purpose:** Closes an HTML `<dialog>` element.

**Signature:**
```csharp
IRxResponseBuilder AddTriggerCloseDialog(
    string dialogId,
    string? onCloseData = null,
    string? resetFormId = null
)
```

**Parameters:**
- `dialogId` - ID of `<dialog>` to close
- `onCloseData` - Optional data to pass to close handler
- `resetFormId` - Optional form ID to reset after closing

**Examples:**
```csharp
// Simple dialog close
return await rxDriver
    .With(context)
    .AddTriggerCloseDialog("edit-dialog")
    .Render();

// Close dialog and reset form
return await rxDriver
    .With(context)
    .AddTriggerCloseDialog("edit-dialog", resetFormId: "edit-form")
    .Render();

// Pass data to close handler
return await rxDriver
    .With(context)
    .AddTriggerCloseDialog("confirm-dialog", onCloseData: "cancelled")
    .Render();
```

**Client-Side (HTML):**
```html
<dialog id="edit-dialog">
    <form id="edit-form" data-rx-action="/update" data-rx-method="PUT">
        <input name="title">
        <button type="submit">Save</button>
    </form>
</dialog>
```

**Complete Example:**
```csharp
public static async Task<IResult> UpdateUser(
    HttpContext context,
    IRxDriver rxDriver,
    int id,
    UserModel user)
{
    await db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(
        u => u.SetProperty(x => x.Name, user.Name)
    );

    var updated = await db.Users.FindAsync(id);

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(updated!, $"user-{id}", FragmentMergeStrategyType.Morph)
        .AddTriggerCloseDialog("edit-user-dialog", resetFormId: "edit-form")
        .AddTriggerToast("User updated!", ToastType.Success)
        .Render();
}
```

#### AddTriggerFocusElement()

**Purpose:** Sets focus to a specific element.

**Signature:**
```csharp
IRxResponseBuilder AddTriggerFocusElement(
    string elementId,
    bool positionCursorEnd = false
)
```

**Parameters:**
- `elementId` - ID of element to focus
- `positionCursorEnd` - If true, moves cursor to end of input value

**Use Cases:**
- Focus first field after error
- Focus search box after modal opens
- Return cursor to input after save

**Examples:**
```csharp
// Focus username field
return await rxDriver
    .With(context)
    .AddFragment<LoginForm>("form-container")
    .AddTriggerFocusElement("username")
    .Render();

// Focus and move cursor to end
return await rxDriver
    .With(context)
    .AddFragment<EditComment, CommentModel>(comment, "comment-123", FragmentMergeStrategyType.Morph)
    .AddTriggerFocusElement("comment-input", positionCursorEnd: true)
    .Render();

// Focus after validation error
return await rxDriver
    .With(context)
    .AddFragment<ErrorMessage>("error-container")
    .AddTriggerFocusElement("email-field")
    .AddTriggerToast("Invalid email", ToastType.Error)
    .Render();
```

**Client-Side:**
```html
<input id="username" type="text" placeholder="Username">
<input id="email-field" type="email" placeholder="Email">
```

#### AddTriggerSetState()

**Purpose:** Sets a state value in browser storage.

**Signature:**
```csharp
IRxResponseBuilder AddTriggerSetState(
    string key,
    string value,
    MetadataScope scope = MetadataScope.Session,
    bool updateUrl = false
)
```

**Parameters:**
- `key` - Storage key name
- `value` - Value to store
- `scope` - `Session` (sessionStorage) or `Persistent` (localStorage)
- `updateUrl` - If true, also updates URL query parameters

**Use Cases:**
- Filter persistence
- User preferences
- Pagination state
- Sort order

**Examples:**
```csharp
// Set filter state
return await rxDriver
    .With(context)
    .AddFragment<ProductList, IEnumerable<Product>>(products, "products")
    .AddTriggerSetState("filter", "active", MetadataScope.Session)
    .Render();

// Set with URL sync
return await rxDriver
    .With(context)
    .AddFragment<Results>("results")
    .AddTriggerSetState("page", "2", MetadataScope.Session, updateUrl: true)
    .Render();
// URL becomes: /products?page=2

// Persistent preference
return await rxDriver
    .With(context)
    .AddTriggerSetState("theme", "dark", MetadataScope.Persistent)
    .Render();
```

**Client Retrieval:**

Client can include state in requests:
```html
<button
  data-rx-action="/search"
  data-rx-include-state='["filter", "page"]'>
  Search
</button>
```

Server receives as query parameters:
```csharp
public static async Task<IResult> Search(
    HttpContext context,
    IRxDriver rxDriver,
    string? filter = null,  // ← From state
    int page = 1)           // ← From state
{
    // Use filter and page
}
```

#### AddTriggerSetStateBatch()

**Purpose:** Sets multiple state values at once.

**Signature:**
```csharp
IRxResponseBuilder AddTriggerSetStateBatch(
    Dictionary<string, string> state,
    MetadataScope scope,
    bool updateUrl = false
)
```

**Use Cases:**
- Setting multiple filters
- Bulk preference updates
- Resetting state

**Example:**
```csharp
// Set multiple filters
return await rxDriver
    .With(context)
    .AddFragment<Results>("results")
    .AddTriggerSetStateBatch(
        new Dictionary<string, string> {
            { "filter", "active" },
            { "sort", "date" },
            { "page", "1" }
        },
        MetadataScope.Session,
        updateUrl: true
    )
    .Render();
// URL: /products?filter=active&sort=date&page=1
```

**Clear State:**
```csharp
// Set to empty string to remove
return await rxDriver
    .With(context)
    .AddTriggerSetStateBatch(
        new Dictionary<string, string> {
            { "filter", "" },
            { "sort", "" }
        },
        MetadataScope.Session,
        updateUrl: true
    )
    .Render();
```

#### AddTriggerToast()

**Purpose:** Displays a toast notification.

**Signature:**
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
- `message` - Text to display
- `type` - `Info`, `Success`, `Warning`, `Error`
- `duration` - Milliseconds before auto-dismiss (0 = permanent)
- `verticalPosition` - `Top`, `Center`, `Bottom`
- `horizontalPosition` - `Left`, `Middle`, `Right`
- `clickToDismiss` - Allow clicking to dismiss

**Examples:**
```csharp
// Simple success toast
return await rxDriver
    .With(context)
    .AddFragment<UserCard>(user, "user-123", FragmentMergeStrategyType.Swap)
    .AddTriggerToast("User updated!")
    .Render();

// Error toast (5 seconds, bottom-left)
return await rxDriver
    .With(context)
    .AddTriggerToast(
        "Failed to save!",
        ToastType.Error,
        duration: 5000,
        verticalPosition: ToastVerticalPosition.Bottom,
        horizontalPosition: ToastHorizontalPosition.Left
    )
    .Render();

// Warning toast (permanent, must click)
return await rxDriver
    .With(context)
    .AddTriggerToast(
        "Your session will expire in 5 minutes",
        ToastType.Warning,
        duration: 0,  // ← Stays until clicked
        clickToDismiss: true
    )
    .Render();

// Info toast (center screen)
return await rxDriver
    .With(context)
    .AddTriggerToast(
        "Loading more items...",
        ToastType.Info,
        verticalPosition: ToastVerticalPosition.Center,
        horizontalPosition: ToastHorizontalPosition.Middle
    )
    .Render();
```

**Toast Positioning:**

```
┌─────────────────────────────────┐
│ Top-Left   Top-Center  Top-Right│
│                                  │
│                                  │
│Center-Left  Center   Center-Right│
│                                  │
│                                  │
│Bottom-Left Bottom-Ctr Bottom-Right│
└─────────────────────────────────┘
```

**Multiple Toasts:**
```csharp
return await rxDriver
    .With(context)
    .AddTriggerToast("Item 1 saved", ToastType.Success)
    .AddTriggerToast("Item 2 saved", ToastType.Success)
    .AddTriggerToast("Item 3 saved", ToastType.Success)
    .Render();
// All 3 toasts display simultaneously, stacked vertically
```

---

### Rendering

#### Render()

**Purpose:** Executes the builder and returns the response.

**Signature:**
```csharp
Task<IResult> Render(
    bool ignoreActiveElementValueOnMorph = false,
    CancellationToken cancellationToken = default
)
```

**Parameters:**
- `ignoreActiveElementValueOnMorph` - When true, preserves focused input value during morph
- `cancellationToken` - Cancellation token

**Returns:** `IResult` for ASP.NET Core

**Behavior:**
1. Renders all fragments in parallel (Task.WhenAll)
2. Constructs response headers (rx-merge, rx-trigger-*)
3. Returns appropriate response type (fragments or full page)

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

// With cancellation
return await rxDriver
    .With(context)
    .AddFragment<LargeComponent>("target")
    .Render(cancellationToken: ct);
```

**Response Headers:**

```http
HTTP/1.1 200 OK
Content-Type: text/html; charset=utf-8
rx-merge: [{"target":"user-123","strategy":"swap"}]
rx-trigger-toast: {"message":"Saved!","type":"Success","duration":3500,"verticalPosition":"Top","horizontalPosition":"Right","clickToDismiss":true}
rx-trigger-focus-element: {"elementId":"username","positionCursorEnd":false}
```

**When to Call:**
- ✅ **Last method** in builder chain
- ✅ Always `await` the result
- ✅ Return from handler

---

## Fragment Merge Strategies

Merge strategies control how fragments update the DOM.

### Swap

**Behavior:** Replaces entire target element with fragment.

**Before:**
```html
<div id="target" class="card">
    <h3>Old Title</h3>
</div>
```

**Fragment:**
```html
<div id="target" class="card-updated">
    <h3>New Title</h3>
</div>
```

**After:**
```html
<div id="target" class="card-updated">
    <h3>New Title</h3>
</div>
```

**Use Cases:**
- ✅ Replacing entire card/item
- ✅ Complete component refresh
- ✅ When element attributes change

**Example:**
```csharp
.AddFragment<UserCard, UserModel>(user, "user-123", FragmentMergeStrategyType.Swap)
```

### SwapInner

**Behavior:** Replaces inner HTML only, preserves element itself.

**Before:**
```html
<div id="target" class="container">
    <h3>Old Content</h3>
</div>
```

**Fragment:**
```html
<h3>New Content</h3>
<p>More stuff</p>
```

**After:**
```html
<div id="target" class="container">
    <h3>New Content</h3>
    <p>More stuff</p>
</div>
```

**Use Cases:**
- ✅ Updating content but keeping wrapper
- ✅ Preserving element ID and classes
- ✅ When target has event listeners

**Example:**
```csharp
.AddFragment<ContentOnly>("container", FragmentMergeStrategyType.SwapInner)
```

### AppendAfterBegin

**Behavior:** Inserts fragment as first child.

**Before:**
```html
<div id="list">
    <div>Item 2</div>
    <div>Item 3</div>
</div>
```

**Fragment:**
```html
<div>Item 1</div>
```

**After:**
```html
<div id="list">
    <div>Item 1</div>  ← New
    <div>Item 2</div>
    <div>Item 3</div>
</div>
```

**Use Cases:**
- ✅ Prepending to list (newest first)
- ✅ Adding notifications at top
- ✅ Reverse chronological order

**Example:**
```csharp
.AddFragment<TodoItem, TodoModel>(newTodo, "todo-list", FragmentMergeStrategyType.AppendAfterBegin)
```

### AppendBeforeEnd

**Behavior:** Inserts fragment as last child.

**Before:**
```html
<div id="list">
    <div>Item 1</div>
    <div>Item 2</div>
</div>
```

**Fragment:**
```html
<div>Item 3</div>
```

**After:**
```html
<div id="list">
    <div>Item 1</div>
    <div>Item 2</div>
    <div>Item 3</div>  ← New
</div>
```

**Use Cases:**
- ✅ Appending to list (oldest first)
- ✅ Infinite scroll (load more)
- ✅ Chronological order

**Example:**
```csharp
.AddFragment<TodoItem, TodoModel>(newTodo, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd)
```

### AppendAfterEnd

**Behavior:** Inserts fragment after target element.

**Before:**
```html
<div id="target">Target</div>
<div>Next</div>
```

**Fragment:**
```html
<div>New</div>
```

**After:**
```html
<div id="target">Target</div>
<div>New</div>  ← Inserted here
<div>Next</div>
```

**Use Cases:**
- ✅ Adding sibling elements
- ✅ Dynamic form fields
- ✅ Expanding content

**Example:**
```csharp
.AddFragment<ExtraField>("last-field", FragmentMergeStrategyType.AppendAfterEnd)
```

### AppendBeforeBegin

**Behavior:** Inserts fragment before target element.

**Before:**
```html
<div>Prev</div>
<div id="target">Target</div>
```

**Fragment:**
```html
<div>New</div>
```

**After:**
```html
<div>Prev</div>
<div>New</div>  ← Inserted here
<div id="target">Target</div>
```

**Use Cases:**
- ✅ Inserting before specific element
- ✅ Adding headers dynamically

**Example:**
```csharp
.AddFragment<Alert>("main-content", FragmentMergeStrategyType.AppendBeforeBegin)
```

### Morph

**Behavior:** Intelligently updates DOM, preserving state where possible.

**Uses:** [Idiomorph](https://github.com/bigskysoftware/idiomorph) algorithm

**Before:**
```html
<div id="form">
    <input id="name" value="John" />  ← User is typing here
    <span>Old status</span>
</div>
```

**Fragment:**
```html
<div id="form">
    <input id="name" value="Jane" />  ← Server says "Jane"
    <span>New status</span>
</div>
```

**After:**
```html
<div id="form">
    <input id="name" value="John" />  ← Preserved! (user still typing)
    <span>New status</span>  ← Updated
</div>
```

**Morphing Rules:**
- **Preserves:**
  - Focused element state
  - Input values (if focused)
  - Event listeners
  - Scroll position
  - CSS transitions

- **Updates:**
  - Attributes
  - Text content
  - Non-focused elements
  - Classes

**Use Cases:**
- ✅ Forms while user is editing
- ✅ Real-time collaboration
- ✅ Live updates without disruption
- ✅ Preserving animations

**Example:**
```csharp
// Update form but keep user's input
.AddFragment<EditForm, Model>(model, "edit-form", FragmentMergeStrategyType.Morph)
```

**Force Overwrite (ignoreActiveElementValueOnMorph):**
```csharp
// Server value always wins (even if focused)
.AddFragment<Form, Model>(model, "form", FragmentMergeStrategyType.Morph)
.Render(ignoreActiveElementValueOnMorph: false)  // ← Default behavior

// Preserve user input even on morph
.Render(ignoreActiveElementValueOnMorph: true)
```

---

## Server-Sent Events (SSE)

### RenderSse()

**Purpose:** Streams fragments and triggers to the client in real-time.

**Signature:**
```csharp
IResult RenderSse<TModel>(
    IAsyncEnumerable<TModel> models,
    Func<TModel, IRxResponseBuilder, Task> configureEvent,
    string eventType = "rx-server-sent-event",
    CancellationToken cancellationToken = default
)
```

**Parameters:**
- `models` - Async stream of models to send
- `configureEvent` - Callback to build response for each model
- `eventType` - SSE event type name (for client filtering)
- `cancellationToken` - Stops stream when cancelled

**Complete Example:**

**Server (Handler):**
```csharp
public static IResult StreamNotifications(
    HttpContext context,
    IRxDriver rxDriver,
    NotificationService notifications,
    CancellationToken ct)
{
    var userId = context.User.FindFirst("id")?.Value ?? "anonymous";

    return rxDriver
        .With(context)
        .RenderSse(
            notifications.GetUserNotificationsAsync(userId, ct),
            async (notification, builder) => {
                builder
                    .AddFragment<NotificationCard, Notification>(
                        notification,
                        "notification-area",
                        FragmentMergeStrategyType.AppendAfterBegin
                    )
                    .AddTriggerToast(
                        notification.Message,
                        ToastType.Info,
                        duration: 5000
                    )
                    .AddTriggerSetState("unread-count", notification.UnreadCount.ToString());

                await Task.CompletedTask;
            },
            eventType: "notification",
            cancellationToken: ct
        );
}
```

**Client (HTML):**
```html
<div
  id="notification-stream"
  data-rx-sse-connect="/stream/notifications"
  data-rx-sse-events="notification">
</div>

<div id="notification-area">
  <!-- Notifications appear here -->
</div>
```

**Broadcast Service Pattern:**

See [CLAUDE.md SSE Broadcasting](../../CLAUDE.md#sse-broadcasting-pattern) for complete broadcast pattern.

**Example with RxSseBroadcastService:**
```csharp
// Define metadata (simple record)
public record SseMetadata {
    public string? SubscriberId { get; init; }
    public string? TenantId { get; init; }
}

// SSE Stream Handler
public static IResult StreamTodos(
    HttpContext context,
    [FromQuery(Name = "rx-instance-id")] string instanceId,
    IRxDriver rxDriver,
    RxSseBroadcastService<TodoModel, SseMetadata> broadcast,
    CancellationToken ct)
{
    var tenantId = context.User.FindFirst("TenantId")!.Value;

    // Subscribe with filter (examines broadcaster metadata)
    broadcast.Subscribe(
        instanceId,
        filter: meta =>
            meta?.TenantId == tenantId &&
            meta?.SubscriberId != instanceId
    );
    ct.Register(() => broadcast.Unsubscribe(instanceId));

    return rxDriver
        .With(context)
        .RenderSse(
            broadcast.GetUpdates(instanceId, ct),
            async (todo, builder) => {
                builder
                    .AddFragment<TodoItem, TodoModel>(todo, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd)
                    .AddTriggerToast("New todo added!", ToastType.Success);
                await Task.CompletedTask;
            },
            eventType: "todo-update",
            cancellationToken: ct
        );
}

// Broadcast with metadata (for subscriber filters):
public static async Task<IResult> CreateTodo(
    HttpContext context,
    [FromQuery(Name = "rx-instance-id")] string instanceId,
    IRxDriver rxDriver,
    RxSseBroadcastService<TodoModel, SseMetadata> broadcast,
    TodoModel todo)
{
    var tenantId = context.User.FindFirst("TenantId")!.Value;
    await db.Todos.AddAsync(todo);
    await db.SaveChangesAsync();

    // Broadcast with metadata (subscribers' filters examine this)
    await broadcast.BroadcastUpdate(
        todo,
        new SseMetadata { SubscriberId = instanceId, TenantId = tenantId }
    );

    return await rxDriver
        .With(context)
        .AddFragment<TodoItem, TodoModel>(todo, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd)
        .Render();
}
```

**SSE Response Format:**
```
event: todo-update
data: {"merge":[{"target":"todo-list","strategy":"beforeend"}],"fragments":"<template>...</template>","toast":{"message":"New todo added!","type":"Success","duration":3500,"verticalPosition":"Top","horizontalPosition":"Right","clickToDismiss":true}}
```

---

## Common Patterns

### CRUD Operations

#### Create
```csharp
public static async Task<IResult> CreateUser(
    HttpContext context,
    IRxDriver rxDriver,
    UserModel user)
{
    var created = await db.Users.AddAsync(user);
    await db.SaveChangesAsync();

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(
            created.Entity,
            "user-list",
            FragmentMergeStrategyType.AppendAfterBegin  // ← Add to top of list
        )
        .AddTriggerCloseDialog("create-dialog", resetFormId: "create-form")
        .AddTriggerToast("User created!", ToastType.Success)
        .Render();
}
```

#### Read (Single Item)
```csharp
public static async Task<IResult> GetUser(
    HttpContext context,
    IRxDriver rxDriver,
    int id)
{
    var user = await db.Users.FindAsync(id);

    if (user == null) {
        return await rxDriver
            .With(context)
            .AddFragment<NotFound>("content")
            .AddTriggerToast("User not found", ToastType.Error)
            .Render();
    }

    return await rxDriver
        .With(context)
        .AddFragment<UserDetails, UserModel>(user, "content", FragmentMergeStrategyType.Swap)
        .Render();
}
```

#### Read (List)
```csharp
public static async Task<IResult> GetUsers(
    HttpContext context,
    IRxDriver rxDriver,
    string? search = null,
    int page = 1)
{
    var users = await db.Users
        .Where(u => search == null || u.Name.Contains(search))
        .Skip((page - 1) * 20)
        .Take(20)
        .ToListAsync();

    return await rxDriver
        .With(context)
        .AddFragment<UserList, IEnumerable<UserModel>>(
            users,
            "user-list",
            page == 1 ? FragmentMergeStrategyType.Swap : FragmentMergeStrategyType.AppendBeforeEnd  // ← Replace first page, append rest
        )
        .AddTriggerSetState("page", page.ToString(), updateUrl: true)
        .Render();
}
```

#### Update
```csharp
public static async Task<IResult> UpdateUser(
    HttpContext context,
    IRxDriver rxDriver,
    int id,
    UserModel user)
{
    await db.Users
        .Where(u => u.Id == id)
        .ExecuteUpdateAsync(u => u
            .SetProperty(x => x.Name, user.Name)
            .SetProperty(x => x.Email, user.Email)
        );

    var updated = await db.Users.FindAsync(id);

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(
            updated!,
            $"user-{id}",
            FragmentMergeStrategyType.Morph  // ← Preserve state during update
        )
        .AddTriggerCloseDialog("edit-dialog")
        .AddTriggerToast("User updated!", ToastType.Success)
        .Render();
}
```

#### Delete
```csharp
public static async Task<IResult> DeleteUser(
    HttpContext context,
    IRxDriver rxDriver,
    int id)
{
    await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync();

    return await rxDriver
        .With(context)
        .RemoveElement($"user-{id}")  // ← Remove from DOM
        .AddTriggerCloseDialog("confirm-delete-dialog")
        .AddTriggerToast("User deleted", ToastType.Success)
        .Render();
}
```

### Multi-Fragment Updates

```csharp
// Update multiple parts of page simultaneously
public static async Task<IResult> ProcessOrder(
    HttpContext context,
    IRxDriver rxDriver,
    int orderId)
{
    var order = await ProcessOrderAsync(orderId);
    var stats = await GetStatsAsync();
    var recentOrders = await GetRecentOrdersAsync();

    return await rxDriver
        .With(context)
        .AddFragment<OrderDetails, OrderModel>(order, "order-details", FragmentMergeStrategyType.Swap)
        .AddFragment<StatsWidget, StatsModel>(stats, "stats", FragmentMergeStrategyType.Morph)
        .AddFragment<RecentOrders, List<Order>>(recentOrders, "recent", FragmentMergeStrategyType.Swap)
        .AddTriggerToast("Order processed!")
        .AddTriggerSetState("last-order", orderId.ToString())
        .Render();
}
```

### Conditional Rendering

```csharp
public static async Task<IResult> HandleAction(
    HttpContext context,
    IRxDriver rxDriver,
    int id)
{
    var result = await TryProcessAsync(id);
    var builder = rxDriver.With(context);

    if (result.Success) {
        builder
            .AddFragment<SuccessMessage>("result", FragmentMergeStrategyType.Swap)
            .AddTriggerToast("Success!", ToastType.Success);
    } else {
        builder
            .AddFragment<ErrorMessage, string>(result.Error, "result", FragmentMergeStrategyType.Swap)
            .AddTriggerToast(result.Error, ToastType.Error)
            .AddTriggerFocusElement("retry-button");
    }

    return await builder.Render();
}
```

### Pagination

```csharp
public static async Task<IResult> GetPage(
    HttpContext context,
    IRxDriver rxDriver,
    int page = 1,
    int pageSize = 20)
{
    var items = await db.Items
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    var totalPages = (await db.Items.CountAsync() + pageSize - 1) / pageSize;

    return await rxDriver
        .With(context)
        .AddFragment<ItemList, IEnumerable<Item>>(items, "items", FragmentMergeStrategyType.Swap)
        .AddFragment<Pagination, (int Current, int Total)>(
            (page, totalPages),
            "pagination",
            FragmentMergeStrategyType.Swap
        )
        .AddTriggerSetState("page", page.ToString(), updateUrl: true)
        .Render();
}
```

---

## Best Practices

### ✅ DO

**Use Morph for Forms:**
```csharp
// Preserves user input during real-time validation
.AddFragment<EditForm, Model>(model, "form", FragmentMergeStrategyType.Morph)
```

**Return Toast Feedback:**
```csharp
// Always acknowledge user actions
.AddTriggerToast("Saved!", ToastType.Success)
```

**Close Dialogs After Success:**
```csharp
.AddTriggerCloseDialog("edit-dialog", resetFormId: "edit-form")
```

**Use Consistent Target IDs:**
```csharp
// Pattern: {type}-{id}
.AddFragment<UserCard>(user, $"user-{user.Id}", FragmentMergeStrategyType.Swap)
```

**Set State for Filters:**
```csharp
// Enables back/forward navigation
.AddTriggerSetState("filter", "active", updateUrl: true)
```

### ❌ DON'T

**Don't Forget to Await Render():**
```csharp
// ❌ WRONG
public static IResult BadHandler(IRxDriver rxDriver) {
    return rxDriver.With(context).AddFragment<Component>("target").Render();
    //                                                               ^^^^^^^ Missing await!
}

// ✅ CORRECT
public static async Task<IResult> GoodHandler(IRxDriver rxDriver) {
    return await rxDriver.With(context).AddFragment<Component>("target").Render();
    //     ^^^^^                                                          ^^^^^^^ Await!
}
```

**Don't Use Swap When Morph is Better:**
```csharp
// ❌ BAD: User loses focus/input
.AddFragment<Form, Model>(model, "form", FragmentMergeStrategyType.Swap)

// ✅ GOOD: Preserves user's edits
.AddFragment<Form, Model>(model, "form", FragmentMergeStrategyType.Morph)
```

**Don't Forget Target IDs:**
```csharp
// ❌ BAD: No target ID in component
<div class="user-card">...</div>

// ✅ GOOD: ID for targeting
<div id="user-@Model.Id" class="user-card">...</div>
```

**Don't Return Without Render():**
```csharp
// ❌ WRONG: Builder not executed
return rxDriver.With(context).AddFragment<Component>("target");

// ✅ CORRECT: Render() returns IResult
return await rxDriver.With(context).AddFragment<Component>("target").Render();
```

---

## Troubleshooting

### Fragment Not Updating

**Check:**
1. ✅ Target ID exists in DOM
2. ✅ Target ID matches exactly (case-sensitive)
3. ✅ Component renders without errors
4. ✅ Merge strategy is appropriate

**Debug:**
```csharp
// Add toast to confirm response sent
.AddFragment<Component>("target")
.AddTriggerToast("Fragment sent")  // ← Should see this
.Render()
```

### Morph Overwrites User Input

**Problem:** Morph replaces focused input value

**Solution:**
```csharp
// Preserve active element value
.AddFragment<Form, Model>(model, "form", FragmentMergeStrategyType.Morph)
.Render(ignoreActiveElementValueOnMorph: true)
```

### Multiple Fragments Conflict

**Problem:** Two fragments target same element

**Example:**
```csharp
// ❌ CONFLICT
.AddFragment<Component1>("target", FragmentMergeStrategyType.Swap)      // ← Will be overwritten
.AddFragment<Component2>("target", FragmentMergeStrategyType.SwapInner) // ← Wins (last one)
```

**Solution:** Last one wins. Ensure distinct targets.

### Toast Not Showing

**Check:**
1. ✅ RazorX.js initialized
2. ✅ Message not empty
3. ✅ Duration not 0 (unless intended)
4. ✅ CSS classes configured

**Debug console:**
```javascript
// Check if toast triggered
window.addEventListener('rx-toast', (e) => {
    console.log('Toast:', e.detail);
});
```

### SSE Connection Drops

**Check:**
1. ✅ Server keeps connection open
2. ✅ Returns `Content-Type: text/event-stream`
3. ✅ Client has `data-rx-sse-connect` attribute
4. ✅ CancellationToken passed and honored

**Server must yield control:**
```csharp
// ❌ WRONG: Blocks thread
while (!ct.IsCancellationRequested) {
    // Synchronous work
}

// ✅ CORRECT: Async enumerable
async IAsyncEnumerable<T> GetUpdates([EnumeratorCancellation] CancellationToken ct) {
    while (!ct.IsCancellationRequested) {
        var item = await GetNextAsync();
        yield return item;
    }
}
```

---

## Summary

### Core APIs

| API | Purpose | Returns |
|-----|---------|---------|
| `IRxDriver.With()` | Start building response | `IRxResponseBuilder` |
| `IRxDriver.RenderPage<>()` | Render full page | `Task<IResult>` |
| `builder.AddFragment<>()` | Add component | `IRxResponseBuilder` |
| `builder.RemoveElement()` | Remove element | `IRxResponseBuilder` |
| `builder.AddTrigger*()` | Client instruction | `IRxResponseBuilder` |
| `builder.Render()` | Execute and return | `Task<IResult>` |
| `builder.RenderSse()` | Stream events | `IResult` |

### Merge Strategies Quick Reference

| Strategy | Use When |
|----------|----------|
| **Swap** | Replacing entire element |
| **SwapInner** | Updating content, keeping wrapper |
| **AppendAfterBegin** | Prepending to list |
| **AppendBeforeEnd** | Appending to list |
| **AppendAfterEnd** | Adding after element |
| **AppendBeforeBegin** | Adding before element |
| **Morph** | Preserving state during update |

---

## Next Steps

- **[Client Attributes Guide](./CLIENT_ATTRIBUTES_GUIDE.md)** - Learn data-rx-* attributes
- **[CLAUDE.md](../../CLAUDE.md)** - Complete framework architecture
- **[RazorX.Examples](../../RazorX.Examples)** - Working code samples
- **[AOT Consumer Guide](./AOT_CONSUMER_GUIDE.md)** - Native AOT compilation

---

**Questions?** Check the [RazorX.Examples](../../RazorX.Examples) project for complete working implementations of all patterns.
