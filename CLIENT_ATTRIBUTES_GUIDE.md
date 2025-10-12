# RazorX.js Client Attributes Guide

## 📖 About This Guide

This guide documents all HTML `data-rx-*` attributes that control client-side behavior in RazorX.Framework applications. These attributes declaratively define how elements interact with your server, eliminating the need for manual JavaScript.

**Who should read this:** Front-end developers and full-stack developers working with RazorX applications.

**Prerequisites:**
- Basic HTML/CSS knowledge
- Understanding of HTTP methods (GET, POST, PUT, DELETE)
- Familiarity with browser events (click, submit, input, etc.)

---

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Request Configuration](#request-configuration)
   - [data-rx-action](#data-rx-action)
   - [data-rx-method](#data-rx-method)
   - [data-rx-trigger](#data-rx-trigger)
3. [Request Behavior](#request-behavior)
   - [data-rx-debounce](#data-rx-debounce)
   - [data-rx-disable-in-flight](#data-rx-disable-in-flight)
   - [data-rx-disable-queueing](#data-rx-disable-queueing)
   - [data-rx-allow-event-default](#data-rx-allow-event-default)
4. [State Management](#state-management)
   - [data-rx-include-state](#data-rx-include-state)
5. [UI Feedback](#ui-feedback)
   - [data-rx-loading-indicator](#data-rx-loading-indicator)
6. [Advanced Features](#advanced-features)
   - [data-rx-delegate-action-to](#data-rx-delegate-action-to)
   - [File Uploads](#file-uploads)
   - [Server-Sent Events (SSE)](#server-sent-events-sse)
7. [Common Patterns](#common-patterns)
8. [Troubleshooting](#troubleshooting)

---

## Core Concepts

### How RazorX Attributes Work

RazorX uses **HTML data attributes** to declaratively define interactions. When the framework initializes, it:

1. **Scans the DOM** for elements with `data-rx-*` attributes
2. **Attaches event listeners** based on trigger configuration
3. **Handles requests** automatically when events fire
4. **Updates the DOM** based on server response

**Example:**
```html
<button
  data-rx-action="/api/users/123"
  data-rx-method="DELETE"
  data-rx-trigger="click">
  Delete User
</button>
```

When clicked:
1. Makes DELETE request to `/api/users/123`
2. Server returns fragment response
3. DOM updates automatically
4. No JavaScript required!

### The Request Lifecycle

```
User Action (click, input, etc.)
  ↓
Trigger fires → Debounce delay (if configured)
  ↓
Request disabled? → Skip if in-flight and disabled
  ↓
Queue request (if queueing enabled)
  ↓
Collect form data + state
  ↓
Send HTTP request
  ↓
Show loading indicator (if configured)
  ↓
Receive response
  ↓
Update DOM with fragments
  ↓
Execute triggers (toasts, focus, etc.)
  ↓
Hide loading indicator
  ↓
Re-enable element
```

---

## Request Configuration

### data-rx-action

**Purpose:** Specifies the URL to send the request to.

**Type:** `string` (URL or path)

**Required:** Yes (for actionable elements)

**Examples:**
```html
<!-- Absolute URL -->
<button data-rx-action="https://api.example.com/users">

<!-- Relative path -->
<button data-rx-action="/api/users">

<!-- Path with parameters -->
<button data-rx-action="/api/users/123">

<!-- Dynamic path (from Razor) -->
<button data-rx-action="/api/users/@userId">
```

**Notes:**
- Can be absolute or relative
- Query parameters can be added via `data-rx-include-state` or form fields
- Server returns fragments with merge instructions (see [Server API Guide](./SERVER_API_GUIDE.md))

---

### data-rx-method

**Purpose:** Specifies the HTTP method for the request.

**Type:** `string`

**Valid Values:** `GET`, `POST`, `PUT`, `PATCH`, `DELETE`

**Default (Context-Dependent):**
- For `<form>` elements or elements inside forms: **`POST`**
- For all other elements (buttons, divs, etc.): **`GET`**

**Examples:**
```html
<!-- Form defaults to POST if method not specified -->
<form data-rx-action="/api/users">
  <!-- Uses POST automatically -->
</form>

<!-- Explicit GET for form (fetch data) -->
<form data-rx-action="/api/search" data-rx-method="GET">

<!-- Button inside form defaults to POST -->
<form>
  <button data-rx-action="/api/save">
    <!-- Uses POST (inherits from form context) -->
  </button>
</form>

<!-- Button outside form defaults to GET -->
<button data-rx-action="/api/users/123">
  <!-- Uses GET (not in form context) -->
</button>

<!-- Explicit DELETE -->
<button data-rx-action="/api/users/123" data-rx-method="DELETE">
  Delete User
</button>

<!-- PUT request (update resource) -->
<form data-rx-action="/api/users/123" data-rx-method="PUT">
```

**Form Behavior:**
- For `<form>` elements, collects all input/select/textarea values
- By default, encodes as JSON (`Content-Type: application/json`)
- Can be changed via `razorx.init({ encodeRequestFormDataAsJson: false })`

**Method Detection Logic:**
```
1. Check data-rx-method attribute
2. If not set:
   - Is element a <form>? → POST
   - Is element inside a <form>? → POST
   - Otherwise → GET
```

---

### data-rx-trigger

**Purpose:** Defines when the request should be sent.

**Type:** `string` (event name) or `JSON` (special trigger configuration)

**Default:** `click` for buttons, `submit` for forms

#### DOM Event Triggers

Use any standard DOM event name:

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

#### Special Triggers

RazorX provides three **special triggers** for advanced scenarios:

##### 1. **initialized** - Fires Once When Element Added to DOM

**Use Cases:**
- Load initial content after page loads
- Defer non-critical requests
- Lazy-load data

**Configuration:**
```typescript
{
  "type": "initialized",
  "delay": number  // Optional delay in ms (default: 0)
}
```

**Examples:**
```html
<!-- Load immediately when page loads -->
<div
  data-rx-action="/api/stats"
  data-rx-trigger='{"type":"initialized"}'>
</div>

<!-- Load after 500ms delay -->
<div
  data-rx-action="/api/recommendations"
  data-rx-trigger='{"type":"initialized","delay":500}'>
</div>

<!-- Search with initial state -->
<div
  data-rx-action="/search"
  data-rx-trigger='{"type":"initialized","delay":100}'
  data-rx-include-state="filter">
</div>
```

**Notes:**
- Fires only **once** when element is first added to DOM
- Useful for avoiding inline script tags
- Delay helps prevent race conditions with other initialization

##### 2. **poll** - Fires Repeatedly at Intervals

**Use Cases:**
- Live dashboards
- Real-time monitoring
- Auto-refresh content

**Configuration:**
```typescript
{
  "type": "poll",
  "interval": number  // Interval in ms (default: 1000)
}
```

**Examples:**
```html
<!-- Poll every 5 seconds -->
<div
  data-rx-action="/api/status"
  data-rx-trigger='{"type":"poll","interval":5000}'>
  Loading...
</div>

<!-- Live metrics (every 2 seconds) -->
<div
  data-rx-action="/api/metrics"
  data-rx-trigger='{"type":"poll","interval":2000}'>
</div>
```

**Notes:**
- ⚠️ **Use SSE instead for real-time push** (polling wastes bandwidth)
- Automatically cleaned up when element is removed
- Interval starts immediately
- For better performance, use [Server-Sent Events](#server-sent-events-sse)

##### 3. **revealed** - Fires When Element Enters Viewport

**Use Cases:**
- Infinite scroll
- Lazy loading
- Analytics tracking

**Configuration:**
```typescript
{
  "type": "revealed",
  "margin": string  // IntersectionObserver rootMargin (default: "0px")
}
```

**Examples:**
```html
<!-- Infinite scroll - load when bottom sentinel visible -->
<div
  id="load-more-sentinel"
  data-rx-action="/api/items/next/100"
  data-rx-trigger='{"type":"revealed"}'>
</div>

<!-- Load earlier (200px before visible) -->
<div
  data-rx-action="/api/images/page2"
  data-rx-trigger='{"type":"revealed","margin":"200px"}'>
</div>

<!-- Lazy load images -->
<div
  class="image-placeholder"
  data-rx-action="/api/image/123"
  data-rx-trigger='{"type":"revealed","margin":"50px"}'>
</div>
```

**Notes:**
- Uses [IntersectionObserver](https://developer.mozilla.org/en-US/docs/Web/API/Intersection_Observer_API) for performance
- Fires **once** when element enters viewport
- `margin` values: `"200px"`, `"10%"`, `"200px 0px"` (top/bottom)
- Perfect for infinite scroll pattern

---

## Request Behavior

### data-rx-debounce

**Purpose:** Delays request execution until user stops triggering events.

**Type:** `number` (milliseconds)

**Default:** `0` (no debounce)

**Use Cases:**
- Real-time search (wait for user to stop typing)
- Auto-save (wait for editing pause)
- Reducing server load

**Examples:**
```html
<!-- Search as user types (300ms delay) -->
<input
  type="search"
  data-rx-action="/search"
  data-rx-trigger="input"
  data-rx-debounce="300"
  placeholder="Search...">

<!-- Auto-save textarea (1 second after editing stops) -->
<textarea
  data-rx-action="/save-draft"
  data-rx-trigger="input"
  data-rx-debounce="1000">
</textarea>

<!-- Real-time validation (500ms) -->
<input
  type="email"
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

**Best Practices:**
- **Search:** 300-500ms (feels responsive)
- **Auto-save:** 1000-2000ms (avoid too many saves)
- **Validation:** 500ms (fast feedback)
- **Too low:** Wastes server resources
- **Too high:** Feels unresponsive

---

### data-rx-disable-in-flight

**Purpose:** Disables element while request is in progress.

**Type:** Boolean (presence = true)

**Use Cases:**
- Prevent double-submission
- Disable buttons during save
- Prevent form re-submission

**Examples:**
```html
<!-- Disable button during request -->
<button
  data-rx-action="/api/submit"
  data-rx-method="POST"
  data-rx-disable-in-flight>
  Submit
</button>

<!-- Disable entire form -->
<form
  data-rx-action="/api/save"
  data-rx-method="POST"
  data-rx-disable-in-flight>
  <!-- All inputs disabled during submission -->
</form>

<!-- Combine with loading indicator -->
<button
  data-rx-action="/api/process"
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

---

### data-rx-disable-queueing

**Purpose:** Allows this element's requests to execute immediately without waiting in the queue.

**Type:** Boolean (presence = true)

**Default:** All requests use a single global queue and execute sequentially

**Use Cases:**
- Independent actions that should run in parallel
- Real-time updates that shouldn't wait for other requests
- High-priority requests that need immediate execution

**How RazorX Queuing Works:**

RazorX uses a **single, global request queue** to execute all requests sequentially across the entire page. This ensures predictable order and prevents race conditions.

**Important:** Both modes (queued and non-queued) **always prevent duplicate requests for the same element**. This protection is built-in and cannot be disabled.

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
<button
  data-rx-action="/update-sidebar"
  data-rx-disable-queueing>
  Update Sidebar
</button>

<button
  data-rx-action="/refresh-stats"
  data-rx-disable-queueing>
  Refresh Stats
</button>

<!-- User clicks both buttons quickly -->
<!-- → BOTH requests run concurrently (don't wait for each other) -->
```

**Queue Behavior:**

**Without `data-rx-disable-queueing` (DEFAULT):**
```
User clicks Button A → Request A starts
User clicks Button B → Request B waits in global queue
  → Request A finishes
  → Request B starts immediately
User clicks Button C → Request C waits in queue
  → Request B finishes
  → Request C starts

User clicks Button A again (while A is still running) → SKIPPED (duplicate detection)
```

**With `data-rx-disable-queueing` on Button A and B:**
```
User clicks Button A → Request A starts IMMEDIATELY (bypasses queue)
User clicks Button B → Request B starts IMMEDIATELY (bypasses queue)
  → BOTH run concurrently

User clicks Button A again (while A is still running) → STILL SKIPPED (duplicate detection)
User clicks Button B again (while B is still running) → STILL SKIPPED (duplicate detection)
```

**Key Insights:**

1. **The queue is GLOBAL**, not per-element
   - Default: All requests across ALL elements execute one-at-a-time
   - With disable-queueing: Specific elements can run concurrently

2. **Duplicate protection is ALWAYS active**
   - Same element cannot have multiple requests running simultaneously
   - This is true regardless of queueing setting
   - If element already has a request in-flight, new triggers are ignored

3. **When to use:**
   - ✅ Independent operations that can safely run in parallel (e.g., updating different sections)
   - ✅ Real-time widgets that shouldn't wait for form submissions
   - ✅ Background refresh while user continues interacting
   - ❌ Sequential operations where order matters
   - ❌ Operations that modify shared state

**Example: Mixed Usage**

```html
<!-- Critical form submission: queued (predictable order) -->
<form data-rx-action="/submit-order" data-rx-method="POST">
  <button>Submit Order</button>
</form>

<!-- Live stats: bypass queue (don't block form submission) -->
<div
  data-rx-action="/live-stats"
  data-rx-trigger='{"type":"poll","interval":3000}'
  data-rx-disable-queueing>
  <!-- Stats update every 3s without interfering with other requests -->
</div>
```

---

### data-rx-allow-event-default

**Purpose:** Allows default browser behavior for the event.

**Type:** Boolean (presence = true)

**Default:** `false` (preventDefault() called)

**Use Cases:**
- Form validation before AJAX
- Links that should navigate normally on error
- Checkbox state management

**Examples:**
```html
<!-- Let checkbox state update before sending request -->
<input
  type="checkbox"
  data-rx-action="/toggle"
  data-rx-trigger="change"
  data-rx-allow-event-default>

<!-- Form with native validation -->
<form
  data-rx-action="/submit"
  data-rx-allow-event-default>
  <input required>
  <button>Submit</button>
</form>
```

**Default Behavior (without attribute):**
```javascript
// RazorX automatically calls:
event.preventDefault()  // Stop form submission, link navigation, etc.
```

**With `data-rx-allow-event-default`:**
```javascript
// Event proceeds normally:
// - Forms submit natively
// - Links navigate
// - Checkboxes toggle
// Then AJAX request still fires
```

**⚠️ Warning:** Usually not needed. Forms handled via AJAX don't need native submission.

---

## State Management

### data-rx-include-state

**Purpose:** Includes stored state values in the request.

**Type:** `string` (JSON array) or `string` (single key)

**Use Cases:**
- Filter persistence across pages
- User preferences
- Session tracking

**Examples:**
```html
<!-- Include single state key -->
<button
  data-rx-action="/search"
  data-rx-include-state="filter">
  Search
</button>

<!-- Include multiple state keys -->
<button
  data-rx-action="/api/data"
  data-rx-include-state='["filter", "sort", "page"]'>
  Load Data
</button>

<!-- Include instance ID for SSE -->
<form
  data-rx-action="/submit"
  data-rx-include-state='["rx-instance-id"]'>
</form>
```

**How It Works:**

1. **Server sets state** via `AddTriggerSetState()`:
```csharp
return await rxDriver
    .With(context)
    .AddTriggerSetState("filter", "active", MetadataScope.Session, updateUrl: true)
    .Render();
```

2. **Browser stores** in sessionStorage or localStorage

3. **Client includes** in next request:
```http
GET /search?filter=active
```

**Storage Scopes:**
- `Session` → sessionStorage (cleared when tab closes)
- `Persistent` → localStorage (survives browser restart)

**Special State Keys:**
- `rx-instance-id` - Unique page instance ID (auto-generated)
- Custom keys - Set via server `AddTriggerSetState()`

**URL Synchronization:**

When `updateUrl: true`, state is **also** added to URL query params:
```
https://example.com/page?filter=active&sort=date
```

This enables:
- ✅ Shareable URLs
- ✅ Browser back/forward works correctly
- ✅ Bookmarking preserves state

---

## UI Feedback

### data-rx-loading-indicator

**Purpose:** Shows/hides element while request is in progress.

**Type:** `string` (element ID) or `boolean` (`false` to disable)

**Use Cases:**
- Spinner during save
- Loading text during fetch
- Progress indicators

**Examples:**
```html
<!-- Simple spinner -->
<button
  data-rx-action="/api/save"
  data-rx-loading-indicator="spinner">
  Save
  <span id="spinner" class="rx-loading-hidden">⏳</span>
</button>

<!-- Loading text -->
<form
  data-rx-action="/submit"
  data-rx-loading-indicator="loading-text">
  <button>Submit</button>
</form>
<div id="loading-text" class="rx-loading-hidden">
  Submitting...
</div>

<!-- Disable indicator (inherit from parent) -->
<button
  data-rx-action="/api/action"
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

**Behavior:**
```
Request starts:
  → Add 'rx-loading-visible'
  → Remove 'rx-loading-hidden'

Request completes:
  → Add 'rx-loading-hidden'
  → Remove 'rx-loading-visible'
```

---

## Advanced Features

### data-rx-delegate-action-to

**Purpose:** Transfers `data-rx-action` and `data-rx-method` to another element.

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

<button
  data-rx-delegate-action-to="draft-form"
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

**Notes:**
- Button can override action/method from target
- Useful for separating UI from logic
- Works with forms and any element with action/method

---

### File Uploads

RazorX automatically handles file uploads with progress tracking and size validation.

#### data-rx-file-upload-progress-id

**Purpose:** Specifies which element to update with upload progress.

**Type:** `string` (element ID)

**Applies To:** `<input type="file">` only

**Example:**
```html
<form data-rx-action="/upload" data-rx-method="POST">
  <input
    type="file"
    name="file"
    data-rx-file-upload-progress-id="upload-progress">
  <button>Upload</button>
</form>

<progress id="upload-progress" value="0" max="100"></progress>
```

**Behavior:**
- Updates `<progress>` element's `value` attribute
- Shows percentage (0-100)
- Real-time updates during upload

#### data-rx-file-upload-timeout

**Purpose:** Sets the maximum time allowed for file upload.

**Type:** `number` (milliseconds)

**Default:** No timeout

**Example:**
```html
<!-- 60 second timeout -->
<input
  type="file"
  data-rx-action="/upload"
  data-rx-file-upload-timeout="60000">
```

#### data-rx-file-upload-max-size

**Purpose:** Sets the maximum allowed file size.

**Type:** `number` (bytes)

**Default:** No limit

**Example:**
```html
<!-- 5MB max file size -->
<input
  type="file"
  data-rx-action="/upload"
  data-rx-file-upload-max-size="5242880">

<!-- 10MB with progress -->
<input
  type="file"
  data-rx-action="/upload"
  data-rx-file-upload-max-size="10485760"
  data-rx-file-upload-progress-id="progress">
```

**Behavior:**
- Validates **client-side** before upload
- Shows error if file too large
- Prevents wasted bandwidth

**Complete Upload Example:**
```html
<form data-rx-action="/upload" data-rx-method="POST">
  <input
    type="file"
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

### Server-Sent Events (SSE)

RazorX supports real-time push updates from server via SSE.

#### data-rx-sse-connect

**Purpose:** Establishes a Server-Sent Events connection to the specified URL.

**Type:** `string` (URL)

**Use Cases:**
- Live notifications
- Real-time dashboards
- Collaborative editing
- Activity streams

**Example:**
```html
<!-- Basic SSE connection -->
<div data-rx-sse-connect="/stream"></div>

<!-- With instance ID tracking -->
<div
  data-rx-sse-connect="/stream"
  data-rx-include-state='["rx-instance-id"]'>
</div>
```

#### data-rx-sse-events

**Purpose:** Filter which SSE event types to listen for.

**Type:** `string` (single event) or `JSON array` (multiple events)

**Default:** Listens to all events

**Example:**
```html
<!-- Listen to single event type -->
<div
  data-rx-sse-connect="/stream"
  data-rx-sse-events="user-update">
</div>

<!-- Listen to multiple event types -->
<div
  data-rx-sse-connect="/stream"
  data-rx-sse-events='["user-update", "comment-added", "notification"]'>
</div>
```

#### data-rx-sse-connect-delay

**Purpose:** Delays the SSE connection by the specified milliseconds.

**Type:** `number` (milliseconds)

**Default:** `0` (connect immediately)

**Use Cases:**
- Wait for page initialization
- Avoid race conditions
- Stagger connections

**Example:**
```html
<!-- Connect after 500ms -->
<div
  data-rx-sse-connect="/stream"
  data-rx-sse-connect-delay="500">
</div>
```

**Complete SSE Example:**
```html
<!-- Client: Listen for todo updates -->
<div
  id="todo-stream"
  data-rx-sse-connect="/stream"
  data-rx-sse-connect-delay="100"
  data-rx-sse-events="todo-change"
  data-rx-include-state='["rx-instance-id"]'>
</div>

<!-- Server sends updates that morph/append to DOM automatically -->
```

**Server-Side (C#):**
```csharp
public record SseMetadata {
    public string? SubscriberId { get; init; }
}

public static IResult StreamUpdates(
    HttpContext context,
    [FromQuery(Name = "rx-instance-id")] string rxInstanceId,
    IRxDriver rxDriver,
    RxSseBroadcastService<TodoModel, SseMetadata> broadcast,
    CancellationToken ct)
{
    // Subscribe (no filter = receive all broadcasts)
    broadcast.Subscribe(rxInstanceId);
    ct.Register(() => broadcast.Unsubscribe(rxInstanceId));

    return rxDriver
        .With(context)
        .RenderSse(
            broadcast.GetUpdates(rxInstanceId, ct),
            async (todo, builder) => {
                builder.AddFragment<TodoItem, TodoModel>(todo, "todo-list", FragmentMergeStrategyType.AppendBeforeEnd);
            },
            eventType: "todo-change",
            cancellationToken: ct
        );
}
```

**Connection Behavior:**
- Auto-reconnect with exponential backoff (1s → 2s → 4s → ... max 30s)
- Automatic cleanup when element removed
- State attribute: `data-sse-state="connected"` or `"error"`

---

## Common Patterns

### Real-Time Search

```html
<input
  type="search"
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
<div
  id="load-more"
  data-rx-action="/items/next/20"
  data-rx-trigger='{"type":"revealed","margin":"200px"}'>
  Loading more...
</div>
```

### Auto-Save Form

```html
<form id="draft-form">
  <textarea
    name="content"
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
  <form
    id="edit-form"
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

### Live Dashboard with Polling

```html
<!-- Better: Use SSE instead! See below -->
<div
  id="dashboard"
  data-rx-action="/api/metrics"
  data-rx-trigger='{"type":"poll","interval":5000}'>
  Loading metrics...
</div>
```

### Live Dashboard with SSE (Recommended)

```html
<!-- Server pushes updates (more efficient) -->
<div
  id="dashboard"
  data-rx-sse-connect="/stream/metrics"
  data-rx-sse-events="metrics-update">
  Loading metrics...
</div>
```

### Conditional Loading

```html
<!-- Load content after initial page render -->
<div
  data-rx-action="/expensive-widget"
  data-rx-trigger='{"type":"initialized","delay":1000}'
  data-rx-loading-indicator="widget-loading">
  <div id="widget-loading" class="rx-loading-visible">
    Loading widget...
  </div>
</div>
```

---

## Troubleshooting

### Request Not Firing

**Check:**
1. ✅ `data-rx-action` attribute present
2. ✅ `data-rx-trigger` matches event (or uses default)
3. ✅ Element not disabled
4. ✅ `razorx.init()` called in `<script>`
5. ✅ Browser console for errors

**Debug:**
```javascript
// Check if element is registered
const element = document.getElementById('my-element');
console.log(element.dataset.rxAction); // Should print action URL
```

### Debounce Not Working

**Common Mistake:**
```html
<!-- ❌ WRONG: debounce as string -->
<input data-rx-debounce="500">

<!-- ✅ CORRECT: debounce as number (no quotes in Razor) -->
<input data-rx-debounce="500">
```

**In Razor:**
```razor
<!-- Correct -->
<input data-rx-debounce="500">

<!-- Also correct with variable -->
<input data-rx-debounce="@(500)">
```

### Special Trigger Not Firing

**Check JSON syntax:**
```html
<!-- ❌ WRONG: Invalid JSON (smart quotes) -->
<div data-rx-trigger='{"type":"initialized"}'>

<!-- ✅ CORRECT: Valid JSON (straight quotes) -->
<div data-rx-trigger='{"type":"initialized"}'>

<!-- ❌ WRONG: Missing quotes around type -->
<div data-rx-trigger='{type:"initialized"}'>

<!-- ✅ CORRECT: Quotes around property names -->
<div data-rx-trigger='{"type":"initialized"}'>
```

**In Razor, use `@( )` for JSON:**
```razor
<div data-rx-trigger='@("{\"type\":\"poll\",\"interval\":5000}")'>
```

Or use `value:` syntax:
```razor
<div data-rx-trigger="@(value: "{ \"type\": \"poll\", \"interval\": 5000 }")">
```

### SSE Connection Failing

**Check:**
1. ✅ Server returns `Content-Type: text/event-stream`
2. ✅ Server keeps connection open
3. ✅ `data-rx-sse-connect` points to valid URL
4. ✅ Network tab shows EventSource connection
5. ✅ Element has `data-sse-state="connected"` attribute

**Debug in console:**
```javascript
// Check SSE state
const element = document.getElementById('stream');
console.log(element.dataset.sseState); // Should be "connected"
```

### State Not Included in Request

**Check:**
1. ✅ State was set via server `AddTriggerSetState()`
2. ✅ Scope matches (Session vs Persistent)
3. ✅ Key name matches exactly (case-sensitive)
4. ✅ `data-rx-include-state` syntax correct

**Debug in console:**
```javascript
// Check sessionStorage
console.log(sessionStorage.getItem('filter'));

// Check localStorage
console.log(localStorage.getItem('theme'));
```

### File Upload Not Progressing

**Check:**
1. ✅ Progress element ID matches `data-rx-file-upload-progress-id`
2. ✅ Progress element is `<progress>` tag
3. ✅ Server supports chunked uploads
4. ✅ File size under `data-rx-file-upload-max-size`

---

## Summary

| Attribute | Purpose | Common Use |
|-----------|---------|------------|
| **data-rx-action** | Request URL | All requests |
| **data-rx-method** | HTTP method | POST, PUT, DELETE |
| **data-rx-trigger** | When to fire | click, input, special |
| **data-rx-debounce** | Delay requests | Search, auto-save |
| **data-rx-disable-in-flight** | Prevent double-submit | Forms, buttons |
| **data-rx-disable-queueing** | Skip if busy | Search, polling |
| **data-rx-include-state** | Include storage | Filters, preferences |
| **data-rx-loading-indicator** | Show spinner | All requests |
| **data-rx-delegate-action-to** | Remote submit | Dialogs, external buttons |
| **data-rx-sse-connect** | Real-time updates | Live data, notifications |

---

## Next Steps

- **[Server API Guide](./SERVER_API_GUIDE.md)** - Learn how to build responses with `IRxDriver` and `IRxResponseBuilder`
- **[CLAUDE.md](../../CLAUDE.md)** - Complete framework architecture and patterns
- **[AOT Consumer Guide](./AOT_CONSUMER_GUIDE.md)** - Native AOT compilation requirements

---

**Questions?** Check the [RazorX.Examples](../../RazorX.Examples) project for working code samples.
