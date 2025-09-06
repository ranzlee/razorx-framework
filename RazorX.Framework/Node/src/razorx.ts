import { Idiomorph } from "idiomorph";

declare global {
    interface Document {
        rxMutationObserver: MutationObserver
    }

    interface HTMLElement {
        dataset: {
            // all dataset props must be strings
            rxAction?: string, //data-rx-action
            rxMethod?: string, //data-rx-method
            rxTrigger?: string, //data-rx-trigger
            rxAllowEventDefault?: string //data-rx-allow-event-default
            rxDisableInFlight?: string, //data-rx-disable-in-flight
            rxDebounce?: string //data-rx-debounce
            rxDisableQueueing?: string // data-rx-disable-queueing
            rxDelegateActionTo?: string //data-rx-delegate-action-to transfer action and method to another element
            rxIncludeState?: string //data-rx-include-state
            rxLoadingIndicator?: string //data-rx-loading-indicator
            rxFileUploadProgressId?: string //data-rx-file-upload-progress-id - ID of progress element (only valid on file inputs)
            rxFileUploadTimeout?: string //data-rx-file-upload-timeout - Timeout in milliseconds for file upload (only valid on file inputs)
            rxFileUploadMaxSize?: string //data-rx-file-upload-max-size - Maximum file size in bytes (only valid on file inputs)
        },
        addRxCallbacks?: (callbacks: ElementCallbacks) => void,
        _rxCallbacks?: ElementCallbacks,
    }
}

/**
 * Main RazorX framework interface providing initialization and callback management.
 * @remarks
 * This is the primary API for interacting with the RazorX client-side framework.
 * The framework must be initialized before use.
 */
export type RazorX = {
    /**
     * Initializes the RazorX framework and begins processing the document.
     * @param options - Optional configuration for framework behavior
     * @remarks
     * - Sets up MutationObserver for dynamic DOM changes
     * - Processes all existing elements with data-rx attributes
     * - Must be called once per page load
     * - Subsequent calls are ignored with a debug message
     * @example
     * ```typescript
     * razorx.init({
     *   encodeRequestFormDataAsJson: true,
     *   addCookieToRequestHeader: 'RequestVerificationToken'
     * });
     * ```
     */
    init: (options?: Options) => void,
    /**
     * Registers global callbacks for framework lifecycle events.
     * @param callbacks - Document-level callback handlers
     * @remarks
     * Callbacks are additive - calling multiple times adds new callbacks
     * without removing existing ones.
     */
    addCallbacks: (callbacks: DocumentCallbacks) => void,
}

/**
 * Configuration options for the RazorX framework.
 * @remarks
 * All options are optional and have sensible defaults.
 * Options are set during initialization and cannot be changed afterward.
 */
export type Options = {
    /**
     * Cookie name(s) to include in request headers for CSRF protection.
     * @remarks
     * Can be a single cookie name or array of names.
     * Cookie values are added to the request header with the same name.
     * @example
     * ```typescript
     * addCookieToRequestHeader: 'RequestVerificationToken'
     * // or
     * addCookieToRequestHeader: ['Token1', 'Token2']
     * ```
     */
    addCookieToRequestHeader?: string | string[],
    /**
     * Whether to encode form data as JSON before sending.
     * @default true
     * @remarks
     * When true, FormData is converted to JSON with Content-Type: application/json.
     * When false, FormData is sent as-is (multipart/form-data or application/x-www-form-urlencoded).
     * Files are always extracted and uploaded separately regardless of this setting.
     */
    encodeRequestFormDataAsJson?: boolean,
    /**
     * CSS classes for loading indicator states.
     * @remarks
     * Applied to elements specified by data-rx-loading-indicator attribute.
     */
    loadingIndicatorClasses?: {
        /** Class applied when element is not loading @default 'rx-loading-hidden' */
        hidden?: string,
        /** Class applied when element is loading @default 'rx-loading-visible' */
        visible?: string
    },
    /**
     * CSS classes for toast notifications.
     * @remarks
     * Customize the appearance and positioning of toast messages.
     * All classes are optional and have framework defaults.
     */
    toastClasses?: {
        /** Base class for all toasts @default 'rx-toast' */
        base?: string,
        /** Info toast type @default 'rx-toast-info' */
        info?: string,
        /** Success toast type @default 'rx-toast-success' */
        success?: string,
        /** Warning toast type @default 'rx-toast-warning' */
        warning?: string,
        /** Error toast type @default 'rx-toast-error' */
        error?: string,
        /** Top-left position @default 'rx-toast-top-left' */
        topLeft?: string,
        /** Top-center position @default 'rx-toast-top-middle' */
        topMiddle?: string,
        /** Top-right position @default 'rx-toast-top-right' */
        topRight?: string,
        /** Center-left position @default 'rx-toast-center-left' */
        centerLeft?: string,
        /** Center position @default 'rx-toast-center-middle' */
        centerMiddle?: string,
        /** Center-right position @default 'rx-toast-center-right' */
        centerRight?: string,
        /** Bottom-left position @default 'rx-toast-bottom-left' */
        bottomLeft?: string,
        /** Bottom-center position @default 'rx-toast-bottom-middle' */
        bottomMiddle?: string,
        /** Bottom-right position @default 'rx-toast-bottom-right' */
        bottomRight?: string
    }
}

/**
 * Information about a selected file.
 * @remarks
 * Provided to file selection callbacks for user feedback.
 */
export type FileInfo = {
    /** The name of the file */
    fileName: string,
    /** Human-readable file size (e.g., "1.5 MB") */
    size: string,
    /** Exact file size in bytes */
    sizeInBytes: number
}

/**
 * Document-level callbacks for global framework lifecycle events.
 * @remarks
 * These callbacks apply to all RazorX elements in the document.
 * Use ElementCallbacks for element-specific behavior.
 * All callbacks are optional.
 */
export type DocumentCallbacks = {
    /**
     * Called before the document is initially processed.
     * @remarks Useful for setup tasks before RazorX begins processing.
     */
    beforeDocumentProcessed?: () => void,
    /**
     * Called after the document has been fully processed.
     * @remarks All initial RazorX elements have been configured.
     */
    afterDocumentProcessed?: () => void,
    /**
     * Called before an element with data-rx attributes is initialized.
     * @param element - The element about to be initialized
     * @returns Return false to prevent initialization of this element
     */
    beforeInitializeElement?: (element: HTMLElement) => boolean,
    /**
     * Called after an element has been initialized with triggers.
     * @param element - The newly initialized element
     */
    afterInitializeElement?: (element: HTMLElement) => void,
    /**
     * Called before a fetch request is sent.
     * @param triggerElement - The element that triggered the request
     * @param requestConfiguration - The request configuration (can be modified)
     * @remarks
     * Modify headers, body, or call abort() to cancel the request.
     * This is the last chance to modify the request before sending.
     */
    beforeFetch?: (triggerElement: HTMLElement, requestConfiguration: RequestConfiguration) => void,
    /**
     * Called after a fetch response is received.
     * @param triggerElement - The element that triggered the request
     * @param requestDetail - The final request details that were sent
     * @param response - The fetch response object
     */
    afterFetch?: (triggerElement: HTMLElement, requestDetail: RequestDetail, response: Response) => void,
    /**
     * Called before the DOM is updated with a fragment.
     * @param triggerElement - The element that triggered the update
     * @param mergeElement - The target element to be updated
     * @param strategy - The merge strategy to be applied
     * @returns Return false to cancel the DOM update
     */
    beforeDocumentUpdate?: (triggerElement: HTMLElement, mergeElement: HTMLElement, strategy: MergeStrategyType) => boolean,
    /**
     * Called after the DOM has been updated.
     * @param triggerElement - The element that triggered the update
     */
    afterDocumentUpdate?: (triggerElement: HTMLElement) => void
    /**
     * Called when a new element with data-rx attributes is added to the DOM.
     * @param addedElement - The newly added element
     * @remarks Fired by MutationObserver when elements are dynamically added.
     */
    onElementAdded?: (addedElement: HTMLElement) => void,
    /**
     * Called after an element has been morphed (intelligently updated).
     * @param morphedElement - The element that was morphed
     * @remarks Only fired when using the 'morph' merge strategy.
     */
    onElementMorphed?: (morphedElement: HTMLElement) => void,
    /**
     * Called when an element with data-rx attributes is removed from the DOM.
     * @param removedElement - The element being removed
     * @remarks Used for cleanup of event listeners and observers.
     */
    onElementRemoved?: (removedElement: HTMLElement) => void,
    /**
     * Called when an error occurs during trigger processing.
     * @param triggerElement - The element where the error occurred
     * @param error - The error that was caught
     */
    onElementTriggerError?: (triggerElement: HTMLElement, error: unknown) => void,
    /**
     * Called during file upload progress.
     * @param fileInput - The file input element
     * @param progressContext - Upload progress information
     * @remarks Useful for displaying upload progress bars.
     */
    onFileUploadProgress?: (fileInput: HTMLInputElement, progressContext: FileUploadProgressContext) => void,
    /**
     * Called when files are selected in a file input.
     * @param fileInput - The file input element
     * @param files - Array of selected file information
     * @param error - Error if file selection failed
     */
    onFileSelected?: (fileInput: HTMLInputElement, files: FileInfo[], error?: Error) => void,
}

/**
 * Element-specific callbacks for individual RazorX elements.
 * @remarks
 * These callbacks are attached to specific elements and only fire for that element.
 * Set via element._rxCallbacks property.
 * All callbacks are optional.
 */
export type ElementCallbacks = {
    /**
     * Called before this element sends a fetch request.
     * @param requestConfiguration - The request configuration (can be modified)
     * @remarks Call abort() to cancel the request.
     */
    beforeFetch?: (requestConfiguration: RequestConfiguration) => void,
    /**
     * Called after this element receives a fetch response.
     * @param requestDetail - The final request details that were sent
     * @param response - The fetch response object
     */
    afterFetch?: (requestDetail: RequestDetail, response: Response) => void,
    /**
     * Called before this element triggers a DOM update.
     * @param mergeElement - The target element to be updated
     * @param strategy - The merge strategy to be applied
     * @returns Return false to cancel the DOM update
     */
    beforeDocumentUpdate?: (mergeElement: HTMLElement, strategy: MergeStrategyType) => boolean,
    /**
     * Called after this element has triggered a DOM update.
     */
    afterDocumentUpdate?: () => void,
    /**
     * Called when an error occurs during this element's trigger processing.
     * @param error - The error that was caught
     */
    onElementTriggerError?: (error: unknown) => void,
    /**
     * Called during file upload progress for this element's file input.
     * @param progressContext - Upload progress information
     */
    onFileUploadProgress?: (progressContext: FileUploadProgressContext) => void,
    /**
     * Called when files are selected in this element's file input.
     * @param files - Array of selected file information
     * @param error - Error if file selection failed
     */
    onFileSelected?: (files: FileInfo[], error?: Error) => void,
}

/**
 * Configuration object passed to beforeFetch callbacks.
 * @remarks
 * Allows inspection and modification of the request before it's sent.
 * Call abort() to cancel the request.
 */
export type RequestConfiguration = {
    /** The DOM event that triggered this request */
    trigger: Event,
    /** The URL or path for the request */
    action: string,
    /** The HTTP method to use */
    method: HttpMethod,
    /** Request headers (can be modified) */
    headers: Headers,
    /** Request body (FormData or JSON string) */
    body: FormData | string | undefined,
    /**
     * Aborts the request before it's sent.
     * @param reason - Optional reason for aborting
     */
    abort: (reason?: string) => void
}

/**
 * Final request details passed to fetch().
 * @remarks
 * Read-only snapshot of the request that was actually sent.
 * Provided to afterFetch callbacks.
 */
export type RequestDetail = {
    /** The URL or path for the request */
    action: string,
    /** The HTTP method used */
    method: HttpMethod,
    /** How redirects are handled */
    redirect: FetchRedirect,
    /** Request body that was sent */
    body: FormData | string | undefined,
    /** Request headers that were sent */
    headers: Headers,
    /** AbortSignal for the request */
    signal: AbortSignal,
}

/**
 * File upload progress information.
 * @remarks
 * Provided to onFileUploadProgress callbacks during file uploads.
 */
export type FileUploadProgressContext = {
    /** The file being uploaded */
    file: File,
    /** Bytes uploaded so far */
    loaded: number,
    /** Total file size in bytes */
    total: number,
    /** Upload percentage (0-100) */
    percentage: number,
}

/**
 * Defines how a fragment should be merged into the DOM.
 * @remarks
 * Sent by the server in the rx-merge response header.
 */
export type MergeStrategy = {
    /** ID of the target element to update */
    target: string,
    /** The merge strategy to apply */
    strategy: MergeStrategyType
}

/**
 * Supported HTTP methods for RazorX requests.
 */
export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

/**
 * Fetch API redirect handling modes.
 * @see https://developer.mozilla.org/en-US/docs/Web/API/Request/redirect
 */
export type FetchRedirect = "follow" | "error" | "manual";

/**
 * DOM merge strategies for fragment updates.
 * @remarks
 * - InsertPosition: DOM standard positions (beforebegin, afterbegin, beforeend, afterend)
 * - swap: Replace entire target element
 * - swapInner: Replace target's innerHTML
 * - morph: Intelligent diff-based update using Idiomorph
 * - remove: Remove the target element
 */
export type MergeStrategyType = InsertPosition | "swap" | "swapInner" | "morph" | "remove";

/**
 * RazorX-specific response headers sent by the server.
 * @remarks
 * These headers control client-side behavior after receiving a response.
 * - rx-merge: Fragment merge instructions (JSON)
 * - rx-morph-ignore-active: Preserve active element value during morph
 * - rx-trigger-close-dialog: Close a dialog element (JSON)
 * - rx-trigger-focus-element: Focus an element (JSON)
 * - rx-trigger-set-state: Set browser storage state (JSON)
 * - rx-trigger-toast: Display a toast notification (JSON)
 */
export type RxResponseHeaders = 
    "rx-merge" | 
    "rx-morph-ignore-active" | 
    "rx-trigger-close-dialog" | 
    "rx-trigger-focus-element" | 
    "rx-trigger-set-state" |
    "rx-trigger-toast";

/**
 * Types of special triggers that fire automatically.
 * @remarks
 * - initialized: Fires once when element is added to DOM
 * - poll: Fires repeatedly at specified intervals
 * - revealed: Fires when element enters viewport
 */
export type SpecialTriggerType = 'initialized' | 'poll' | 'revealed';

/**
 * Base configuration for special triggers.
 */
export type SpecialTriggerConfig = {
    /** The type of special trigger */
    type: SpecialTriggerType;
}

/**
 * Configuration for initialized triggers.
 * @remarks
 * Fires once when the element is added to the DOM.
 * Useful for loading initial content or starting animations.
 */
export type InitializedTrigger = SpecialTriggerConfig & {
    /** Trigger type discriminator */
    type: 'initialized';
    /** Optional delay in milliseconds before triggering @default 0 */
    delay?: number;
}

/**
 * Configuration for polling triggers.
 * @remarks
 * Fires repeatedly at specified intervals.
 * Useful for real-time updates or monitoring.
 * Automatically cleaned up when element is removed.
 */
export type PollTrigger = SpecialTriggerConfig & {
    /** Trigger type discriminator */
    type: 'poll';
    /** Polling interval in milliseconds @default 1000 */
    interval?: number;
}

/**
 * Configuration for revealed triggers.
 * @remarks
 * Fires when the element enters the viewport.
 * Uses IntersectionObserver for efficient detection.
 * Useful for lazy loading or scroll-triggered animations.
 */
export type RevealedTrigger = SpecialTriggerConfig & {
    /** Trigger type discriminator */
    type: 'revealed';
    /** Root margin for intersection observer @default "0px" */
    margin?: string;
}

/**
 * Union type of all special trigger configurations.
 */
export type SpecialTrigger = InitializedTrigger | PollTrigger | RevealedTrigger;

/**
 * Definition of a trigger that can be either a DOM event name or special trigger.
 * @remarks
 * Used in data-rx-trigger attribute parsing.
 * @example
 * ```html
 * <!-- DOM event -->
 * <button data-rx-trigger="click">Click me</button>
 * 
 * <!-- Special trigger -->
 * <div data-rx-trigger='[{"type":"poll","interval":5000}]'>Poll every 5s</div>
 * ```
 */
export type TriggerDefinition = string | SpecialTrigger;

/**
 * Server trigger to close a dialog element.
 * @remarks
 * Sent via rx-trigger-close-dialog response header.
 */
export type RxCloseDialogTrigger = {
    /** ID of the dialog element to close */
    dialogId: string,
    /** Optional data to pass to close handler */
    onCloseData?: string,
    /** Optional form ID to reset after closing */
    resetFormId?: string
}

/**
 * Server trigger to focus an element.
 * @remarks
 * Sent via rx-trigger-focus-element response header.
 */
export type RxFocusElementTrigger = {
    /** ID of the element to focus */
    elementId: string,
    /** Whether to position cursor at end of content */
    positionCursorEnd: boolean,
}

/**
 * Server trigger to set browser storage state.
 * @remarks
 * Sent via rx-trigger-set-state response header.
 * State can be included in subsequent requests via data-rx-include-state.
 */
export type RxSetStateTrigger = {
    /** Storage type: Session (sessionStorage) or Persistent (localStorage) */
    scope: "Session" | "Persistent"
    /** Storage key name */
    key: string,
    /** Value to store (null to remove) */
    value?: string | null,
    /** Whether to update URL query parameters */
    updateUrl?: boolean,
}

/**
 * Toast notification types affecting visual style.
 */
export type ToastType = "Info" | "Success" | "Warning" | "Error";

/**
 * Vertical positioning for toast notifications.
 */
export type ToastVerticalPosition = "Top" | "Center" | "Bottom";

/**
 * Horizontal positioning for toast notifications.
 */
export type ToastHorizontalPosition = "Left" | "Middle" | "Right";

/**
 * Server trigger to display a toast notification.
 * @remarks
 * Sent via rx-trigger-toast response header.
 * Multiple toasts can be displayed simultaneously.
 */
export type RxToastTrigger = {
    /** The message to display */
    message: string,
    /** Toast type for styling */
    type: ToastType,
    /** Display duration in milliseconds */
    duration: number,
    /** Vertical screen position */
    verticalPosition: ToastVerticalPosition,
    /** Horizontal screen position */
    horizontalPosition: ToastHorizontalPosition,
    /** Whether clicking dismisses the toast */
    clickToDismiss: boolean
}

type ElementTriggerState = {
    triggers: Set<string>;
    intervalId?: ReturnType<typeof setInterval>;
    observer?: IntersectionObserver;
}

type DelegatedActionConfig = {
    action: string;
    method: string;
    sourceId: string;
    timestamp: number;
}

type ToastState = {
    element: HTMLElement;
    zone: string;
    stackIndex: number;
    timeoutId?: ReturnType<typeof setTimeout>;
    clickHandler?: (e: MouseEvent) => void;
}

type ParsedRxHeaders = {
    merge?: MergeStrategy[];
    setState?: RxSetStateTrigger[];
    closeDialog?: RxCloseDialogTrigger;
    focusElement?: RxFocusElementTrigger;
    toast?: RxToastTrigger;
    morphIgnoreActive?: boolean;
};

const RxRequestHeader = "rx-request";

const _processedScriptTag = "data-rx-script-processed";

const _requestRefTracker: Set<string> = new Set();

const _debouncedRequests: Map<string, (() => Promise<void>) & { _cleanup?: () => void }> = new Map();

const _elementCache: Map<string, HTMLElement> = new Map();

const _elementTriggerState: WeakMap<HTMLElement, ElementTriggerState> = new WeakMap();

const _delegatedActionConfigs: WeakMap<HTMLElement, DelegatedActionConfig> = new WeakMap();

const _elementOriginalDisabledState: WeakMap<HTMLElement, Set<Element>> = new WeakMap();

const _activeLoadingIndicators: Map<string, Set<string>> = new Map();

const _activeToasts: Map<string, ToastState> = new Map();

const _toastZones: Map<string, string[]> = new Map();

const _fetchRedirect: FetchRedirect = "follow";

const _callbacks: DocumentCallbacks = {};

let _loadingClasses = {
    hidden: 'rx-loading-hidden',
    visible: 'rx-loading-visible'
}

let _toastClasses = {
    base: 'rx-toast',
    info: 'rx-toast-info',
    success: 'rx-toast-success',
    warning: 'rx-toast-warning',
    error: 'rx-toast-error',
    // Position classes
    topLeft: 'rx-toast-top-left',
    topMiddle: 'rx-toast-top-middle',
    topRight: 'rx-toast-top-right',
    centerLeft: 'rx-toast-center-left',
    centerMiddle: 'rx-toast-center-middle',
    centerRight: 'rx-toast-center-right',
    bottomLeft: 'rx-toast-bottom-left',
    bottomMiddle: 'rx-toast-bottom-middle',
    bottomRight: 'rx-toast-bottom-right'
}

const _addCallbacks = (callbacks: DocumentCallbacks): void => {
    _callbacks.afterDocumentProcessed = callbacks.afterDocumentProcessed;
    _callbacks.afterDocumentUpdate = callbacks.afterDocumentUpdate;
    _callbacks.afterFetch = callbacks.afterFetch;
    _callbacks.afterInitializeElement = callbacks.afterInitializeElement;
    _callbacks.beforeDocumentProcessed = callbacks.beforeDocumentProcessed;
    _callbacks.beforeDocumentUpdate = callbacks.beforeDocumentUpdate;
    _callbacks.beforeFetch = callbacks.beforeFetch;
    _callbacks.beforeInitializeElement = callbacks.beforeInitializeElement;
    _callbacks.onElementAdded = callbacks.onElementAdded;
    _callbacks.onElementMorphed = callbacks.onElementMorphed;
    _callbacks.onElementRemoved = callbacks.onElementRemoved;
    _callbacks.onElementTriggerError = callbacks.onElementTriggerError;
    _callbacks.onFileUploadProgress = callbacks.onFileUploadProgress;
    _callbacks.onFileSelected = callbacks.onFileSelected;
}

// ============================================================================
// Event Dispatching System
// ============================================================================

const _dispatchBeforeDocumentProcessed = (): void => {
    document.dispatchEvent(new CustomEvent('rx:before-document-processed'));
}

const _dispatchAfterDocumentProcessed = (): void => {
    document.dispatchEvent(new CustomEvent('rx:after-document-processed'));
}

const _dispatchBeforeInitializeElement = (element: HTMLElement): CustomEvent => {
    const event = new CustomEvent('rx:before-initialize-element', {
        detail: { element },
        cancelable: true
    });
    document.dispatchEvent(event);
    return event;
}

const _dispatchAfterInitializeElement = (element: HTMLElement): void => {
    document.dispatchEvent(new CustomEvent('rx:after-initialize-element', {
        detail: { element }
    }));
}

const _dispatchBeforeFetch = (triggerElement: HTMLElement, requestConfiguration: RequestConfiguration): void => {
    document.dispatchEvent(new CustomEvent('rx:before-fetch', {
        detail: { triggerElement, requestConfiguration }
    }));
}

const _dispatchAfterFetch = (triggerElement: HTMLElement, requestDetail: RequestDetail, response: Response): void => {
    document.dispatchEvent(new CustomEvent('rx:after-fetch', {
        detail: { triggerElement, requestDetail, response }
    }));
}

const _dispatchBeforeDocumentUpdate = (triggerElement: HTMLElement, mergeElement: HTMLElement, strategy: MergeStrategyType): CustomEvent => {
    const event = new CustomEvent('rx:before-document-update', {
        detail: { triggerElement, mergeElement, strategy },
        cancelable: true
    });
    document.dispatchEvent(event);
    return event;
}

const _dispatchAfterDocumentUpdate = (triggerElement: HTMLElement): void => {
    document.dispatchEvent(new CustomEvent('rx:after-document-update', {
        detail: { triggerElement }
    }));
}

const _dispatchOnElementAdded = (addedElement: HTMLElement): void => {
    document.dispatchEvent(new CustomEvent('rx:element-added', {
        detail: { element: addedElement }
    }));
}

const _dispatchOnElementMorphed = (morphedElement: HTMLElement): void => {
    document.dispatchEvent(new CustomEvent('rx:element-morphed', {
        detail: { element: morphedElement }
    }));
}

const _dispatchOnElementRemoved = (removedElement: HTMLElement): void => {
    document.dispatchEvent(new CustomEvent('rx:element-removed', {
        detail: { element: removedElement }
    }));
}

const _dispatchOnElementTriggerError = (triggerElement: HTMLElement, error: unknown): void => {
    document.dispatchEvent(new CustomEvent('rx:element-trigger-error', {
        detail: { triggerElement, error }
    }));
}

const _dispatchOnFileUploadProgress = (fileInput: HTMLInputElement, progressContext: FileUploadProgressContext): void => {
    document.dispatchEvent(new CustomEvent('rx:file-upload-progress', {
        detail: { fileInput, progressContext }
    }));
}

const _dispatchOnFileSelected = (fileInput: HTMLInputElement, files: FileInfo[], error?: Error): void => {
    document.dispatchEvent(new CustomEvent('rx:file-selected', {
        detail: { fileInput, files, error }
    }));
}

// ============================================================================
// End Event Dispatching System
// ============================================================================

const _isFirefox = navigator.userAgent.toLowerCase().includes("firefox");

const _init = (options?: Options, callbacks?: DocumentCallbacks): void => {

    // initialization

    if (document.rxMutationObserver) {
        // Document already processed - this is intentional
        console.debug("Document already has active MutationObserver");
        return;
    }

    // Configure loading indicator classes
    if (options?.loadingIndicatorClasses) {
        _loadingClasses = {
            ..._loadingClasses,
            ...options.loadingIndicatorClasses
        }
    }
    
    // Configure toast classes
    if (options?.toastClasses) {
        _toastClasses = {
            ..._toastClasses,
            ...options.toastClasses
        }
    }

    // Add cleanup for page unload scenarios
    window.addEventListener('beforeunload', () => {
        if (document.rxMutationObserver) {
            document.rxMutationObserver.disconnect();
            _debouncedRequests.forEach(req => req._cleanup?.());
            _debouncedRequests.clear();
            clearElementCache();
            cleanupAllToasts();
        }
    });

    let _requestQueue = Promise.resolve();

    document.rxMutationObserver = new MutationObserver((recs: MutationRecord[]): void => {
        recs.forEach((rec: MutationRecord): void => {
            if (rec.type !== "childList") {
                return;
            }
            rec.removedNodes.forEach((node: Node): void => { 
                if (!(node instanceof HTMLElement)) {
                    return;
                }
                removeTriggers(node);
                if (node.id) {
                    invalidateCachedElement(node.id);
                    if (node.classList.contains(_toastClasses.base)) {
                        _activeToasts.forEach((state, toastId) => {
                            if (state.element === node) {
                                if (state.zone && _toastZones.has(state.zone)) {
                                    const zoneToasts = _toastZones.get(state.zone)!;
                                    const index = zoneToasts.indexOf(toastId);
                                    if (index > -1) {
                                        zoneToasts.splice(index, 1);
                                    }
                                    if (zoneToasts.length === 0) {
                                        _toastZones.delete(state.zone);
                                    } else {
                                        reflowZone(state.zone);
                                    }
                                }
                                if (state.timeoutId) {
                                    clearTimeout(state.timeoutId);
                                }
                                _activeToasts.delete(toastId);
                            }
                        });
                    }
                }
                node.querySelectorAll('[id]').forEach((child: Element) => {
                    if (child.id) {
                        invalidateCachedElement(child.id);
                    }
                });
                if (_callbacks.onElementRemoved) {
                    _callbacks.onElementRemoved(node);
                }
                _dispatchOnElementRemoved(node);
            });
            rec.addedNodes.forEach((node: Node): void => { 
                if (!(node instanceof HTMLElement)) {
                    return;
                }
                // Check for duplicate IDs
                if (node.id && _elementCache.has(node.id)) {
                    const cachedElement = _elementCache.get(node.id)!;
                    if (document.contains(cachedElement)) {
                        throw new Error(`Duplicate element ID detected: "${node.id}". An element with this ID already exists in the DOM.`);
                    }
                    // If cached element is detached, invalidate it (self-healing)
                    _elementCache.delete(node.id);
                }
                normalizeScriptTags(node);
                addTriggers(node);
                if (_callbacks.onElementAdded) {
                    _callbacks.onElementAdded(node);
                }
                _dispatchOnElementAdded(node);
            });
        });
    });

    if (callbacks) {
        _addCallbacks(callbacks);
    }

    document.addEventListener("DOMContentLoaded", DOMContentLoaded);

    // configuration functions

    function parseTriggers(triggerAttr: string | undefined): TriggerDefinition[] {
        if (!triggerAttr || triggerAttr.trim() === "") {
            return [];
        }    
        const trimmed = triggerAttr.trim();
        if (trimmed.startsWith("[") || trimmed.startsWith("{")) {
            try {
                const parsed = JSON.parse(trimmed);
                if (!Array.isArray(parsed) && typeof parsed === 'object' && parsed && parsed.type) {
                    return [parsed as SpecialTrigger];
                }
                if (Array.isArray(parsed)) {
                    return parsed
                        .filter((item): boolean => {
                            return (typeof item === "string" && item.trim() !== "") ||
                                   (typeof item === "object" && item && item.type);
                        })
                        .map((item): TriggerDefinition => {
                            if (typeof item === "string") {
                                return item.trim();
                            }
                            return item as SpecialTrigger;
                        });
                }
            } catch {
                console.error(`Failed to parse triggers as JSON: ${trimmed}`);
                return [];
            }
        }
        if (trimmed.includes(" ")) {
            throw new Error(`Triggers must use JSON array format, not space-separated values: "${trimmed}"`);
        }
        return [trimmed];
    }

    function parseStateKeys(stateAttr: string | undefined): string[] {
        if (!stateAttr || stateAttr.trim() === "") {
            return [];
        }
        const trimmed = stateAttr.trim();
        if (trimmed.startsWith("[") && trimmed.endsWith("]")) {
            try {
                const parsed = JSON.parse(trimmed);
                if (Array.isArray(parsed)) {
                    return parsed
                        .filter((item): boolean => typeof item === "string" && item.trim() !== "")
                        .map((item): string => item.trim());
                }
            } catch {
                console.warn(`Failed to parse state keys as JSON: ${trimmed}`);
                return [];
            }
        }
        if (trimmed.includes(" ")) {
            const jsonArray = `["${trimmed.split(' ').join('", "')}"]`;
            throw new Error(`State keys must use JSON array format: ${jsonArray} (not space-separated: "${trimmed}")`);
        }
        return [trimmed];
    }

    function getCachedElement(id: string): HTMLElement | null {
        if (_elementCache.has(id)) {
            return _elementCache.get(id)!;
        }
        const element = document.getElementById(id);
        if (element) {
            _elementCache.set(id, element);
        }
        return element;
    }

    function clearElementCache(): void {
        _elementCache.clear();
    }

    function invalidateCachedElement(id: string): void {
        _elementCache.delete(id);
    }

    function toggleLoadingIndicator(ele: HTMLElement, show: boolean): void {
        const indicatorId = ele.dataset.rxLoadingIndicator;
        if (!indicatorId) {
            return;
        }
        if (show) {
            if (!_activeLoadingIndicators.has(indicatorId)) {
                _activeLoadingIndicators.set(indicatorId, new Set());
            }
            const activeElements = _activeLoadingIndicators.get(indicatorId)!;
            activeElements.add(ele.id);
            const indicator = getCachedElement(indicatorId);
            if (indicator) {
                indicator.classList.remove(_loadingClasses.hidden);
                indicator.classList.add(_loadingClasses.visible);
            } else if (activeElements.size === 1) { // Only warn once
                console.warn(`Loading indicator element '${indicatorId}' not found`);
            }
        } else {
            const activeElements = _activeLoadingIndicators.get(indicatorId);
            if (!activeElements) {
                return;
            }
            activeElements.delete(ele.id);
            // Only hide if no other elements are using it
            if (activeElements.size === 0) {
                const indicator = getCachedElement(indicatorId);
                if (indicator) {
                    indicator.classList.remove(_loadingClasses.visible);
                    indicator.classList.add(_loadingClasses.hidden);
                }
                _activeLoadingIndicators.delete(indicatorId);
            }
        }
    }


    function setTriggers(ele: HTMLElement): void {
        if (ele.dataset.rxAction && (!ele.id || ele.id.trim() === "")) {
            throw new Error(`Element with "data-rx-action" must have a unique ID.`);
        }
        if (!ele.dataset.rxAction && ele.dataset.rxTrigger) {
            console.warn(`Element has data-rx-trigger but no data-rx-action. Triggers will not function.`, ele);
        }
        let rxTrigger = ele.dataset.rxTrigger;
        if (!rxTrigger) {
            rxTrigger = ele.matches("form")
                ? "submit" 
                : ele.matches("input:not([type=button]),select,textarea") ? "change" : "click";
            ele.setAttribute("data-rx-trigger", rxTrigger);
        }
        const triggers = parseTriggers(ele.dataset.rxTrigger);
        const hasOnlySpecialTriggers = triggers.length > 0 && triggers.every((trigger) => 
            typeof trigger === 'object' && 'type' in trigger
        );
        if (hasOnlySpecialTriggers) {
            if (ele.dataset.rxDebounce !== undefined) {
                console.warn(
                    `Element ${ele.id} has data-rx-debounce="${ele.dataset.rxDebounce}" but only contains special triggers (initialized, poll, revealed). ` +
                    `The debounce attribute has no effect on special triggers. ` +
                    `For the initialized trigger, use the 'delay' property instead.`
                );
            }
            if (ele.dataset.rxDisableQueueing !== undefined) {
                console.warn(
                    `Element ${ele.id} has data-rx-disable-queueing="${ele.dataset.rxDisableQueueing}" but only contains special triggers (initialized, poll, revealed). ` +
                    `The disable-queueing attribute has no effect on special triggers.`
                );
            }
            if (ele.dataset.rxAllowEventDefault !== undefined) {
                console.warn(
                    `Element ${ele.id} has data-rx-allow-event-default="${ele.dataset.rxAllowEventDefault}" but only contains special triggers (initialized, poll, revealed). ` +
                    `The allow-event-default attribute has no effect on special triggers as they use CustomEvents, not DOM events.`
                );
            }
        }
        
        if (ele.dataset.rxDelegateActionTo) {
            const hasSpecialTrigger = triggers.some((trigger) => 
                typeof trigger === 'object' && 'type' in trigger
            );
            if (hasSpecialTrigger) {
                throw new Error(
                    `Element ${ele.id} cannot use special triggers ` +
                    `with data-rx-delegate-action-to. Special triggers have their own lifecycle and cannot be delegated to another element.`
                );
            }
            const triggerState = _elementTriggerState.get(ele) || { triggers: new Set() };
            triggers.forEach((trigger): void => {
                if (typeof trigger === 'string') {
                    if (!triggerState.triggers.has(trigger)) {
                        ele.addEventListener(trigger, elementDelegateActionEventHandler);
                        triggerState.triggers.add(trigger);
                    }
                }
            });
            _elementTriggerState.set(ele, triggerState);
        } else {
            const triggerState = _elementTriggerState.get(ele) || { triggers: new Set() };
            triggers.forEach((trigger): void => {
                if (typeof trigger === 'object' && trigger.type) {
                    // Handle special triggers
                    switch (trigger.type) {
                        case 'initialized':
                            initializedTrigger(ele, trigger.delay);
                            triggerState.triggers.add('initialized' + (trigger.delay ? ':' + trigger.delay : ''));
                            break;
                        case 'poll':
                            pollTrigger(ele, trigger.interval);
                            triggerState.triggers.add('poll:' + (trigger.interval || 1000));
                            break;
                        case 'revealed':
                            revealedTrigger(ele, trigger.margin);
                            triggerState.triggers.add('revealed:' + (trigger.margin || '0px'));
                            break;
                        default:
                            console.warn(`Unknown special trigger type: ${(trigger as SpecialTriggerConfig).type}`);
                    }
                } else if (typeof trigger === 'string') {
                    // Handle regular string triggers
                    if (!triggerState.triggers.has(trigger)) {
                        ele.addEventListener(trigger, elementTriggerEventHandler);
                        triggerState.triggers.add(trigger);
                    }
                }
            });
            _elementTriggerState.set(ele, triggerState);
        }
    }

    function initializedTrigger(ele: HTMLElement, delay?: number): void {
        const evt = new CustomEvent('initialized', { detail: { type: 'initialized', delay: delay } });
        if (delay && delay > 0) {
            setTimeout(() => {
                elementTriggerProcessor(ele, evt);
            }, delay);
        } else {
            elementTriggerProcessor(ele, evt);
        }
    }

    function pollTrigger(ele: HTMLElement, interval?: number): void {
        const existingState = _elementTriggerState.get(ele);
        if (existingState?.intervalId) {
            console.warn(`Polling already active for element ${ele.id}`);
            return;
        }
        let pollInterval = interval || 1000;
        if (pollInterval <= 0) {
            pollInterval = 1000;
            console.warn(`Invalid poll interval ${pollInterval} for element ${ele.id}. Using default 1000ms.`);
        }
        const evt = new CustomEvent('poll', { detail: { type: 'poll', interval: pollInterval } });
        const intervalId = setInterval(() => {
            elementTriggerProcessor(ele, evt);
        }, pollInterval);
        const state = _elementTriggerState.get(ele) || { triggers: new Set() };
        state.intervalId = intervalId;
        _elementTriggerState.set(ele, state);
    }

    function revealedTrigger(ele: HTMLElement, margin?: string): void {
        const existingState = _elementTriggerState.get(ele);
        if (existingState?.observer) {
            console.warn(`Observer already active for element ${ele.id}`);
            return;
        }
        const rootMargin = margin || "0px";
        const marginPattern = /^-?\d+px(\s+-?\d+px)*$/;
        if (!marginPattern.test(rootMargin)) {
            console.warn(`Invalid margin format "${rootMargin}" for element ${ele.id}. Must be CSS margin format (e.g., "200px", "100px 0px"). Using default "0px".`);
        }
        const observer = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting && entry.target === ele) {
                        const evt = new CustomEvent('revealed', { 
                            detail: { type: 'revealed', margin: rootMargin } 
                        });
                        elementTriggerProcessor(ele, evt);
                        observer.disconnect();
                        const state = _elementTriggerState.get(ele);
                        if (state?.observer === observer) {
                            delete state.observer;
                        }
                    }
                });
            },
            { rootMargin }
        );
        observer.observe(ele);
        const triggerState = _elementTriggerState.get(ele) || { triggers: new Set() };
        triggerState.observer = observer;
        _elementTriggerState.set(ele, triggerState);
    }

    function addTriggers(ele: HTMLElement): void {
        if (ele.dataset.rxAction && (!_callbacks.beforeInitializeElement || _callbacks.beforeInitializeElement(ele))) {
            const beforeEvent = _dispatchBeforeInitializeElement(ele);
            if (beforeEvent.defaultPrevented) {
                return;
            }
            configureElement(ele);
            if (!(ele instanceof HTMLInputElement && ele.type === 'file')) {
                setTriggers(ele);
            }
            if (_callbacks.afterInitializeElement) {
                _callbacks.afterInitializeElement(ele);
            }
            _dispatchAfterInitializeElement(ele);
        }
        const children = ele.children;
        if (children?.length <= 0) {
            return;
        } 
        for (let i = 0; i < children.length; i++) {
            const child = children[i];
            if (child instanceof HTMLElement) {
                addTriggers(child);
            }
        }
    }

    function removeTriggers(ele: HTMLElement): void {
        const triggerState = _elementTriggerState.get(ele);
        if (triggerState) {
            if (triggerState.intervalId) {
                clearInterval(triggerState.intervalId);
            }
            if (triggerState.observer) {
                triggerState.observer.unobserve(ele);
                triggerState.observer.disconnect();
            }
            _elementTriggerState.delete(ele);
        }
        if (ele.dataset.rxTrigger) {	
            const triggers = ele.dataset.rxTrigger.split(/\s+/);
            triggers.forEach((trigger): void => {
                ele.removeEventListener(trigger, elementTriggerEventHandler);
                ele.removeEventListener(trigger, elementDelegateActionEventHandler);
            });
        }
        const debouncedRequest = _debouncedRequests.get(ele.id);
        if (debouncedRequest) {
            debouncedRequest._cleanup?.();
            _debouncedRequests.delete(ele.id);
        }
        _delegatedActionConfigs.delete(ele);
        if (ele.dataset.rxLoadingIndicator && ele.id) {
            const indicatorId = ele.dataset.rxLoadingIndicator;
            const activeElements = _activeLoadingIndicators.get(indicatorId);
            if (activeElements) {
                activeElements.delete(ele.id);
                if (activeElements.size === 0) {
                    const indicator = getCachedElement(indicatorId);
                    if (indicator) {
                        indicator.classList.remove(_loadingClasses.visible);
                        indicator.classList.add(_loadingClasses.hidden);
                    }
                    _activeLoadingIndicators.delete(indicatorId);
                }
            }
        }
        const children = ele.children;
        if (children?.length <= 0) {
            return;
        } 
        for (let i = 0; i < children.length; i++) {
            const child = children[i];
            if (child instanceof HTMLElement) {
                removeTriggers(child);
            }
        }
    }

    function configureElement(ele: HTMLElement): void {
        if (!ele.id || ele.id.trim() === "") {
            const err = `Element with "data-rx-action" must have a unique ID.`;
            throw new Error(err);
        }
        //id is required and mustn't be modified
        Object.freeze(ele.id);
        //enforce the existence of the element rxTrigger, addRxCallbacks() and _rxCallbacks properties
        const elementCallbacks: ElementCallbacks = {};
        const addCallbacks = (callbacks: ElementCallbacks): void => {
            elementCallbacks.afterDocumentUpdate = callbacks.afterDocumentUpdate;
            elementCallbacks.afterFetch = callbacks.afterFetch;
            elementCallbacks.beforeDocumentUpdate = callbacks.beforeDocumentUpdate;
            elementCallbacks.beforeFetch = callbacks.beforeFetch;
            elementCallbacks.onElementTriggerError = callbacks.onElementTriggerError;
            elementCallbacks.onFileUploadProgress = callbacks.onFileUploadProgress;
            elementCallbacks.onFileSelected = callbacks.onFileSelected;
        }
        Object.defineProperty(ele, "addRxCallbacks", {
            value: addCallbacks,
            writable: false,
        });
        Object.defineProperty(ele, "_rxCallbacks", {
            value: elementCallbacks,
            writable: false,
        });
        if (ele instanceof HTMLInputElement && ele.type === 'file') {
            configureFileInput(ele);
            return;
        }
    }
    
    function configureFileInput(fileInput: HTMLInputElement): void {
        const invalidRxAttributes: (keyof typeof fileInput.dataset)[] = [
            'rxMethod',
            'rxTrigger', 
            'rxAllowEventDefault',
            'rxDisableInFlight',
            'rxDebounce',
            'rxDisableQueueing',
            'rxDelegateActionTo',
            'rxIncludeState',
            'rxLoadingIndicator'
        ];
        for (const attr of invalidRxAttributes) {
            if (fileInput.dataset[attr] !== undefined) {
                const htmlAttr = attr.replace(/([A-Z])/g, '-$1').toLowerCase();
                const errorMsg = `File input "${fileInput.id}" cannot have data-${htmlAttr} attribute. File inputs only support data-rx-action.`;
                throw new Error(errorMsg);
            }
        }
        if (fileInput.dataset.rxFileUploadProgressId) {
            const progressId = fileInput.dataset.rxFileUploadProgressId;
            const progressElement = document.getElementById(progressId);
            if (!progressElement) {
                throw new Error(`File input "${fileInput.id}" references non-existent progress element: ${progressId}`);
            }
            if (!(progressElement instanceof HTMLProgressElement)) {
                throw new Error(`File input "${fileInput.id}" data-rx-file-upload-progress-id must reference a <progress> element, found: <${progressElement.tagName.toLowerCase()}>`);
            }
        }
        fileInput.addEventListener('change', function(this: HTMLInputElement) {
            if (!this.files || this.files.length === 0) return;
            const fileInfos: FileInfo[] = [];
            let totalSize = 0;
            for (let i = 0; i < this.files.length; i++) {
                const file = this.files[i];
                if (file) {
                    fileInfos.push({
                        fileName: file.name,
                        size: formatBytes(file.size),
                        sizeInBytes: file.size
                    });
                    totalSize += file.size;
                }
            }
            let error: Error | undefined;
            if (this.dataset.rxFileUploadMaxSize) {
                const maxSize = parseInt(this.dataset.rxFileUploadMaxSize, 10);
                if (!isNaN(maxSize) && maxSize > 0 && totalSize > maxSize) {
                    // Create appropriate error message
                    if (this.files.length === 1 && fileInfos[0]) {
                        error = new Error(`File "${fileInfos[0].fileName}" exceeds maximum size of ${formatBytes(maxSize)}`);
                    } else {
                        error = new Error(`Selected files exceed the maximum allowed size of ${formatBytes(maxSize)}`);
                    }
                }
            }
            if (this._rxCallbacks?.onFileSelected) {
                this._rxCallbacks.onFileSelected(fileInfos, error);
            }
            if (_callbacks.onFileSelected) {
                _callbacks.onFileSelected(this, fileInfos, error);
            }
            _dispatchOnFileSelected(this, fileInfos, error);
            if (error) {
                sendError(this, error);
                this.value = '';
                if (this.dataset.rxFileUploadProgressId) {
                    const progressElement = document.getElementById(this.dataset.rxFileUploadProgressId);
                    if (progressElement && progressElement instanceof HTMLProgressElement) {
                        progressElement.value = 0;
                    }
                }
            }
        });
    }

    // event handlers

    function DOMContentLoaded(): void {
        document.rxMutationObserver.observe(document.documentElement, { childList: true, subtree: true });
        if (_callbacks.beforeDocumentProcessed) {
            _callbacks.beforeDocumentProcessed();
        }
        _dispatchBeforeDocumentProcessed();
        addTriggers(document.body);
        if (_callbacks.afterDocumentProcessed) {
            _callbacks.afterDocumentProcessed();
        }
        _dispatchAfterDocumentProcessed();
    }

    function elementDelegateActionEventHandler(this: HTMLElement): void {
        const delegateTargetId = this.dataset.rxDelegateActionTo ?? "";
        const delegateTarget = getCachedElement(delegateTargetId);
        if (!delegateTarget) {
            const err = `Element ${this.id} with "data-rx-delegate-action-to" ${this.dataset.rxDelegateActionTo} does not reference a valid DOM element.`;
            throw new Error(err);
        }
        _delegatedActionConfigs.set(delegateTarget, {
            action: this.dataset.rxAction!,  // Always exists - only elements with rxAction get here
            method: this.dataset.rxMethod ?? ((this.tagName === "FORM" || this.closest("form")) ? "POST" : "GET"),
            sourceId: this.id,
            timestamp: Date.now()
        });
        if (!delegateTarget.dataset.rxTrigger) {
            delegateTarget.addEventListener('click', delegatedActionTargetClickHandler, { once: true });
            delegateTarget.setAttribute('data-rx-trigger', 'delegate-one-shot');
        }
    }
    
    async function delegatedActionTargetClickHandler(this: HTMLElement, evt: Event): Promise<void> {
        evt.preventDefault();
        const config = _delegatedActionConfigs.get(this);
        if (!config) {
            console.warn(`No delegated action configuration found for element ${this.id}. The delegation may have expired.`);
            this.removeAttribute('data-rx-trigger'); // Clean up the temporary marker
            return;
        }
        _delegatedActionConfigs.delete(this);
        this.removeAttribute('data-rx-trigger'); // Clean up the temporary marker
        const syntheticElement = document.createElement('div');
        syntheticElement.id = `delegate-synthetic-${config.sourceId}-${Date.now()}`;
        syntheticElement.dataset.rxAction = config.action;
        syntheticElement.dataset.rxMethod = config.method;
        configureElement(syntheticElement);
        try {
            await elementTriggerProcessor(syntheticElement, evt);
        } catch (error) {
            sendError(this, error);
        }
    }

    function elementTriggerEventHandler(this: HTMLElement, evt: Event): void {
        const allowEventDefault = this.dataset.rxAllowEventDefault?.trim().toLowerCase();
        if (allowEventDefault !== undefined && allowEventDefault !== "" && allowEventDefault !== "true" && allowEventDefault !== "false") {
            console.warn(`The data-rx-allow-event-default attribute on element ${this.id} has an invalid value "${allowEventDefault}". Valid values are: no value (empty), "true", or "false"`);
        }
        if (allowEventDefault === undefined || allowEventDefault === "false") {
            evt.preventDefault();
        }
        const debounceValue = this.dataset.rxDebounce?.trim();
        if (debounceValue === undefined) {
            queue(this, evt);
            return;
        }
        const delay = parseInt(debounceValue, 10);
        if (Number.isNaN(delay) || delay <= 0) {
            console.warn(`The data-rx-debounce attribute on element ${this.id} is invalid. It must be a number > zero`);
            queue(this, evt);
            return;
        }
        let debounceElementTrigger = _debouncedRequests.get(this.id);
        if (debounceElementTrigger) {
            debounceElementTrigger();
        } else {
            debounceElementTrigger = debounce(this, evt, delay);
            _debouncedRequests.set(this.id, debounceElementTrigger);
            debounceElementTrigger();
        }
    }
    
    function queue(ele: HTMLElement, evt: Event): void {
        const disableQueueing = ele.dataset.rxDisableQueueing?.trim().toLowerCase();
        if (disableQueueing !== undefined && disableQueueing !== "" && disableQueueing !== "true" && disableQueueing !== "false") {
            console.warn(`The data-rx-disable-queueing attribute on element ${ele.id} is invalid. It should be either a Boolean (no value) or ="true" or ="false"`);
        }
        if (disableQueueing !== undefined && disableQueueing !== "false") {
            elementTriggerProcessor(ele, evt);
            return;
        }
        _requestQueue = _requestQueue.finally(async (): Promise<void> => {
            try {
                await elementTriggerProcessor(ele, evt);
            } catch (error: unknown) {
                sendError(ele, error);
            }
        });
    }

    function debounce(ele: HTMLElement, evt: Event, delay: number): (() => Promise<void>) & { _cleanup?: () => void } {
        let timeoutId: ReturnType<typeof setTimeout> | null = null;
        let pending: Array<{ 
            resolve: (value: void) => void; 
            reject: (reason?: unknown) => void 
        }> = [];
        const cleanup = (): void => {
            if (timeoutId) {
                clearTimeout(timeoutId);
                timeoutId = null;
            }
            pending.forEach(({ reject }): void => {
                reject(new Error(`Element ${ele.id} was removed from DOM`));
            });
            pending = [];
        };
        const debouncedFn = (): Promise<void> => {
            return new Promise((resolve, reject): void => {
                if (timeoutId) {
                    clearTimeout(timeoutId);
                }
                pending.push({ resolve, reject });
                timeoutId = setTimeout((): void => {
                    timeoutId = null;
                    Promise.resolve(queue(ele, evt))
                        .then((result: void): void => {
                            pending.forEach(({ resolve: res }): void => res(result)); 
                        })
                        .catch((error: unknown): void => {
                            pending.forEach(({ reject: rej }): void => rej(error));
                        })
                        .finally((): void => {
                            pending = []; 
                        });
                }, delay);
            });
        };
        debouncedFn._cleanup = cleanup;
        return debouncedFn;
    }
    
    // process request

    async function elementTriggerProcessor(ele: HTMLElement, evt: Event): Promise<void> {
        try {   
            _debouncedRequests.delete(ele.id);
            if (_requestRefTracker.has(ele.id)) {
                throw new Error(`Element ${ele.id} is already executing a request.`);
            }
            _requestRefTracker.add(ele.id);
            toggleLoadingIndicator(ele, true);
            let form: HTMLFormElement | null = null;
            if ("form" in ele && ele.form instanceof HTMLFormElement) {
                form = ele.form; 
            }
            if (!form) {
                form = ele.closest("form");
            }
            const body = new FormData(form ?? undefined, evt instanceof SubmitEvent ? evt.submitter : null);
            if (!form && "name" in ele && "value" in ele && typeof ele.name === "string" && typeof ele.value === "string" && ele.name.trim() !== "") {
                body.append(ele.name, ele.value);
            }
            const headers = new Headers();
            headers.set(RxRequestHeader, "");
            const ac = new AbortController();
            const request: RequestDetail = {
                action: ele.dataset.rxAction!,  // Always exists - only elements with rxAction get here
                method: getMethod(ele),
                redirect: _fetchRedirect,
                body,
                headers: headers,
                signal: ac.signal,
            };
            if (options?.addCookieToRequestHeader) {
                if (Array.isArray(options.addCookieToRequestHeader)) {
                    options.addCookieToRequestHeader.forEach((cookie: string): void => {
                        addCookieToRequest(request, cookie);
                    });
                } else {
                    addCookieToRequest(request, options.addCookieToRequestHeader);
                }
            }
            if (evt instanceof CustomEvent && (evt.detail?.type === 'initialized' || evt.detail?.type === 'poll')) {
                if (request.method !== "GET") {
                    const triggerType = evt.detail?.type;
                    throw new Error(`Element ${ele.id} with ${triggerType} trigger must use GET method, but found ${request.method}`);
                }
                const finalParams = new URLSearchParams();
                const formParams = request.body instanceof FormData 
                    ? new URLSearchParams(request.body as unknown as Record<string, string>)
                    : new URLSearchParams(request.body);
                formParams.forEach((value, key) => {
                    finalParams.set(key, value);  // Use set() not append() to avoid duplicates
                });
                const currentUrlParams = new URLSearchParams(window.location.search);
                currentUrlParams.forEach((value, key) => {
                    finalParams.set(key, value);  // Overrides form data if same key exists
                });
                if (finalParams.size > 0) {
                    const url = new URL(request.action!, window.location.href);
                    finalParams.forEach((value, key) => url.searchParams.set(key, value));
                    request.action = url.pathname + url.search;
                }
                delete request.body;
            } else if (/GET|DELETE/.test(request.method!)) {
                const params = request.body instanceof FormData 
                    ? new URLSearchParams(request.body! as unknown as Record<string, string>)
                    : new URLSearchParams(request.body);
                if (params.size) {
                    const url = new URL(request.action!, window.location.href);
                    params.forEach((value, key) => url.searchParams.append(key, value));
                    request.action = url.pathname + url.search;
                }
                delete request.body;
            }
            const state = collectState(ele);
            if (Object.keys(state).length > 0) {
                const url = new URL(request.action!, window.location.href);
                const stateParams = new URLSearchParams(state);
                stateParams.forEach((value, key) => {
                    if (!url.searchParams.has(key)) {
                        url.searchParams.set(key, value);
                    }
                });
                request.action = url.pathname + url.search;
            }
            const disableElement = ele.dataset.rxDisableInFlight?.trim().toLowerCase();
            if (disableElement !== undefined && disableElement !== "" && disableElement !== "true" && disableElement !== "false") {
                console.warn(`The data-rx-disable-in-flight attribute on element ${ele.id} is invalid. It should be either a Boolean (no value) or ="true" or ="false"`);
            }
            let response: Response | null = null;
            try {
                if (disableElement !== undefined && disableElement !== "false") {
                    toggleDisable(ele, true);
                }
                const filesMap = new Map<string, File>();
                let hasFiles = false;
                if (request.body instanceof FormData) {
                    const filesToRemove: string[] = [];
                    request.body.forEach((value: FormDataEntryValue, key: string) => {
                        if (value instanceof File && value.size > 0 && value.name !== '') {
                            filesMap.set(key, value);
                            filesToRemove.push(key);
                            hasFiles = true;
                        }
                    });
                    filesToRemove.forEach(key => {
                        (request.body as FormData).delete(key);
                    });
                    // Don't encode to JSON yet if we have files - we'll need the FormData later
                    if (!hasFiles && (options?.encodeRequestFormDataAsJson === undefined || options.encodeRequestFormDataAsJson === true)) {
                        encodeBodyAsJson(request);
                    }
                }
                const config: RequestConfiguration = {
                    trigger: evt,
                    action: request.action,
                    method: request.method,
                    body: request.body,
                    headers: request.headers,
                    abort: ac.abort.bind(ac),
                }
                if (ele._rxCallbacks!.beforeFetch) {
                    ele._rxCallbacks!.beforeFetch(config);
                }
                if (_callbacks.beforeFetch) {
                    _callbacks.beforeFetch(ele, config);
                }
                _dispatchBeforeFetch(ele, config);
                if (ac.signal.aborted) {
                    return;
                }
                if (hasFiles) {
                    const originalFormData = request.body as FormData;
                    await processFileUploads(filesMap, ele, options, ac.signal);
                    if (form) {
                        try {
                            const newFormData = new FormData(form, evt instanceof SubmitEvent ? evt.submitter : null);
                            originalFormData.forEach((value: FormDataEntryValue, key: string): void => {
                                if (!newFormData.has(key)) {
                                    newFormData.append(key, value);
                                }
                            });
                            request.body = newFormData;
                            if (options?.encodeRequestFormDataAsJson === undefined || options.encodeRequestFormDataAsJson === true) {
                                encodeBodyAsJson(request);
                            }
                        } catch (formError) {
                            const errorMsg = 'Form structure was invalidated during file upload. File upload responses should not replace the form element or submit button. Update only the file input area.';
                            console.error(errorMsg, formError);
                            sendError(ele, new Error(errorMsg));
                            return;
                        }
                    } else {
                        request.body = originalFormData;
                        if (options?.encodeRequestFormDataAsJson === undefined || options.encodeRequestFormDataAsJson === true) {
                            encodeBodyAsJson(request);
                        }
                    }
                }
                response = await fetch(request.action, request);
                if (ac.signal.aborted) {
                    return;
                }
                if (ele._rxCallbacks!.afterFetch) {
                    ele._rxCallbacks!.afterFetch(request, response);
                }
                if (_callbacks.afterFetch) {
                    _callbacks.afterFetch(ele, request, response);
                }
                _dispatchAfterFetch(ele, request, response);
            } finally {
                _requestRefTracker.delete(ele.id);
                toggleLoadingIndicator(ele, false);
                if (disableElement !== undefined && disableElement !== "false") {
                    toggleDisable(ele, false);
                }
            }
            await responseProcessor(ele, response);
        } catch(error: unknown) {
            sendError(ele, error);
        } 
    }

    function collectState(ele: HTMLElement): Record<string, string> {
        const stateKeys = parseStateKeys(ele.dataset.rxIncludeState);
        if (stateKeys.length === 0) {
            return {};
        }
        const state: Record<string, string> = {};
        try {
            stateKeys.forEach((k): void => {
                let value: string | null = null;
                try {
                    value = sessionStorage.getItem(k);
                } catch (storageError) {
                    console.warn(`Failed to read sessionStorage key '${k}':`, storageError instanceof Error ? storageError.message : String(storageError));
                }
                if (!value) {
                    try {
                        value = localStorage.getItem(k);
                    } catch (storageError) {
                        console.warn(`Failed to read localStorage key '${k}':`, storageError instanceof Error ? storageError.message : String(storageError));
                    }
                }
                if (value) {
                    state[k] = value;
                }
            });
        } catch (globalError) {
            console.warn('Failed to access browser storage:', globalError instanceof Error ? globalError.message : String(globalError));
            return {};
        }
        return state;
    }

    function parseRxHeaders(response: Response): ParsedRxHeaders | null {
        const parsed: ParsedRxHeaders = {};
        const mergeHeader = response.headers.get("rx-merge");
        if (mergeHeader) {
            try {
                parsed.merge = JSON.parse(mergeHeader);
            } catch (parseError) {
                const errorMsg = `Failed to parse "rx-merge" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
                console.error(errorMsg, { header: mergeHeader });
                throw new Error(errorMsg);
            }
        }
        const setStateHeader = response.headers.get("rx-trigger-set-state");
        if (setStateHeader) {
            try {
                parsed.setState = JSON.parse(`[${setStateHeader}]`);
            } catch (parseError) {
                const errorMsg = `Failed to parse "rx-trigger-set-state" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
                console.error(errorMsg, { header: setStateHeader });
                throw new Error(errorMsg);
            }
        }
        const closeDialogHeader = response.headers.get("rx-trigger-close-dialog");
        if (closeDialogHeader) {
            try {
                parsed.closeDialog = JSON.parse(closeDialogHeader);
            } catch (parseError) {
                const errorMsg = `Failed to parse "rx-trigger-close-dialog" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
                console.error(errorMsg, { header: closeDialogHeader });
                throw new Error(errorMsg);
            }
        }
        const focusElementHeader = response.headers.get("rx-trigger-focus-element");
        if (focusElementHeader) {
            try {
                parsed.focusElement = JSON.parse(focusElementHeader);
            } catch (parseError) {
                const errorMsg = `Failed to parse "rx-trigger-focus-element" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
                console.error(errorMsg, { header: focusElementHeader });
                throw new Error(errorMsg);
            }
        }
        const toastHeader = response.headers.get("rx-trigger-toast");
        if (toastHeader) {
            try {
                parsed.toast = JSON.parse(toastHeader);
            } catch (parseError) {
                const errorMsg = `Failed to parse "rx-trigger-toast" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
                console.error(errorMsg, { header: toastHeader });
                throw new Error(errorMsg);
            }
        }
        parsed.morphIgnoreActive = response.headers.has("rx-morph-ignore-active");
        return parsed;
    }

    async function responseProcessor(ele: HTMLElement, response: Response | null): Promise<void> {
        if (!response) {
            throw new Error(`Element ${ele.id} has no response after request.`);
        }
        if (response.status >= 400) {
            document.rxMutationObserver?.disconnect();
            removeTriggers(document.body);
            cleanupAllToasts();
            document.title = "Error";
            const contentType = response.headers.get("content-type");
            if (contentType && (contentType.includes("application/json") || contentType.includes("application/problem+json"))) {
                try {
                    const jsonData = await response.json();
                    const formattedJson = JSON.stringify(jsonData, null, 2); 
                    const pre = document.createElement('pre');
                    const code = document.createElement('code');
                    code.textContent = formattedJson;
                    pre.appendChild(code);
                    document.body.innerHTML = '';
                    document.body.appendChild(pre);
                } catch (jsonError) {
                    console.error("Error parsing JSON response:", jsonError);
                    const errorPre = document.createElement('pre');
                    const errorCode = document.createElement('code');
                    errorCode.textContent = `Error parsing JSON response: ${jsonError instanceof Error ? jsonError.message : String(jsonError)}`;
                    errorPre.appendChild(errorCode);
                    document.body.innerHTML = '';
                    document.body.appendChild(errorPre);
                }				
            } else {
                document.body.innerText = await response.text();
            }
            return;
        }
        const parsedHeaders = parseRxHeaders(response);
        const stateResult = processSetStateTrigger(ele, parsedHeaders?.setState);
        // Update browser URL immediately after state persistence
        if (stateResult.shouldUpdateUrl && stateResult.stateKeys.length > 0) {
            updateBrowserUrl(stateResult.stateKeys);
        }
        if (response.status === 202) {
            //used to issue a follow-up GET request for rendering
            const location = response.headers.get("location");
            if (location && location.trim() !== "") {
                window.location.assign(location);
            }
            return; 
        }
        processCloseDialogTrigger(ele, parsedHeaders?.closeDialog);
        // Handle 204 No Content - no merge processing required
        if (response.status === 204) {
            // Skip merge processing but still handle callbacks and triggers
            if (ele._rxCallbacks!.afterDocumentUpdate) {
                ele._rxCallbacks!.afterDocumentUpdate();
            }
            if (_callbacks.afterDocumentUpdate) {
                _callbacks.afterDocumentUpdate(ele);
            }
            _dispatchAfterDocumentUpdate(ele);
            processFocusElementTrigger(ele, parsedHeaders?.focusElement);
            processToastTrigger(ele, parsedHeaders?.toast);
            return;
        }
        if (!parsedHeaders?.merge) {
            throw new Error(`Expected a "rx-merge" header object.`);
        }
        if (document.startViewTransition !== undefined) {
            await document.startViewTransition(() => mergeFragments(ele, response, parsedHeaders.merge!, parsedHeaders.morphIgnoreActive)).finished;
        } else {
            await mergeFragments(ele, response, parsedHeaders.merge, parsedHeaders.morphIgnoreActive);
        }
        if (ele._rxCallbacks!.afterDocumentUpdate) {
            ele._rxCallbacks!.afterDocumentUpdate();
        }
        if (_callbacks.afterDocumentUpdate) {
            _callbacks.afterDocumentUpdate(ele);
        }
        processFocusElementTrigger(ele, parsedHeaders?.focusElement);
        processToastTrigger(ele, parsedHeaders?.toast);
    }

    function updateBrowserUrl(stateKeys: string[]): void {
        try {
            const currentUrl = new URL(window.location.href);
            const newParams = new URLSearchParams(currentUrl.search);
            stateKeys.forEach((key): void => {
                let value: string | null = null;
                try {
                    value = sessionStorage.getItem(key);
                } catch (sessionError) {
                    console.warn(`Failed to read sessionStorage key '${key}':`, sessionError instanceof Error ? sessionError.message : String(sessionError));
                }
                if (!value) {
                    try {
                        value = localStorage.getItem(key);
                    } catch (localError) {
                        console.warn(`Failed to read localStorage key '${key}':`, localError instanceof Error ? localError.message : String(localError));
                    }
                }
                if (value) {
                    newParams.set(key, value);
                } else {
                    newParams.delete(key);
                }
            });
            const newUrl = newParams.size === 0 
                ? currentUrl.pathname 
                : `${currentUrl.pathname}?${newParams.toString()}${currentUrl.hash}`;
            window.history.replaceState({}, '', newUrl);
        } catch (error) {
            console.warn('Failed to update browser URL:', error instanceof Error ? error.message : String(error));
        }
    }

    function processSetStateTrigger(ele: HTMLElement, setStateTriggers?: RxSetStateTrigger[]): { shouldUpdateUrl: boolean, stateKeys: string[] } {
        const result = { shouldUpdateUrl: false, stateKeys: [] as string[] };
        if (!setStateTriggers) {
            return result;
        }
        for (let i = 0; i < setStateTriggers.length; i++) {
            const setStateTrigger = setStateTriggers[i]!;
            if (!setStateTrigger.key || !setStateTrigger.scope) {
                const errorMsg = `Invalid "rx-trigger-set-state" structure - missing required fields: key, scope`;
                console.error(errorMsg, { parsed: setStateTrigger });
                const error = new Error(errorMsg);
                if (ele._rxCallbacks?.onElementTriggerError) {
                    ele._rxCallbacks.onElementTriggerError(error);
                }
                if (_callbacks.onElementTriggerError) {
                    _callbacks.onElementTriggerError(ele, error);
                }
                _dispatchOnElementTriggerError(ele, error);
                continue;
            }
            if (setStateTrigger.scope === "Session") {
                try {
                    if (!setStateTrigger.value) {
                        sessionStorage.removeItem(setStateTrigger.key);
                    } else {
                        sessionStorage.setItem(setStateTrigger.key, setStateTrigger.value);
                    }
                    if (setStateTrigger.updateUrl) {
                        result.stateKeys.push(setStateTrigger.key);
                        result.shouldUpdateUrl = true;
                    }
                } catch (storageError) {
                    const errorMsg = `Failed to ${!setStateTrigger.value ? 'remove' : 'set'} sessionStorage key '${setStateTrigger.key}': ${storageError instanceof Error ? storageError.message : String(storageError)}`;
                    console.warn(errorMsg, { key: setStateTrigger.key, value: setStateTrigger.value, error: storageError });
                    const error = new Error(errorMsg);
                    if (ele._rxCallbacks?.onElementTriggerError) {
                        ele._rxCallbacks.onElementTriggerError(error);
                    }
                    if (_callbacks.onElementTriggerError) {
                        _callbacks.onElementTriggerError(ele, error);
                    }
                }
                continue;
            }
            if (setStateTrigger.scope === "Persistent") {
                try {
                    if (!setStateTrigger.value) {
                        localStorage.removeItem(setStateTrigger.key);
                    } else {
                        localStorage.setItem(setStateTrigger.key, setStateTrigger.value);
                    }
                    if (setStateTrigger.updateUrl) {
                        result.stateKeys.push(setStateTrigger.key);
                        result.shouldUpdateUrl = true;
                    }
                } catch (storageError) {
                    const errorMsg = `Failed to ${!setStateTrigger.value ? 'remove' : 'set'} localStorage key '${setStateTrigger.key}': ${storageError instanceof Error ? storageError.message : String(storageError)}`;
                    console.warn(errorMsg, { key: setStateTrigger.key, value: setStateTrigger.value, error: storageError });
                    const error = new Error(errorMsg);
                    if (ele._rxCallbacks?.onElementTriggerError) {
                        ele._rxCallbacks.onElementTriggerError(error);
                    }
                    if (_callbacks.onElementTriggerError) {
                        _callbacks.onElementTriggerError(ele, error);
                    }
                }
            }
        }
        return result;
    }

    function processCloseDialogTrigger(ele: HTMLElement, closeDialogTrigger?: RxCloseDialogTrigger): void {
        if (!closeDialogTrigger) {
            return;
        }
        if (!closeDialogTrigger.dialogId) {
            const errorMsg = `Invalid "rx-trigger-close-dialog" structure - missing required field: dialogId`;
            console.error(errorMsg, { parsed: closeDialogTrigger });
            const error = new Error(errorMsg);
            if (ele._rxCallbacks?.onElementTriggerError) {
                ele._rxCallbacks.onElementTriggerError(error);
            }
            if (_callbacks.onElementTriggerError) {
                _callbacks.onElementTriggerError(ele, error);
            }
            return;
        }
        const modal = getCachedElement(closeDialogTrigger.dialogId);
        if (modal instanceof HTMLDialogElement) {
            modal.close(closeDialogTrigger.onCloseData);
            if (closeDialogTrigger.resetFormId) {
                const form = getCachedElement(closeDialogTrigger.resetFormId);
                if (form instanceof HTMLFormElement) {
                    form.reset();
                }
            }
        }
    }

    function processFocusElementTrigger(ele: HTMLElement, focusElementTrigger?: RxFocusElementTrigger): void {
        if (!focusElementTrigger) {
            return;
        }
        if (!focusElementTrigger.elementId) {
            const errorMsg = `Invalid "rx-trigger-focus-element" structure - missing required field: elementId`;
            console.error(errorMsg, { parsed: focusElementTrigger });
            const error = new Error(errorMsg);
            if (ele._rxCallbacks?.onElementTriggerError) {
                ele._rxCallbacks.onElementTriggerError(error);
            }
            if (_callbacks.onElementTriggerError) {
                _callbacks.onElementTriggerError(ele, error);
            }
            return;
        }
        const focusElement = getCachedElement(focusElementTrigger.elementId);
        if (focusElement) {
            //queue macro-task to focus
            setTimeout(() => {
                focusElement.focus();
                if (!focusElementTrigger.positionCursorEnd) {
                    return;
                }
                if (focusElement instanceof HTMLInputElement || focusElement instanceof HTMLTextAreaElement) {
                    const textLength = focusElement.value.length;
                    focusElement.setSelectionRange(textLength, textLength);
                }   
            }, 0);
        }
    }

    function processToastTrigger(ele: HTMLElement, toastTrigger?: RxToastTrigger): void {
        if (!toastTrigger) {
            return;
        }
        if (!toastTrigger.message) {
            const errorMsg = `Invalid "rx-trigger-toast" structure - missing required field: message`;
            console.error(errorMsg, { parsed: toastTrigger });
            const error = new Error(errorMsg);
            if (ele._rxCallbacks?.onElementTriggerError) {
                ele._rxCallbacks.onElementTriggerError(error);
            }
            if (_callbacks.onElementTriggerError) {
                _callbacks.onElementTriggerError(ele, error);
            }
            return;
        }
        const MAX_CONCURRENT_TOASTS = 5;
        if (_activeToasts.size >= MAX_CONCURRENT_TOASTS) {
            // Remove oldest toast (first in Map iteration order)
            const oldestToastId = _activeToasts.keys().next().value;
            if (oldestToastId) {
                cleanupToast(oldestToastId);
            }
        }
        const toastId = `rx-toast-${Date.now()}-${Math.random().toString(36).substring(2, 11)}`;
        const zone = `${toastTrigger.verticalPosition.toLowerCase()}-${toastTrigger.horizontalPosition.toLowerCase()}`;
        if (!_toastZones.has(zone)) {
            _toastZones.set(zone, []);
        }
        const zoneToasts = _toastZones.get(zone)!;
        const stackIndex = zoneToasts.length;
        const toast = document.createElement('div');
        toast.id = toastId;
        toast.setAttribute('popover', 'manual'); // Use popover API for top-layer
        toast.textContent = toastTrigger.message; // Use textContent for security
        const typeClass = _toastClasses[toastTrigger.type.toLowerCase() as keyof typeof _toastClasses] || '';
        const zoneToClassKey: Record<string, keyof typeof _toastClasses> = {
            'top-left': 'topLeft',
            'top-middle': 'topMiddle',
            'top-right': 'topRight',
            'center-left': 'centerLeft',
            'center-middle': 'centerMiddle',
            'center-right': 'centerRight',
            'bottom-left': 'bottomLeft',
            'bottom-middle': 'bottomMiddle',
            'bottom-right': 'bottomRight'
        };
        const positionClass = zoneToClassKey[zone] ? _toastClasses[zoneToClassKey[zone]] : '';
        const classes = [
            _toastClasses.base,
            typeClass,
            positionClass
        ].filter(c => c); // Remove empty strings
        toast.className = classes.join(' ');
        if (stackIndex > 0) {
            const STACK_SPACING = 10; // pixels between stacked toasts
            const stackOffset = calculateStackOffset(zone, stackIndex, STACK_SPACING);
            toast.style.transform = stackOffset;
        }
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'polite');
        document.body.appendChild(toast);
        toast.showPopover(); // Show on top-layer
        zoneToasts.push(toastId);
        const toastState: ToastState = { 
            element: toast,
            zone: zone,
            stackIndex: stackIndex
        };
        if (toastTrigger.clickToDismiss) {
            const clickHandler = () => {
                cleanupToast(toastId);
            };
            toast.addEventListener('click', clickHandler);
            toastState.clickHandler = clickHandler;
        }
        if (toastTrigger.duration > 0) {
            toastState.timeoutId = setTimeout(() => {
                cleanupToast(toastId);
            }, toastTrigger.duration);
        }
        _activeToasts.set(toastId, toastState);
    }

    function calculateStackOffset(zone: string, stackIndex: number, spacing: number): string {
        const verticalPos = zone.split('-')[0];
        const horizontalPos = zone.split('-')[1];
        const baseOffset = 60; // Approximate height of a toast
        const totalOffset = stackIndex * (baseOffset + spacing);
        let transform = '';
        if (verticalPos === 'top') {
            // Stack downward for top positions
            transform = `translateY(${totalOffset}px)`;
        } else if (verticalPos === 'bottom') {
            // Stack upward for bottom positions
            transform = `translateY(-${totalOffset}px)`;
        } else { // center
            // Stack downward from center (could be made smarter)
            transform = `translateY(${totalOffset}px)`;
        }
        if (horizontalPos === 'middle') {
            if (verticalPos === 'top' || verticalPos === 'bottom') {
                transform = `translateX(-50%) ${transform}`;
            } else if (verticalPos === 'center') {
                transform = `translate(-50%, calc(-50% + ${totalOffset}px))`;
            }
        } else if (verticalPos === 'center') {
            if (horizontalPos === 'left' || horizontalPos === 'right') {
                transform = `translateY(calc(-50% + ${totalOffset}px))`;
            }
        }
        return transform;
    }
    
    function reflowZone(zone: string): void {
        const zoneToasts = _toastZones.get(zone);
        if (!zoneToasts || zoneToasts.length === 0) return;
        const STACK_SPACING = 10;
        // Reposition each toast in the zone
        zoneToasts.forEach((toastId, newIndex) => {
            const toastState = _activeToasts.get(toastId);
            if (toastState) {
                toastState.stackIndex = newIndex;
                if (newIndex === 0) {
                    toastState.element.style.transform = '';
                } else {
                    const stackOffset = calculateStackOffset(zone, newIndex, STACK_SPACING);
                    toastState.element.style.transform = stackOffset;
                }
            }
        });
    }
    
    function cleanupToast(toastId: string): void {
        const toastState = _activeToasts.get(toastId);
        if (!toastState) return;
        if (toastState.zone && _toastZones.has(toastState.zone)) {
            const zoneToasts = _toastZones.get(toastState.zone)!;
            const index = zoneToasts.indexOf(toastId);
            if (index > -1) {
                zoneToasts.splice(index, 1);
            }
            if (zoneToasts.length === 0) {
                _toastZones.delete(toastState.zone);
            } else {
                reflowZone(toastState.zone);
            }
        }
        if (toastState.timeoutId) {
            clearTimeout(toastState.timeoutId);
        }
        if (toastState.clickHandler) {
            toastState.element.removeEventListener('click', toastState.clickHandler);
        }
        if (toastState.element.matches(':popover-open')) {
            toastState.element.hidePopover();
        }
        toastState.element.remove();
        _activeToasts.delete(toastId);
    }

    function cleanupAllToasts(): void {
        _activeToasts.forEach((_, toastId) => cleanupToast(toastId));
    }
    
    function getMethod(ele: HTMLElement): HttpMethod {
        const m = ele.dataset.rxMethod?.trim().toUpperCase() ?? "";
        switch (m) {
            case "":
                // Default to POST for form elements, GET for everything else
                return (ele.tagName === "FORM" || ele.closest("form")) ? "POST" : "GET";
            case "GET":
                return "GET";
            case "POST": 
            case "PUT":
            case "PATCH":
            case "DELETE":
                return m;
            default: { 
                const err = `${m} is not a valid HTTP method.`;
                throw new Error(err); 
            }
        }
    }

    function sendError(ele: HTMLElement, err: unknown): void {
        if (ele._rxCallbacks!.onElementTriggerError) {
            ele._rxCallbacks!.onElementTriggerError(err);
        }
        if (_callbacks.onElementTriggerError) {
            _callbacks.onElementTriggerError(ele, err);
        }
        _dispatchOnElementTriggerError(ele, err);
        console.error(err);
    }

    function toggleDisable(ele: HTMLElement, disable: boolean = false): void {
        let targetElement: HTMLElement | null = null;
        const parentFieldset = ele.closest("fieldset");
        if (parentFieldset) {
            targetElement = parentFieldset;
        } else if (ele instanceof HTMLFormElement) {
            if (disable) {
                if (!_elementOriginalDisabledState.has(ele)) {
                    const originallyDisabled = new Set<Element>();
                    const formControls = ele.querySelectorAll('input, textarea, select, button');
                    formControls.forEach(control => {
                        if (control.hasAttribute('disabled')) {
                            originallyDisabled.add(control);
                        }
                    });
                    if (ele.id) {
                        const associatedControls = document.querySelectorAll(`[form="${ele.id}"]`);
                        associatedControls.forEach(control => {
                            if (control.hasAttribute('disabled')) {
                                originallyDisabled.add(control);
                            }
                        });
                    }
                    _elementOriginalDisabledState.set(ele, originallyDisabled);
                }
                const formControls = ele.querySelectorAll('input, textarea, select, button');
                formControls.forEach(control => {
                    control.setAttribute("disabled", "");
                });
                if (ele.id) {
                    const associatedControls = document.querySelectorAll(`[form="${ele.id}"]`);
                    associatedControls.forEach(control => {
                        control.setAttribute("disabled", "");
                    });
                }
            } else {
                const originallyDisabled = _elementOriginalDisabledState.get(ele);
                const formControls = ele.querySelectorAll('input, textarea, select, button');
                formControls.forEach(control => {
                    if (!originallyDisabled?.has(control)) {
                        control.removeAttribute("disabled");
                    }
                });
                if (ele.id) {
                    const associatedControls = document.querySelectorAll(`[form="${ele.id}"]`);
                    associatedControls.forEach(control => {
                        if (!originallyDisabled?.has(control)) {
                            control.removeAttribute("disabled");
                        }
                    });
                }
                _elementOriginalDisabledState.delete(ele);
            }
            return; // Exit early for form handling
        } else if (ele instanceof HTMLOptionElement) {
            const parentOptGroup = ele.closest("optgroup");
            if (parentOptGroup) {
                targetElement = parentOptGroup;
            } else {
                targetElement = ele;
            }
        } else if (ele instanceof HTMLInputElement
            || ele instanceof HTMLTextAreaElement
            || ele instanceof HTMLSelectElement
            || ele instanceof HTMLButtonElement) {
            targetElement = ele;
        }
        if (targetElement) {
            if (disable) {
                if (!_elementOriginalDisabledState.has(ele)) {
                    const originallyDisabled = new Set<Element>();
                    if (targetElement.hasAttribute('disabled')) {
                        originallyDisabled.add(targetElement);
                    }
                    _elementOriginalDisabledState.set(ele, originallyDisabled);
                }
                targetElement.setAttribute("disabled", "");
            } else {
                const originallyDisabled = _elementOriginalDisabledState.get(ele);
                if (!originallyDisabled?.has(targetElement)) {
                    targetElement.removeAttribute("disabled");
                }
                _elementOriginalDisabledState.delete(ele);
            }
        }
    }

    function addCookieToRequest(detail: RequestDetail, cookie: string): void {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${cookie}=`);
        if (parts.length !== 2) {
            return;
        }
        if (!detail.headers) {
            return;
        }
        detail.headers.set(`${cookie}`, parts.pop()!.split(";").shift() ?? "");
    }

    function encodeBodyAsJson(detail: RequestDetail): void {
        detail.headers?.set("Content-Type", "application/json");
        if (!(detail.body instanceof FormData)) {
            return;
        }
        const object: Record<string, string | string[]> = {};
        let hasProps = false;
        detail.body?.forEach((value: FormDataEntryValue, key: string): void => {
            if (value instanceof Blob) {
                //skip any input [type=file]
                return;
            }
            hasProps = true;
            if (Object.hasOwn(object, key)) {
                const existingValue = object[key];
                if (!Array.isArray(existingValue)) {
                    object[key] = [existingValue as string];
                }
                (object[key] as string[]).push(value);
            } else {
                object[key] = value;
            }
        })
        if (hasProps) {
            detail.body = JSON.stringify(object);
        }
    }
    
    function parseXHRHeaders(headersString: string): Headers {
        const headers = new Headers();
        const lines = headersString.trim().split(/[\r\n]+/);
        lines.forEach(line => {
            const parts = line.split(': ');
            if (parts.length === 2 && parts[0] && parts[1]) {
                headers.append(parts[0], parts[1]);
            }
        });
        return headers;
    }
    
    function formatBytes(bytes: number): string {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.min(Math.floor(Math.log(bytes) / Math.log(k)), sizes.length - 1);
        const value = bytes / Math.pow(k, i);
        const formatted = value % 1 === 0 ? value.toString() : value.toFixed(2).replace(/\.?0+$/, '');
        return formatted + ' ' + sizes[i];
    }
    
    async function processFileUploads(filesMap: Map<string, File>, _ele: HTMLElement, options?: Options, signal?: AbortSignal): Promise<void> {
        const uploads: Promise<void>[] = [];
        const activeXHRs: XMLHttpRequest[] = [];
        let signalAborted = false;
        function resetProgress(fileInput: HTMLInputElement): void {
            if (fileInput.dataset.rxFileUploadProgressId) {
                const progressElement = document.getElementById(fileInput.dataset.rxFileUploadProgressId);
                if (progressElement && progressElement instanceof HTMLProgressElement) {
                    progressElement.value = 0;
                }
            }
        }
        const abortHandler = (): void => {
            signalAborted = true;
            activeXHRs.forEach(xhr => xhr.abort());
        };
        signal?.addEventListener('abort', abortHandler);
        const inputFileGroups = new Map<HTMLInputElement, Array<File>>();
        filesMap.forEach((file: File, name: string): void => {
            const fileInput = document.querySelector(`input[type="file"][name="${name}"][data-rx-action]`) as HTMLInputElement;
            if (!fileInput) return;
            
            if (!inputFileGroups.has(fileInput)) {
                inputFileGroups.set(fileInput, []);
            }
            inputFileGroups.get(fileInput)!.push(file);
        });
        for (const [fileInput, files] of inputFileGroups) {
            if (fileInput.dataset.rxFileUploadMaxSize) {
                const maxSize = parseInt(fileInput.dataset.rxFileUploadMaxSize, 10);
                if (!isNaN(maxSize) && maxSize > 0) {
                    const totalSize = files.reduce((sum, file) => sum + file.size, 0);
                    if (totalSize > maxSize) {
                        const error = new Error(
                            files.length === 1 && files[0]
                                ? `File "${files[0].name}" exceeds maximum size of ${formatBytes(maxSize)}`
                                : `Total size of ${files.length} files (${formatBytes(totalSize)}) exceeds maximum of ${formatBytes(maxSize)}`
                        );
                        sendError(fileInput, error);
                        resetProgress(fileInput);
                        return; // Don't upload any files
                    }
                } else if (maxSize !== 0) {
                    console.warn(`Invalid max size "${fileInput.dataset.rxFileUploadMaxSize}" for file input`);
                }
            }
        }
        filesMap.forEach((file: File, name: string): void => {
            const fileInput = document.querySelector(`input[type="file"][name="${name}"][data-rx-action]`) as HTMLInputElement;
            if (!fileInput) return;
            const uploadData = new FormData();
            uploadData.append(name, file);
            uploads.push(
                new Promise<void>((resolve, reject) => {
                    const xhr = new XMLHttpRequest();
                    activeXHRs.push(xhr);
                    let aborted = false;
                    if (fileInput.dataset.rxFileUploadTimeout) {
                        const timeout = parseInt(fileInput.dataset.rxFileUploadTimeout, 10);
                        if (!isNaN(timeout) && timeout > 0) {
                            xhr.timeout = timeout;
                        } else if (timeout === 0) {
                            xhr.timeout = 0; // Explicit 0 means no timeout
                        } else {
                            console.warn(`Invalid timeout "${fileInput.dataset.rxFileUploadTimeout}" for file input, using no timeout`);
                            xhr.timeout = 0;
                        }
                    } else {
                        xhr.timeout = 0; // Default: no timeout
                    }
                    xhr.ontimeout = () => {
                        resetProgress(fileInput);
                        const index = activeXHRs.indexOf(xhr);
                        if (index > -1) activeXHRs.splice(index, 1);
                        if (!aborted) {
                            const error = new Error(`Upload timeout for "${file.name}" after ${xhr.timeout}ms`);
                            sendError(fileInput, error);
                            reject(error);
                        }
                    };
                    xhr.upload.onprogress = (e: ProgressEvent) => {
                        if (aborted) return;
                        if (e.lengthComputable) {
                            const percentComplete = Math.round((e.loaded / e.total) * 100);
                            if (fileInput.dataset.rxFileUploadProgressId) {
                                const progressElement = document.getElementById(fileInput.dataset.rxFileUploadProgressId) as HTMLProgressElement;
                                if (progressElement) {
                                    progressElement.value = percentComplete;
                                }
                            }
                            const progressContext: FileUploadProgressContext = {
                                file: file,
                                loaded: e.loaded,
                                total: e.total,
                                percentage: percentComplete
                            };
                            if (fileInput._rxCallbacks?.onFileUploadProgress) {
                                fileInput._rxCallbacks.onFileUploadProgress(progressContext);
                            }
                            if (_callbacks.onFileUploadProgress) {
                                _callbacks.onFileUploadProgress(fileInput, progressContext);
                            }
                            _dispatchOnFileUploadProgress(fileInput, progressContext);
                        }
                    };
                    xhr.onload = () => {
                        if (aborted) return;
                        const index = activeXHRs.indexOf(xhr);
                        if (index > -1) activeXHRs.splice(index, 1);
                        resetProgress(fileInput); // Use helper function
                        const body = (xhr.status === 204 || xhr.status === 205) 
                            ? null 
                            : xhr.responseText;
                        const response = new Response(body, {
                            status: xhr.status,
                            statusText: xhr.statusText,
                            headers: parseXHRHeaders(xhr.getAllResponseHeaders())
                        });
                        responseProcessor(fileInput, response).then(resolve).catch(reject);
                    };
                    xhr.onerror = () => {
                        resetProgress(fileInput);
                        const index = activeXHRs.indexOf(xhr);
                        if (index > -1) activeXHRs.splice(index, 1);
                        if (!aborted) {
                            const error = new Error(`Failed to upload "${file.name}": Network error`);
                            sendError(fileInput, error);
                            reject(error);
                        }
                    };
                    xhr.onabort = () => {
                        resetProgress(fileInput);
                        const index = activeXHRs.indexOf(xhr);
                        if (index > -1) activeXHRs.splice(index, 1);
                        if (!aborted && !signalAborted) {
                            aborted = true;
                            const error = new Error(`Upload aborted for "${file.name}"`);
                            sendError(fileInput, error);
                            reject(error);
                        }
                    };
                    xhr.open('POST', fileInput.dataset.rxAction!);
                    xhr.setRequestHeader('rx-request', '');
                    if (options?.addCookieToRequestHeader) {
                        const cookies = Array.isArray(options.addCookieToRequestHeader) 
                            ? options.addCookieToRequestHeader 
                            : [options.addCookieToRequestHeader];
                        cookies.forEach((cookieName: string) => {
                            const value = `; ${document.cookie}`;
                            const parts = value.split(`; ${cookieName}=`);
                            if (parts.length === 2) {
                                const cookieValue = parts.pop()!.split(";").shift();
                                if (cookieValue) {
                                    xhr.setRequestHeader(cookieName, decodeURIComponent(cookieValue));
                                }
                            }
                        });
                    }
                    xhr.responseType = 'text';
                    xhr.send(uploadData);
                })
            );
        });
        await Promise.all(uploads);
        signal?.removeEventListener('abort', abortHandler);
    }

    // response rendering

    function removeElements(triggerElement: HTMLElement, removals: MergeStrategy[]): void {
        removals.forEach((r: MergeStrategy): void => {
            const target = getCachedElement(r.target);
            if (!target) {
                return;
            }
            if (triggerElement._rxCallbacks!.beforeDocumentUpdate && triggerElement._rxCallbacks!.beforeDocumentUpdate(target, r.strategy) === false) {
                return;
            }
            if (_callbacks.beforeDocumentUpdate && _callbacks.beforeDocumentUpdate(triggerElement, target, r.strategy) === false) {
                return;
            }
            const beforeEvent = _dispatchBeforeDocumentUpdate(triggerElement, target, r.strategy);
            if (beforeEvent.defaultPrevented) {
                return;
            }
            target.remove();
        });
    }

    async function mergeFragments(triggerElement: HTMLElement, response: Response, mergeStrategyArray: MergeStrategy[], morphIgnoreActive?: boolean): Promise<void> {
        const removals = mergeStrategyArray.filter((s: MergeStrategy): boolean => s.strategy === "remove");
        removeElements(triggerElement, removals);
        const parser = new DOMParser();
        const responseText = await response.text();
        const doc = parser.parseFromString(`<body><template>${responseText}</template></body>`, "text/html");
        const template = doc.body.querySelector("template")?.content;
        const fragments = Array.from(template?.childNodes ?? []);
        const swaps = mergeStrategyArray.filter((s: MergeStrategy): boolean => {
            if (s.strategy === "swap" 
                || s.strategy === "swapInner"
                || s.strategy === "afterbegin"
                || s.strategy === "afterend"
                || s.strategy === "beforebegin"
                || s.strategy === "beforeend") {
                return true;
            }
            return false;
        });
        swaps.forEach((s: MergeStrategy): void => {
            const fragment = getFragment(fragments, s);
            if (!fragment) {
                return;
            }
            const target = getTarget(triggerElement, fragment, s);
            if (!target) {
                return;
            }
            if (s.strategy === "swap") {
                target.replaceWith(fragment.content);
            } else if (s.strategy === "swapInner") {
                target.replaceChildren(fragment.content);
            } else {
                const newContent = Array.from(fragment.content.children);
                if (newContent.length === 0) {
                    return;
                }
                //insert the first element based on the strategy
                const firstInserted = target.insertAdjacentElement(s.strategy as InsertPosition, newContent[0]!);
                if (!firstInserted) {
                    const errorMsg = `Failed to insert element using strategy "${s.strategy}" for target "${target.id}"`;
                    console.error(errorMsg);
                    const error = new Error(errorMsg);
                    if (triggerElement._rxCallbacks?.onElementTriggerError) {
                        triggerElement._rxCallbacks.onElementTriggerError(error);
                    }
                    if (_callbacks.onElementTriggerError) {
                        _callbacks.onElementTriggerError(triggerElement, error);
                    }
                    return; // Skip remaining elements if first insertion fails
                }
                let thisEle = firstInserted;
                //insert the remainder afterend of the previous element
                for (let i = 1; i < newContent.length; i++) {
                    const inserted = thisEle.insertAdjacentElement("afterend", newContent[i]!);
                    if (!inserted) {
                        const errorMsg = `Failed to insert element ${i} after element "${thisEle.id || thisEle.tagName}"`;
                        console.error(errorMsg);        
                        const error = new Error(errorMsg);
                        if (triggerElement._rxCallbacks?.onElementTriggerError) {
                            triggerElement._rxCallbacks.onElementTriggerError(error);
                        }
                        if (_callbacks.onElementTriggerError) {
                            _callbacks.onElementTriggerError(triggerElement, error);
                        }
                        _dispatchOnElementTriggerError(triggerElement, error);
                        continue; // Skip this element but try the next ones
                    }
                    thisEle = inserted;
                }
            }
        });
        const morphs = mergeStrategyArray.filter((s: MergeStrategy): boolean => s.strategy === "morph");
        morphs.forEach((s: MergeStrategy): void => {
            const fragment = getFragment(fragments, s);
            if (!fragment) {
                return;
            }
            const target = getTarget(triggerElement, fragment, s);
            if (!target) {
                return;
            }
            Idiomorph.morph(target, Array.from(fragment.content.children), { 
                morphStyle: "outerHTML", 
                ignoreActiveValue: morphIgnoreActive,
                callbacks: {
                    beforeNodeMorphed: (oldNode: Element, newNode: Element): void => {
                        if (!(oldNode instanceof HTMLElement) || !(newNode instanceof HTMLElement)) {
                            return;
                        }
                        const oldTrigger = oldNode.dataset.rxTrigger;
                        const newTrigger = newNode.dataset.rxTrigger;
                        if (oldTrigger && oldTrigger !== newTrigger) {
                            removeTriggers(oldNode);
                            _elementTriggerState.delete(oldNode);
                        }
                    },
                    afterNodeMorphed: (oldNode: Element, _newNode: Element): void => { // eslint-disable-line @typescript-eslint/no-unused-vars
                        if (!(oldNode instanceof HTMLElement)) {
                            return;
                        }
                        if (oldNode.dataset.rxAction) {
                            if (!oldNode.id || oldNode.id.trim() === "") {
                                throw new Error(`Element with "data-rx-action" must have a unique ID after morphing.`);
                            }
                            setTriggers(oldNode);
                        }
                        if (_callbacks.onElementMorphed) {
                            _callbacks.onElementMorphed(oldNode);
                        }
                        _dispatchOnElementMorphed(oldNode);
                    }
                }
            });
        });
    }

    function getFragment(fragments: ChildNode[], mergeStrategy: MergeStrategy): HTMLTemplateElement | undefined {
        const fragmentId = `${mergeStrategy.target}-rx-fragment`;
        const fragment = fragments.find((f: ChildNode): f is HTMLTemplateElement => f instanceof HTMLTemplateElement && f.id === fragmentId) as HTMLTemplateElement | undefined;
        if (!fragment) {
            throw new Error(`Expected a response body fragment with id="${fragmentId}"`);
        }
        if (!fragment.hasChildNodes) {
            throw new Error(`Expected one or more child elements in fragment with id="${fragmentId}"`);
        }
        return fragment;
    }

    function getTarget(triggerElement: HTMLElement, fragment: HTMLTemplateElement, mergeStrategy: MergeStrategy): HTMLElement | undefined {
        const target = getCachedElement(mergeStrategy.target);
        if (!target) {
            throw new Error(`Expected an HTML element with id="${mergeStrategy.target}"`);
        }
        if (triggerElement._rxCallbacks!.beforeDocumentUpdate && triggerElement._rxCallbacks!.beforeDocumentUpdate(fragment, mergeStrategy.strategy) === false) {
            return;
        }
        if (_callbacks.beforeDocumentUpdate && _callbacks.beforeDocumentUpdate(triggerElement, fragment, mergeStrategy.strategy) === false) {
            return;
        }
        const beforeEvent = _dispatchBeforeDocumentUpdate(triggerElement, fragment, mergeStrategy.strategy);
        if (beforeEvent.defaultPrevented) {
            return;
        }
        return target;
    }

    function normalizeScriptTags(fragment: HTMLElement): void {
        if (!_isFirefox) {
            return;
        }
        if (fragment instanceof HTMLScriptElement) {
            processScript(fragment);
            return;
        }
        Array.from(fragment.querySelectorAll("script")).forEach((script: HTMLScriptElement): void => {
            processScript(script);
        });
    }

    function processScript(script: HTMLScriptElement): void {
        if (script.hasAttribute(_processedScriptTag)) {
            script.removeAttribute(_processedScriptTag);
            return;
        }
        const newScript = document.createElement("script");
        Array.from(script.attributes).forEach((attr: Attr): void => {
            newScript.setAttribute(attr.name, attr.value);
        });
        newScript.setAttribute(_processedScriptTag, "");
        newScript.textContent = script.textContent;
        newScript.async = false;
        const parent = script.parentNode;
        parent?.insertBefore(newScript, script);
        script.remove();
    }   
}

const razorxProto: unknown = {
    init: Object.freeze(_init),
    addCallbacks: Object.freeze(_addCallbacks)
}

/**
 * The main RazorX framework object.
 * @remarks
 * This is the primary entry point for the RazorX client-side framework.
 * Import this object to initialize and configure RazorX in your application.
 * 
 * @example
 * ```typescript
 * import { razorx } from './razorx';
 * 
 * // Initialize with default options
 * razorx.init();
 * 
 * // Or with custom options
 * razorx.init({
 *   encodeRequestFormDataAsJson: true,
 *   addCookieToRequestHeader: 'RequestVerificationToken'
 * });
 * 
 * // Add global callbacks
 * razorx.addCallbacks({
 *   beforeFetch: (element, config) => {
 *     console.log('Request starting:', config.action);
 *   },
 *   afterFetch: (element, detail, response) => {
 *     console.log('Request completed:', response.status);
 *   }
 * });
 * ```
 */
export const razorx = razorxProto as RazorX;