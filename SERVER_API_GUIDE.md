# RazorX.Framework Server API Reference

Complete API reference for `IRxDriver` and `IRxResponseBuilder` interfaces.

---

## Table of Contents

1. [IRxDriver API](#irxdriver-api)
2. [IRxResponseBuilder API](#irxresponsebuilder-api)
3. [Enums](#enums)

---

## IRxDriver API

Main framework interface (injected via DI, scoped per HTTP request).

| Method | Signature | Description |
|--------|-----------|-------------|
| **With** | `IRxResponseBuilder With(HttpContext context)` | Creates a response builder for fragment-based responses |
| **RenderPage** | `Task<IResult> RenderPage<TRoot, TComponent, TModel>(HttpContext context, TModel model, CancellationToken cancellationToken = default)` | Renders full page with root layout, component, and model |
| **RenderPage** | `Task<IResult> RenderPage<TRoot, TComponent>(HttpContext context, CancellationToken cancellationToken = default)` | Renders full page with root layout and component (no model) |
| **RenderPage** | `Task<IResult> RenderPage<TRoot, THead, TComponent, TModel>(HttpContext context, TModel model, CancellationToken cancellationToken = default)` | Renders full page with custom head (no model) and body with model |
| **RenderPage** | `Task<IResult> RenderPage<TRoot, THead, TComponent>(HttpContext context, CancellationToken cancellationToken = default)` | Renders full page with custom head and body (no models) |
| **RenderPage** | `Task<IResult> RenderPage<TRoot, THead, TComponent, THeadModel, TModel>(HttpContext context, THeadModel headModel, TModel model, CancellationToken cancellationToken = default)` | Renders full page with separate head and body models |

**Type Constraints:**
- `TRoot` must implement `IRootComponent`
- `THead` must implement `IComponent` (or `IComponentModel<THeadModel>` if model used)
- `TComponent` must implement `IComponent` (or `IComponentModel<TModel>` if model used)

**Syntax:**
```csharp
// Fragment response
return await rxDriver.With(context).AddFragment<Component>(targetId).Render();

// Full page
return await rxDriver.RenderPage<App, HomePage>(context);
```

---

## IRxResponseBuilder API

Fluent API for building fragment responses. All methods return `IRxResponseBuilder` for chaining.

### Fragment Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| **AddFragment** | `AddFragment<TComponent, TModel>(TModel model, string targetId, FragmentMergeStrategyType fragmentMergeStrategy = Swap)` | Adds component fragment with model |
| **AddFragment** | `AddFragment<TComponent>(string targetId, FragmentMergeStrategyType fragmentMergeStrategy = Swap)` | Adds component fragment without model |
| **RemoveElement** | `RemoveElement(string targetId)` | Removes element from DOM |

**Type Constraints:**
- `TComponent` must implement `IComponent` (or `IComponentModel<TModel>` if model used)

**Syntax:**
```csharp
.AddFragment<UserCard, UserModel>(user, "user-123", FragmentMergeStrategyType.Swap)
.AddFragment<LoadingSpinner>("content")
.RemoveElement("notification-5")
```

### Trigger Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| **AddTriggerCloseDialog** | `AddTriggerCloseDialog(string dialogId, string? onCloseData = null, string? resetFormId = null)` | Closes HTML dialog element |
| **AddTriggerFocusElement** | `AddTriggerFocusElement(string elementId, bool positionCursorEnd = false)` | Sets focus to element, optionally positions cursor at end |
| **AddTriggerSetState** | `AddTriggerSetState(string key, string value, MetadataScope scope = Session, bool updateUrl = false)` | Sets single state value in browser storage |
| **AddTriggerSetStateBatch** | `AddTriggerSetStateBatch(Dictionary<string, string> state, MetadataScope scope, bool updateUrl = false)` | Sets multiple state values in browser storage |
| **AddTriggerToast** | `AddTriggerToast(string message, ToastType type = Success, int duration = 3500, ToastVerticalPosition verticalPosition = Top, ToastHorizontalPosition horizontalPosition = Right, bool clickToDismiss = true)` | Displays toast notification |

**Syntax:**
```csharp
.AddTriggerCloseDialog("edit-dialog", resetFormId: "edit-form")
.AddTriggerFocusElement("username", positionCursorEnd: true)
.AddTriggerSetState("filter", "active", MetadataScope.Session, updateUrl: true)
.AddTriggerSetStateBatch(new Dictionary<string, string> { { "page", "1" } }, MetadataScope.Session)
.AddTriggerToast("Saved!", ToastType.Success, duration: 3500)
```

### Rendering Methods

| Method | Signature | Description |
|--------|-----------|-------------|
| **Render** | `Task<IResult> Render(bool ignoreActiveElementValueOnMorph = false, CancellationToken cancellationToken = default)` | Executes builder and returns IResult with fragments/triggers |
| **RenderSse** | `IResult RenderSse<TModel>(IAsyncEnumerable<TModel> models, Func<TModel, IRxResponseBuilder, Task> configureEvent, string eventType = "rx-server-sent-event", CancellationToken cancellationToken = default)` | Streams fragments/triggers via Server-Sent Events |

**Syntax:**
```csharp
.Render()
.Render(ignoreActiveElementValueOnMorph: true)
.RenderSse(stream, async (model, builder) => { ... }, eventType: "update")
```

---

## Enums

### FragmentMergeStrategyType

| Value | Wire Format | Description |
|-------|-------------|-------------|
| `Swap` | `"swap"` | Replaces entire target element |
| `SwapInner` | `"swapInner"` | Replaces innerHTML only, preserves element |
| `AppendAfterBegin` | `"afterbegin"` | Inserts fragment as first child of target |
| `AppendAfterEnd` | `"afterend"` | Inserts fragment after target element (sibling) |
| `AppendBeforeBegin` | `"beforebegin"` | Inserts fragment before target element (sibling) |
| `AppendBeforeEnd` | `"beforeend"` | Inserts fragment as last child of target |
| `Morph` | `"morph"` | Intelligently updates DOM using Idiomorph algorithm, preserving focus/input state |

### MetadataScope

| Value | Wire Format | Storage Location |
|-------|-------------|------------------|
| `Session` | `"Session"` | sessionStorage (cleared when tab closes) |
| `Persistent` | `"Persistent"` | localStorage (survives browser restart) |

### ToastType

| Value | Description |
|-------|-------------|
| `Info` | Informational toast (neutral styling) |
| `Success` | Success toast (positive styling) |
| `Warning` | Warning toast (cautionary styling) |
| `Error` | Error toast (negative styling) |

### ToastVerticalPosition

| Value | Description |
|-------|-------------|
| `Top` | Display at top of viewport |
| `Center` | Display at vertical center of viewport |
| `Bottom` | Display at bottom of viewport |

### ToastHorizontalPosition

| Value | Description |
|-------|-------------|
| `Left` | Display at left edge of viewport |
| `Middle` | Display at horizontal center of viewport |
| `Right` | Display at right edge of viewport |

---

## Response Headers

| Header | Type | Description |
|--------|------|-------------|
| `rx-merge` | JSON array | Merge strategies: `[{"target":"id","strategy":"swap"}]` |
| `rx-trigger-close-dialog` | JSON object | Close dialog: `{"dialogId":"id","onCloseData":null,"resetFormId":"form-id"}` |
| `rx-trigger-focus-element` | JSON object | Focus element: `{"elementId":"id","positionCursorEnd":false}` |
| `rx-trigger-set-state` | JSON object | Set state: `{"key":"filter","value":"active","scope":"Session","updateUrl":true}` (can appear multiple times) |
| `rx-trigger-toast` | JSON object | Toast: `{"message":"text","type":"Success","duration":3500,"verticalPosition":"Top","horizontalPosition":"Right","clickToDismiss":true}` |
| `rx-morph-ignore-active` | string | When `"True"`, preserves focused input value during morph |

**Note:** JSON uses camelCase property names.

---

## Component Interfaces

### IRootComponent

Required properties for root layout components:

| Property | Type | Description |
|----------|------|-------------|
| `HeadContent` | `Type?` | Optional head component type |
| `HeadContentParameters` | `Dictionary<string, object?>` | Parameters for head component |
| `MainContent` | `Type` | Main content component type |
| `MainContentParameters` | `Dictionary<string, object?>` | Parameters for main content component |

### IComponentModel<TModel>

Required property for components with models:

| Property | Type | Description |
|----------|------|-------------|
| `Model` | `TModel` | Strongly-typed model instance |

---

## Server-Sent Events (SSE)

### Broadcast Service

**RxSseBroadcastService<TModel, TMetadata>** - Multi-client broadcasting with metadata filtering

**Constructor:**
```csharp
public RxSseBroadcastService(
    ILogger<RxSseBroadcastService<TModel, TMetadata>> logger,
    IRxBroadcastTransport? transport = null,
    JsonTypeInfo<TModel>? modelTypeInfo = null,
    JsonTypeInfo<TMetadata>? metadataTypeInfo = null,
    IConfiguration? config = null
)
```

**Methods:**

| Method | Signature | Description |
|--------|-----------|-------------|
| **Subscribe** | `bool Subscribe(string subscriberId, Func<TMetadata?, bool>? filter = null)` | Subscribe to broadcasts with optional metadata filter. Returns false if already subscribed. |
| **Unsubscribe** | `void Unsubscribe(string subscriberId)` | Unsubscribe from broadcasts |
| **GetUpdates** | `IAsyncEnumerable<TModel> GetUpdates(string subscriberId, CancellationToken cancellationToken)` | Get async stream of updates for subscriber |
| **BroadcastUpdate** | `Task BroadcastUpdate(TModel model, TMetadata? broadcasterMetadata = default)` | Broadcast update to all subscribers (filtered by their subscription filters) |
| **GetActiveConnectionCount** | `int GetActiveConnectionCount()` | Get count of active subscribers |
| **GetActiveSubscribers** | `IReadOnlyList<string> GetActiveSubscribers()` | Get list of active subscriber IDs |
| **HasSubscriber** | `bool HasSubscriber(string subscriberId)` | Check if subscriber ID is currently subscribed |

**Registration:**
```csharp
// In-memory mode (single server)
builder.Services.AddSingleton(sp => {
    var logger = sp.GetRequiredService<ILogger<RxSseBroadcastService<TodoModel, Metadata>>>();
    return new RxSseBroadcastService<TodoModel, Metadata>(logger);
});
```

**Usage:**
```csharp
// Subscribe with filter
broadcast.Subscribe(subscriberId, filter: meta => meta?.TenantId == tenantId);

// Unsubscribe (typically on disconnect)
ct.Register(() => broadcast.Unsubscribe(subscriberId));

// Get updates stream
broadcast.GetUpdates(subscriberId, ct)

// Broadcast with metadata
await broadcast.BroadcastUpdate(model, new Metadata { TenantId = tenantId });
```

---

## Validation Rules

| Item | Rule | Exception |
|------|------|-----------|
| Target ID | Cannot be null/empty/whitespace | `ArgumentException` |
| Target ID | Cannot contain `<`, `>`, `"`, `'`, `&` | `ArgumentException` |
| State Key | Cannot be null/empty/whitespace | `ArgumentException` |
| State Key | Only alphanumeric, hyphen, underscore | `ArgumentException` |
| State Key | Cannot be set twice in same response | `InvalidOperationException` |
| Toast Message | Cannot be null/empty/whitespace | `ArgumentException` |
| Toast Duration | Cannot be negative | `ArgumentException` |
| Render | Requires `rx-request` header (AJAX requests only) | `InvalidOperationException` |
| Render | Can only be called once per request | `InvalidOperationException` |
| RenderSse models | Cannot be null | `ArgumentNullException` |
| RenderSse configureEvent | Cannot be null | `ArgumentNullException` |
| RenderSse eventType | Cannot be null/empty/whitespace | `ArgumentException` |
| RenderSse | Can only be called once per request | `InvalidOperationException` |
| Disposed objects | Cannot use disposed driver/builder | `ObjectDisposedException` |

---

See [RazorX.Examples](../../RazorX.Examples) for working code examples.
