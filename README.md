# RazorX.Framework

A Server-Driven User Interface (SDUI) framework for ASP.NET Core that implements HATEOAS principles through HTML-over-the-wire. RazorX uses Razor components for server-side HTML templating and a TypeScript client library for DOM manipulation.

## Table of Contents

- [Installation](#installation)
- [Setup and Configuration](#setup-and-configuration)
- [Server API Reference](#server-api-reference)
- [Client Attributes Reference](#client-attributes-reference)
- [Server-Sent Events](#server-sent-events)
- [AOT Compilation](#aot-compilation)
- [Common Patterns](#common-patterns)

---

## Installation

Install via NuGet:

```bash
dotnet add package RazorX.Framework
```

Or add to your `.csproj`:

```xml
<PackageReference Include="RazorX.Framework" Version="1.0.0" />
```

The package copies `razorx.js` and `razorx.css` to your `wwwroot` folder during build.

---

## Setup and Configuration

### Service Registration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Required services
builder.Services.AddRxDriver();
builder.Services.AddAntiforgery();
builder.Services.AddRxAntiforgery();

// Optional: Configure form encoding
builder.Services.AddRxDriver(options => {
    options.AddJsonConverters = false; // Disable if not using JSON-encoded forms
});

// Optional: Custom antiforgery cookie
builder.Services.AddRxAntiforgery(options => {
    options.RequestVerificationTokenCookieName = "MyToken";
});
```

### Middleware Configuration

```csharp
var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseRxAntiforgeryCookie();

// Option 1: Auto-discover handlers (uses assembly scanning)
app.MapGroup(string.Empty).MapRoutes();

// Option 2: Manual registration (recommended for AOT)
var routes = app.MapGroup(string.Empty);
new HomeHandler().MapRoutes(routes);
new UserHandler().MapRoutes(routes);

app.Run();
```

### Root Layout Component

Create `App.razor`:

```razor
@implements IRootComponent

<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>My RazorX App</title>
    <link rel="stylesheet" href="/css/razorx.css" />
    <link rel="stylesheet" href="/css/app.css" />

    @if (HeadContent != null) {
        <DynamicComponent Type="HeadContent" Parameters="HeadContentParameters" />
    }
</head>
<body>
    <DynamicComponent Type="MainContent" Parameters="MainContentParameters" />

    <script type="module">
        import { razorx } from '/js/razorx.js';
        razorx.init({
            encodeRequestFormDataAsJson: true,
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

**Understanding DynamicComponent:**

The `<DynamicComponent>` tag renders components dynamically at runtime. When you call:

```csharp
rxDriver.RenderPage<App, ProductPage, ProductModel>(context, product);
```

The framework:
1. Sets `MainContent = typeof(ProductPage)`
2. Sets `MainContentParameters = new Dictionary { ["Model"] = product }`
3. The layout's `<DynamicComponent>` renders the type with the parameters
4. The page component receives the model via its `[Parameter] public TModel Model { get; set; }` property

This decouples your layout from specific page types while maintaining type safety.

---

## Server API Reference

### IRxDriver Interface

The main framework interface, injected via dependency injection (scoped per HTTP request).

#### With()

Creates a response builder for composing responses.

```csharp
IRxResponseBuilder With(HttpContext context)
```

**Returns:** `IRxResponseBuilder` for method chaining

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

#### RenderPage() - Full Page Rendering

Renders a complete HTML page with layout and content.

**Overload 1: Page with Model**

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

**Overload 2: Page without Model**

```csharp
Task<IResult> RenderPage<TRoot, TComponent>(
    HttpContext context,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where TComponent : IComponent
```

**Overload 3: Page with Custom Head (No Model) and Body with Model**

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

**Overload 4: Page with Custom Head and Body (No Models)**

```csharp
Task<IResult> RenderPage<TRoot, THead, TComponent>(
    HttpContext context,
    CancellationToken cancellationToken = default
)
where TRoot : IRootComponent
where THead : IComponent
where TComponent : IComponent
```

**Overload 5: Page with Separate Head and Body Models**

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

**Example with SEO metadata:**

```csharp
// Example - assumes ProductSeoModel and ProductPageModel exist
var headData = new ProductSeoModel {
    Title = $"{product.Name} - Buy Online",
    Description = product.ShortDescription,
    ImageUrl = product.PrimaryImage
};

var bodyData = new ProductPageModel {
    Product = product,
    Reviews = reviews  // Pass in reviews from your data layer
};

return await rxDriver.RenderPage<App, ProductHead, ProductPage, ProductSeoModel, ProductPageModel>(
    context,
    headData,
    bodyData
);
```

---

### IRxResponseBuilder Interface

Fluent API for building responses with fragments and triggers.

#### Fragment Management

**AddFragment<TComponent, TModel>()** - Add component with model

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
- `fragmentMergeStrategy` - How to update the DOM (default: Swap)

**Example:**

```csharp
return await rxDriver
    .With(context)
    .AddFragment<UserCard, UserModel>(user, "user-123", FragmentMergeStrategyType.Morph)
    .Render();
```

**AddFragment<TComponent>()** - Add component without model

```csharp
IRxResponseBuilder AddFragment<TComponent>(
    string targetId,
    FragmentMergeStrategyType fragmentMergeStrategy = FragmentMergeStrategyType.Swap
)
where TComponent : IComponent
```

**RemoveElement()** - Remove element from DOM

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

#### Server Triggers

**AddTriggerToast()** - Display toast notification

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
// Success toast (default: top-right, 3.5s)
.AddTriggerToast("User created", ToastType.Success)

// Error toast (bottom-left, 5s)
.AddTriggerToast(
    "Failed to save",
    ToastType.Error,
    duration: 5000,
    verticalPosition: ToastVerticalPosition.Bottom,
    horizontalPosition: ToastHorizontalPosition.Left
)

// Permanent warning (stays until clicked)
.AddTriggerToast(
    "Session expiring soon",
    ToastType.Warning,
    duration: 0,
    clickToDismiss: true
)
```

**AddTriggerFocusElement()** - Set focus to element

```csharp
IRxResponseBuilder AddTriggerFocusElement(
    string elementId,
    bool positionCursorEnd = false
)
```

**Parameters:**
- `elementId` - ID of element to focus
- `positionCursorEnd` - If true, moves cursor to end of input value

**Examples:**

```csharp
// Focus username field
.AddTriggerFocusElement("username")

// Focus and position cursor at end
.AddTriggerFocusElement("comment-input", positionCursorEnd: true)
```

**AddTriggerSetState()** - Set state in browser storage

```csharp
IRxResponseBuilder AddTriggerSetState(
    string key,
    string value,
    MetadataScope scope = MetadataScope.Session,
    bool updateUrl = false
)
```

**Parameters:**
- `key` - Storage key name (alphanumeric, hyphens, underscores only)
- `value` - Value to store
- `scope` - `Session` (sessionStorage) or `Persistent` (localStorage)
- `updateUrl` - If true, also updates URL query parameters

**Examples:**

```csharp
// Set filter in session storage
.AddTriggerSetState("filter", "active", MetadataScope.Session)

// Set with URL sync
.AddTriggerSetState("page", "2", MetadataScope.Session, updateUrl: true)
// URL becomes: /products?page=2

// Persistent preference
.AddTriggerSetState("theme", "dark", MetadataScope.Persistent)
```

**AddTriggerSetStateBatch()** - Set multiple state values

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
    updateUrl: true
)
// URL: /products?filter=active&sort=date&page=1
```

**AddTriggerCloseDialog()** - Close HTML dialog

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
.AddTriggerCloseDialog("edit-dialog")

// Close and reset form
.AddTriggerCloseDialog("edit-dialog", resetFormId: "edit-form")

// Pass data to close handler
.AddTriggerCloseDialog("confirm-dialog", onCloseData: "cancelled")
```

#### Rendering

**Render()** - Execute builder and return response

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
2. Sets response headers (rx-merge, rx-trigger-*)
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

// With cancellation
return await rxDriver
    .With(context)
    .AddFragment<LargeComponent>("target")
    .Render(cancellationToken: ct);
```

**RenderSse()** - Stream fragments via Server-Sent Events

```csharp
IResult RenderSse<TModel>(
    IAsyncEnumerable<TModel> models,
    Func<TModel, IRxResponseBuilder, Task> configureEvent,
    string eventType = "rx-server-sent-event",
    CancellationToken cancellationToken = default
)
```

**Parameters:**
- `models` - Async stream of data to send to client
- `configureEvent` - Callback to build response for each model
- `eventType` - SSE event type name for client filtering
- `cancellationToken` - Stops stream when cancelled

**Example:**

```csharp
return rxDriver.With(context).RenderSse(
    GetNotificationsAsync(userId, ct),
    async (notification, builder) => {
        builder
            .AddFragment<NotificationCard, Notification>(notification, "notifications", FragmentMergeStrategyType.AppendAfterBegin)
            .AddTriggerToast(notification.Message, ToastType.Info);
    },
    eventType: "notification",
    cancellationToken: ct
);
```

---

### Fragment Merge Strategies

Merge strategies control how fragments update the DOM.

#### Swap
Replaces entire target element with fragment.

```csharp
.AddFragment<UserCard, UserModel>(user, "user-123", FragmentMergeStrategyType.Swap)
```

**Use cases:**
- Complete element replacement
- Element attributes change

#### SwapInner
Replaces inner HTML only, preserves element itself.

```csharp
.AddFragment<ContentOnly>("container", FragmentMergeStrategyType.SwapInner)
```

**Use cases:**
- Update content but keep wrapper
- Preserve element ID and classes
- Element has event listeners

#### Morph
Intelligently updates DOM using [Idiomorph](https://github.com/bigskysoftware/idiomorph), preserving state where possible.

```csharp
.AddFragment<EditForm, Model>(model, "edit-form", FragmentMergeStrategyType.Morph)
```

**Preserves:**
- Focused element state
- Input values (if focused)
- Event listeners
- Scroll position
- CSS transitions

**Updates:**
- Attributes
- Text content
- Non-focused elements
- Classes

**Use cases:**
- Forms while user is editing
- Real-time collaboration
- Live updates without disruption

**Controlling morph behavior:**

```csharp
// Default: Preserve focused input values
.Render(ignoreActiveElementValueOnMorph: false)

// Force server value even if user is typing
.Render(ignoreActiveElementValueOnMorph: true)
```

#### AppendAfterBegin
Inserts fragment as first child.

```csharp
.AddFragment<TodoItem, TodoModel>(newTodo, "todo-list", FragmentMergeStrategyType.AppendAfterBegin)
```

**Use cases:**
- Prepend to list (newest first)
- Add notifications at top

#### AppendBeforeEnd
Inserts fragment as last child.

```csharp
.AddFragment<TodoItem, TodoModel>(newTodo, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd)
```

**Use cases:**
- Append to list (oldest first)
- Infinite scroll (load more)

#### AppendAfterEnd
Inserts fragment after target element.

```csharp
.AddFragment<ExtraField>("last-field", FragmentMergeStrategyType.AppendAfterEnd)
```

**Use cases:**
- Adding sibling elements
- Dynamic form fields

#### AppendBeforeBegin
Inserts fragment before target element.

```csharp
.AddFragment<Alert>("main-content", FragmentMergeStrategyType.AppendBeforeBegin)
```

**Use cases:**
- Inserting before specific element
- Adding headers dynamically

---

## Client Attributes Reference

### Request Configuration

#### data-rx-action
Specifies the URL to send the request to.

**Type:** `string` (URL or path)

**Required:** Yes

**Examples:**

```html
<!-- Relative path -->
<button data-rx-action="/api/users">Load Users</button>

<!-- Absolute URL -->
<button data-rx-action="https://api.example.com/data">Load External</button>

<!-- Dynamic path (from Razor) -->
<button data-rx-action="/api/users/@userId">Load User</button>
```

#### data-rx-method
Specifies the HTTP method for the request.

**Type:** `string`

**Valid Values:** `GET`, `POST`, `PUT`, `PATCH`, `DELETE`

**Default:**
- For `<form>` elements or elements inside forms: `POST`
- For all other elements: `GET`

**Examples:**

```html
<!-- Explicit DELETE -->
<button data-rx-action="/api/users/123" data-rx-method="DELETE">
  Delete User
</button>

<!-- PUT request (update resource) -->
<form data-rx-action="/api/users/123" data-rx-method="PUT">
    <input name="name">
    <button>Update</button>
</form>

<!-- GET form (search) -->
<form data-rx-action="/search" data-rx-method="GET">
    <input name="query">
    <button>Search</button>
</form>
```

#### data-rx-trigger
Defines when the request should be sent.

**Type:** `string` (event name) or `JSON` (special trigger configuration)

**Default:** `click` for buttons, `submit` for forms

**DOM Event Triggers:**

```html
<!-- Click event -->
<button data-rx-action="/api/click" data-rx-trigger="click">

<!-- Input event (real-time search) -->
<input data-rx-action="/search" data-rx-trigger="input">

<!-- Change event (dropdown selection) -->
<select data-rx-action="/filter" data-rx-trigger="change">

<!-- Form submission -->
<form data-rx-action="/submit" data-rx-trigger="submit">

<!-- Mouse hover -->
<div data-rx-action="/preview" data-rx-trigger="mouseover">

<!-- Focus -->
<input data-rx-action="/activate" data-rx-trigger="focus">
```

**Common Events:**
- `click` - User clicks element
- `input` - Input value changes (fires on every keystroke)
- `change` - Input value changes (fires on blur/selection)
- `submit` - Form submission
- `focus` - Element receives focus
- `blur` - Element loses focus
- `mouseover` / `mouseout` - Mouse enters/leaves element
- `keyup` / `keydown` - Keyboard events

**Special Triggers:**

**1. initialized** - Fires once when element added to DOM

Configuration:
```typescript
{
  "type": "initialized",
  "delay": number  // Optional delay in ms (default: 0)
}
```

Examples:
```html
<!-- Load immediately when page loads -->
<div data-rx-action="/api/stats"
     data-rx-trigger='{"type":"initialized"}'>
</div>

<!-- Load after 500ms delay -->
<div data-rx-action="/api/recommendations"
     data-rx-trigger='{"type":"initialized","delay":500}'>
</div>
```

**2. poll** - Fires repeatedly at intervals

Configuration:
```typescript
{
  "type": "poll",
  "interval": number  // Interval in ms (default: 1000)
}
```

Examples:
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

Note: Consider using Server-Sent Events instead of polling for better performance.

**3. revealed** - Fires when element enters viewport

Configuration:
```typescript
{
  "type": "revealed",
  "margin": string  // IntersectionObserver rootMargin (default: "0px")
}
```

Examples:
```html
<!-- Infinite scroll - load when bottom sentinel visible -->
<div id="load-more-sentinel"
     data-rx-action="/api/items/next/100"
     data-rx-trigger='{"type":"revealed"}'>
</div>

<!-- Load earlier (200px before visible) -->
<div data-rx-action="/api/images/page2"
     data-rx-trigger='{"type":"revealed","margin":"200px"}'>
</div>
```

---

### Request Behavior

#### data-rx-debounce
Delays request execution until user stops triggering events.

**Type:** `number` (milliseconds)

**Default:** `0` (no debounce)

**Use Cases:**
- Real-time search (wait for user to stop typing)
- Auto-save (wait for editing pause)
- Reducing server load

**Examples:**

```html
<!-- Search as user types (300ms delay) -->
<input type="search"
       data-rx-action="/search"
       data-rx-trigger="input"
       data-rx-debounce="300"
       placeholder="Search...">

<!-- Auto-save textarea (1 second after editing stops) -->
<textarea data-rx-action="/save-draft"
          data-rx-trigger="input"
          data-rx-debounce="1000">
</textarea>

<!-- Real-time validation (500ms) -->
<input type="email"
       data-rx-action="/validate-email"
       data-rx-trigger="input"
       data-rx-debounce="500">
```

**How It Works:**
```
User types: "h" → Timer starts (300ms)
User types: "he" → Timer resets (300ms)
User types: "hel" → Timer resets (300ms)
User types: "hello" → Timer resets (300ms)
User stops typing → Timer expires → Request sent
```

**Recommended Values:**
- Search: 300-500ms
- Auto-save: 1000-2000ms
- Validation: 500ms

#### data-rx-disable-in-flight
Disables element while request is in progress.

**Type:** Boolean (presence = true)

**Use Cases:**
- Prevent double-submission
- Disable buttons during save
- Prevent form re-submission

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
</form>

<!-- Combine with loading indicator -->
<button data-rx-action="/api/process"
        data-rx-disable-in-flight
        data-rx-loading-indicator="spinner">
  Process
  <span id="spinner" class="rx-loading-hidden">⏳</span>
</button>
```

**Behavior:**
- Element gets `disabled` attribute
- For forms, all child inputs are disabled
- Automatically re-enabled when response received
- Works with buttons, inputs, forms

#### data-rx-disable-queueing
Allows this element's requests to execute immediately without waiting in the queue.

**Type:** Boolean (presence = true)

**Default:** All requests use a single global queue and execute sequentially

RazorX uses a single, global request queue to execute all requests sequentially across the entire page. This ensures predictable order and prevents race conditions.

Both modes (queued and non-queued) always prevent duplicate requests for the same element.

**Examples:**

```html
<!-- DEFAULT: Waits in global queue -->
<button data-rx-action="/save-user">
  Save User
</button>

<button data-rx-action="/save-settings">
  Save Settings
</button>

<!-- User clicks both buttons quickly -->
<!-- → Save User runs first, Save Settings waits in queue -->
<!-- → Save Settings runs after Save User completes -->


<!-- WITH disable-queueing: Runs concurrently -->
<button data-rx-action="/update-sidebar"
        data-rx-disable-queueing>
  Update Sidebar
</button>

<button data-rx-action="/refresh-stats"
        data-rx-disable-queueing>
  Refresh Stats
</button>

<!-- User clicks both buttons quickly -->
<!-- → BOTH requests run concurrently (don't wait for each other) -->
```

**When to use:**
- Independent operations that can safely run in parallel
- Real-time widgets that shouldn't wait for form submissions
- Background refresh while user continues interacting

**When NOT to use:**
- Sequential operations where order matters
- Operations that modify shared state

#### data-rx-allow-event-default
Allows default browser behavior for the event.

**Type:** Boolean (presence = true)

**Default:** `false` (preventDefault() called)

**Examples:**

```html
<!-- Let checkbox state update before sending request -->
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

---

### State Management

#### data-rx-include-state
Includes stored state values in the request as query parameters.

**Type:** `string` (JSON array) or `string` (single key)

**Examples:**

```html
<!-- Include single state key -->
<button data-rx-action="/search"
        data-rx-include-state="filter">
  Search
</button>

<!-- Include multiple state keys -->
<button data-rx-action="/api/data"
        data-rx-include-state='["filter", "sort", "page"]'>
  Load Data
</button>

<!-- Include instance ID for SSE -->
<form data-rx-action="/submit"
      data-rx-include-state='["rx-instance-id"]'>
</form>
```

**How It Works:**

1. Server sets state via `AddTriggerSetState()`:
```csharp
return await rxDriver
    .With(context)
    .AddTriggerSetState("filter", "active", MetadataScope.Session, updateUrl: true)
    .Render();
```

2. Browser stores in sessionStorage or localStorage

3. Client includes in next request:
```http
GET /search?filter=active
```

**Storage Scopes:**
- `Session` → sessionStorage (cleared when tab closes)
- `Persistent` → localStorage (survives browser restart)

**Storage Priority:**
- Checks sessionStorage first
- Falls back to localStorage if key not in session
- Appends to URL query parameters

**Special State Keys:**
- `rx-instance-id` - Unique page instance ID (auto-generated)
- Custom keys - Set via server `AddTriggerSetState()`

**URL Synchronization:**

When `updateUrl: true`, state is also added to URL query params:
```
https://example.com/page?filter=active&sort=date
```

This enables:
- Shareable URLs
- Browser back/forward works correctly
- Bookmarking preserves state

---

### UI Feedback

#### data-rx-loading-indicator
Shows/hides element while request is in progress.

**Type:** `string` (element ID) or `boolean` (`false` to disable)

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

<!-- Disable indicator (inherit from parent) -->
<button data-rx-action="/api/action"
        data-rx-loading-indicator="false">
  No Indicator
</button>
```

**CSS Classes:**

RazorX toggles classes on the indicator element:

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

---

### Advanced Features

#### data-rx-delegate-action-to
Transfers `data-rx-action` and `data-rx-method` to another element.

**Type:** `string` (target element ID)

**Use Cases:**
- Dialog buttons that submit forms
- External submit buttons
- Composite UI patterns

**Examples:**

```html
<!-- Button outside form that submits it -->
<form id="user-form" data-rx-action="/api/users" data-rx-method="POST">
  <input name="name" placeholder="Name">
  <!-- No submit button inside form -->
</form>

<button data-rx-delegate-action-to="user-form">
  Save User
</button>

<!-- Dialog pattern -->
<dialog id="edit-dialog">
  <form id="edit-form" data-rx-action="/edit" data-rx-method="PUT">
    <textarea name="content"></textarea>
  </form>
  <footer>
    <button data-rx-delegate-action-to="edit-form">Save</button>
    <button onclick="this.closest('dialog').close()">Cancel</button>
  </footer>
</dialog>

<!-- Multiple actions for same form -->
<form id="draft-form" data-rx-action="/save-draft">
  <textarea name="content"></textarea>
</form>

<button data-rx-delegate-action-to="draft-form">
  Save Draft
</button>

<button data-rx-delegate-action-to="draft-form"
        data-rx-action="/publish"
        data-rx-method="POST">
  Publish Now
</button>
```

**How It Works:**

When button is clicked:
1. Find target element by ID
2. Copy `data-rx-action` and `data-rx-method` from target
3. Collect form data from target (if form)
4. Send request with target's configuration

#### File Uploads

**data-rx-file-upload-progress-id** - Progress element ID

```html
<form data-rx-action="/upload" data-rx-method="POST">
  <input type="file"
         name="file"
         data-rx-file-upload-progress-id="upload-progress">
  <button>Upload</button>
</form>

<progress id="upload-progress" value="0" max="100"></progress>
```

**data-rx-file-upload-timeout** - Upload timeout in milliseconds

```html
<!-- 60 second timeout -->
<input type="file"
       data-rx-action="/upload"
       data-rx-file-upload-timeout="60000">
```

**data-rx-file-upload-max-size** - Maximum file size in bytes

```html
<!-- 5MB max file size -->
<input type="file"
       data-rx-action="/upload"
       data-rx-file-upload-max-size="5242880">

<!-- 10MB with progress -->
<input type="file"
       data-rx-action="/upload"
       data-rx-file-upload-max-size="10485760"
       data-rx-file-upload-progress-id="progress">
```

**Complete Upload Example:**

```html
<form data-rx-action="/upload" data-rx-method="POST">
  <input type="file"
         name="file"
         data-rx-file-upload-max-size="10485760"
         data-rx-file-upload-timeout="30000"
         data-rx-file-upload-progress-id="progress"
         accept="image/*">

  <progress id="progress" value="0" max="100"></progress>
  <span id="progress-text">0%</span>

  <button type="submit">Upload Image</button>
</form>
```

---

### Server-Sent Events Attributes

#### data-rx-sse-connect
Establishes a Server-Sent Events connection to the specified URL.

**Type:** `string` (URL)

**Examples:**

```html
<!-- Basic SSE connection -->
<div data-rx-sse-connect="/stream"></div>

<!-- With instance ID tracking -->
<div data-rx-sse-connect="/stream"
     data-rx-include-state='["rx-instance-id"]'>
</div>
```

#### data-rx-sse-events
Filter which SSE event types to listen for.

**Type:** `string` (single event) or `JSON array` (multiple events)

**Default:** Listens to all events

**Examples:**

```html
<!-- Listen to single event type -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-events="user-update">
</div>

<!-- Listen to multiple event types -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-events='["user-update", "comment-added", "notification"]'>
</div>
```

#### data-rx-sse-connect-delay
Delays the SSE connection by the specified milliseconds.

**Type:** `number` (milliseconds)

**Default:** `0` (connect immediately)

**Example:**

```html
<!-- Connect after 500ms -->
<div data-rx-sse-connect="/stream"
     data-rx-sse-connect-delay="500">
</div>
```

**Connection Behavior:**
- Auto-reconnect with exponential backoff: 1s → 2s → 4s → 8s → 16s → 30s max
- State tracked via `data-sse-state` attribute: `"connected"` or `"error"`
- Automatic cleanup when element removed from DOM

---

## Server-Sent Events

### Basic SSE Stream

SSE enables real-time server-to-client updates over HTTP.

**Client Setup:**

```html
<div data-rx-sse-connect="/stream/notifications"></div>

<!-- Target element -->
<div id="notifications">
    <!-- Updates appear here -->
</div>
```

**Server Handler:**

```csharp
public static IResult StreamNotifications(
    HttpContext context,
    IRxDriver rxDriver,
    INotificationService notifications,
    CancellationToken ct)
{
    var userId = context.User.Identity?.Name ?? "anonymous";

    return rxDriver
        .With(context)
        .RenderSse(
            notifications.GetUserNotificationsAsync(userId, ct),
            async (notification, builder) => {
                builder
                    .AddFragment<NotificationCard, Notification>(
                        notification,
                        "notifications",
                        FragmentMergeStrategyType.AppendAfterBegin)
                    .AddTriggerToast(
                        $"New notification: {notification.Title}",
                        ToastType.Success,
                        3000)
                    .RemoveElement($"notification-{notification.ReadNotificationId}");
            },
            ct
        );
}
```

**Full Builder API Available:**

You can use all `IRxResponseBuilder` methods in SSE events:

```csharp
async (update, builder) => {
    // Multiple fragments
    builder
        .AddFragment<Component1, Data1Model>(data1, "target1", FragmentMergeStrategyType.Swap)
        .AddFragment<Component2, Data2Model>(data2, "target2", FragmentMergeStrategyType.Morph);

    // All triggers
    builder
        .AddTriggerToast("Update received", ToastType.Info)
        .AddTriggerFocusElement("input")
        .AddTriggerSetState("last-update", DateTime.UtcNow.ToString("o"), MetadataScope.Session)
        .AddTriggerCloseDialog("loading-dialog");

    // Remove elements
    builder.RemoveElement("old-notification");

    // Conditional logic
    if (update.IsUrgent) {
        builder.AddTriggerToast("URGENT", ToastType.Warning);
    }
}
```

### Multi-Client Broadcasting

For scenarios where multiple clients should receive the same updates (notifications, dashboards, collaborative editing), use `RxSseBroadcastService<TModel, TMetadata>`.

#### Define Metadata Type

Metadata can be any JSON-serializable type. No interface implementation required.

```csharp
// Simple record with the properties you need
public record TodoMetadata
{
    public string? SubscriberId { get; init; }
    public string? TenantId { get; init; }
}
```

#### Register Service

```csharp
// In Program.cs
builder.Services.AddSingleton(sp => {
    var logger = sp.GetRequiredService<ILogger<RxSseBroadcastService<TodoModel, TodoMetadata>>>();
    return new RxSseBroadcastService<TodoModel, TodoMetadata>(logger);
});
```

#### SSE Stream Handler

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

    // Subscribe with filter: only receive updates for my tenant, excluding my own updates
    broadcast.Subscribe(
        rxInstanceId,
        filter: meta => meta?.TenantId == tenantId && meta?.SubscriberId != rxInstanceId
    );

    ct.Register(() => broadcast.Unsubscribe(rxInstanceId));

    return rxDriver
        .With(context)
        .RenderSse(
            broadcast.GetUpdates(rxInstanceId, ct),
            async (todo, builder) => {
                builder.AddFragment<TodoCard, TodoModel>(todo, "list", FragmentMergeStrategyType.AppendBeforeEnd);
            },
            ct
        );
}
```

#### Broadcast with Metadata

Broadcasts include metadata that subscriber filters examine.

```csharp
public static async Task<IResult> UpdateTodo(
    HttpContext context,
    [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
    IRxDriver rxDriver,
    RxSseBroadcastService<TodoModel, TodoMetadata> broadcast,
    TodoModel todo,
    IYourRepository repository)
{
    var tenantId = context.User.FindFirst("TenantId")!.Value;

    // Save using your data layer
    await repository.SaveAsync(todo);

    // Broadcast with metadata - subscribers' filters decide who receives this
    await broadcast.BroadcastUpdate(
        todo,
        new TodoMetadata {
            TenantId = tenantId,
            SubscriberId = rxInstanceId
        }
    );

    return await rxDriver
        .With(context)
        .AddFragment<TodoCard, TodoModel>(todo, $"todo-{todo.Id}", FragmentMergeStrategyType.Swap)
        .Render();
}
```

#### Common Patterns

**Echo Suppression Only:**

```csharp
// Subscribe: Don't receive my own updates
broadcast.Subscribe(
    mySubscriberId,
    filter: meta => meta?.SubscriberId != mySubscriberId
);

// Broadcast: Include my subscriber ID
await broadcast.BroadcastUpdate(
    model,
    new Metadata { SubscriberId = rxInstanceId }
);
```

**Tenant Isolation:**

```csharp
// Subscribe: Only receive updates for my tenant
broadcast.Subscribe(
    subscriberId,
    filter: meta => meta?.TenantId == myTenantId
);

// Broadcast: Include tenant ID
await broadcast.BroadcastUpdate(
    model,
    new Metadata { TenantId = tenantId }
);
```

**Role-Based Broadcasting:**

```csharp
// Subscribe: Only receive updates for my role
broadcast.Subscribe(
    subscriberId,
    filter: meta => meta?.Role == myRole
);

// Broadcast: Include role
await broadcast.BroadcastUpdate(
    adminAlert,
    new Metadata { Role = "Admin" }
);
```

**Broadcast to All:**

```csharp
// Subscribe: No filter = receive everything
broadcast.Subscribe(subscriberId, filter: null);

// Broadcast: No metadata needed
await broadcast.BroadcastUpdate(globalAnnouncement);
```


---

## AOT Compilation

The framework supports Native AOT compilation for faster startup and smaller deployments.

### Requirements

#### 1. Route Handler Preservation (REQUIRED)

**Option A: Root Assembly Preservation (Simplest)**

```xml
<!-- In your .csproj file -->
<ItemGroup>
  <TrimmerRootAssembly Include="YourApplicationName" />
</ItemGroup>
```

**Option B: Manual Handler Registration (Most AOT-Friendly)**

```csharp
// Instead of this:
app.MapGroup(string.Empty).MapRoutes();

// Do this:
var routes = app.MapGroup(string.Empty);
new HomeHandler().MapRoutes(routes);
new UserHandler().MapRoutes(routes);
new ProductHandler().MapRoutes(routes);
```

#### 2. Component Type Preservation

Razor component types are automatically preserved by the Razor compiler. No action needed.

#### 3. Model Type Preservation

If you use complex models with components, ensure they're used in a way the trimmer can track:

```csharp
// This approach works well with trimming
public record UserModel(int Id, string Name);

public static async Task<IResult> GetUser(IRxDriver driver, HttpContext context, int id)
{
    var model = new UserModel(id, "John");
    return await driver
        .With(context)
        .AddFragment<UserCard, UserModel>(model, "user-container")
        .Render();
}
```

### Project Configuration for AOT

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- AOT Configuration -->
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>false</InvariantGlobalization>

    <!-- Optional: Trim aggressively -->
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>

    <!-- Optional: Single file output -->
    <PublishSingleFile>true</PublishSingleFile>

    <!-- Optional: Self-contained -->
    <SelfContained>true</SelfContained>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="RazorX.Framework" Version="1.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Preserve all types in your app -->
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

# Platform-specific builds
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
dotnet publish -c Release -r win-x64 -p:PublishAot=true
dotnet publish -c Release -r osx-arm64 -p:PublishAot=true
```

### Expected Results

**Binary Size:**

| Configuration | Size (Approx) |
|--------------|---------------|
| Normal .NET publish | ~80-100 MB |
| Trimmed publish | ~30-50 MB |
| AOT publish | ~15-25 MB |

**Startup Time:**

| Configuration | Cold Start |
|--------------|------------|
| Normal .NET | ~500-1000ms |
| Trimmed | ~300-600ms |
| AOT | ~50-150ms |

**Memory Usage:**
- Lower baseline memory (no JIT compiler)
- Faster warm-up (no JIT compilation phase)
- More predictable (all code pre-compiled)

### Verification Checklist

Before deploying your AOT application:

- [ ] Build completes successfully
- [ ] All route handlers are discovered
- [ ] All components render correctly
- [ ] Form submissions work (if using JSON-encoded forms)
- [ ] Database queries work (if using EF/Dapper)
- [ ] Custom models serialize/deserialize correctly
- [ ] Application starts quickly
- [ ] Binary size is acceptable
- [ ] All tests pass against AOT build

---

## Common Patterns

### CRUD Operations

#### Create

```csharp
// Example - assumes IYourRepository is injected
public static async Task<IResult> CreateUser(
    HttpContext context,
    IRxDriver rxDriver,
    UserModel user,
    IYourRepository repository)
{
    // Save using your data layer
    var created = await repository.CreateUserAsync(user);

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(
            created,
            "user-list",
            FragmentMergeStrategyType.AppendAfterBegin
        )
        .AddTriggerCloseDialog("create-dialog", resetFormId: "create-form")
        .AddTriggerToast("User created", ToastType.Success)
        .Render();
}
```

#### Read

```csharp
// Example - assumes IYourRepository is injected
public static async Task<IResult> GetUser(
    HttpContext context,
    IRxDriver rxDriver,
    int id,
    IYourRepository repository)
{
    // Fetch using your data layer
    var user = await repository.GetUserAsync(id);

    if (user == null) {
        return await rxDriver
            .With(context)
            .AddFragment<NotFound>("content", FragmentMergeStrategyType.Swap)
            .AddTriggerToast("User not found", ToastType.Error)
            .Render();
    }

    return await rxDriver
        .With(context)
        .AddFragment<UserDetails, UserModel>(user, "content", FragmentMergeStrategyType.Swap)
        .Render();
}
```

#### Update

```csharp
// Example - assumes IYourRepository is injected
public static async Task<IResult> UpdateUser(
    HttpContext context,
    IRxDriver rxDriver,
    int id,
    UserModel user,
    IYourRepository repository)
{
    // Update using your data layer
    var updated = await repository.UpdateUserAsync(id, user);

    return await rxDriver
        .With(context)
        .AddFragment<UserCard, UserModel>(
            updated,
            $"user-{id}",
            FragmentMergeStrategyType.Morph
        )
        .AddTriggerCloseDialog("edit-dialog")
        .AddTriggerToast("User updated", ToastType.Success)
        .Render();
}
```

#### Delete

```csharp
// Example - assumes IYourRepository is injected
public static async Task<IResult> DeleteUser(
    HttpContext context,
    IRxDriver rxDriver,
    int id,
    IYourRepository repository)
{
    // Delete using your data layer
    await repository.DeleteUserAsync(id);

    return await rxDriver
        .With(context)
        .RemoveElement($"user-{id}")
        .AddTriggerCloseDialog("confirm-delete-dialog")
        .AddTriggerToast("User deleted", ToastType.Success)
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

### Infinite Scroll

```html
<div id="items-container">
  <!-- Items rendered here -->
</div>

<!-- Sentinel element at bottom -->
<div id="load-more"
     data-rx-action="/items/next/20"
     data-rx-trigger='{"type":"revealed","margin":"200px"}'>
  Loading more...
</div>
```

### Auto-Save Form

```html
<form id="draft-form">
  <textarea name="content"
            data-rx-action="/save-draft"
            data-rx-method="POST"
            data-rx-trigger="input"
            data-rx-debounce="2000"
            data-rx-loading-indicator="save-status">
  </textarea>

  <span id="save-status" class="rx-loading-hidden">Saving...</span>
</form>
```

### Modal Form Submission

```html
<dialog id="edit-dialog">
  <form id="edit-form"
        data-rx-action="/update"
        data-rx-method="PUT"
        data-rx-disable-in-flight
        data-rx-loading-indicator="submit-spinner">
    <input name="title">
    <textarea name="body"></textarea>
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

### Live Dashboard with SSE

```csharp
public static IResult StreamMetrics(
    HttpContext context,
    IRxDriver rxDriver,
    CancellationToken ct)
{
    return rxDriver.With(context).RenderSse(
        GetMetricsAsync(ct),
        async (metric, builder) => {
            // Update multiple widgets per event
            builder
                .AddFragment<CpuGauge, CpuMetric>(metric.Cpu, "cpu-gauge", FragmentMergeStrategyType.Morph)
                .AddFragment<MemoryGauge, MemoryMetric>(metric.Memory, "memory-gauge", FragmentMergeStrategyType.Morph)
                .AddFragment<RequestCounter, int>(metric.Requests, "request-count", FragmentMergeStrategyType.Swap);

            // Conditional toast for alerts
            if (metric.Cpu.Usage > 90) {
                builder.AddTriggerToast(
                    $"High CPU: {metric.Cpu.Usage}%",
                    ToastType.Warning,
                    duration: 5000
                );
            }
        },
        eventType: "metrics",
        cancellationToken: ct
    );
}
```

---

## Requirements

**Server:**
- .NET 10.0 or later
- ASP.NET Core

**Browser:**
- Chrome/Edge 114+ (May 2023)
- Safari 17+ (September 2023)
- Firefox 125+ (April 2024)

These versions provide the required APIs: Popover (toasts), EventSource (SSE), and ES modules. View Transitions API (Chrome/Safari only) is used when available but not required.

## License

MIT

## Repository

https://github.com/ranzlee/razorx-framework
