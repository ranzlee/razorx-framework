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
            rxHoistTo?: string //data-rx-hoist-to transfer rx behaviors to another element
            rxIncludeState?: string //data-rx-include-state
            rxLoadingIndicator?: string //data-rx-loading-indicator
        },
        addRxCallbacks?: (callbacks: ElementCallbacks) => void,
        _rxCallbacks?: ElementCallbacks,
    }
}

export type RazorX = {
    init: (options?: Options) => void,
    addCallbacks: (callbacks: DocumentCallbacks) => void,
}

export type Options = {
    addCookieToRequestHeader?: string | string[],
    encodeRequestFormDataAsJson?: boolean, //true
    loadingIndicatorClasses?: {
        hidden?: string,  // Default: 'rx-loading-hidden'
        visible?: string  // Default: 'rx-loading-visible'
    }
}

export type DocumentCallbacks = {
    beforeDocumentProcessed?: () => void,
    afterDocumentProcessed?: () => void,
    beforeInitializeElement?: (element: HTMLElement) => boolean, //return false to cancel
    afterInitializeElement?: (element: HTMLElement) => void, //return false to cancel
    beforeFetch?: (triggerElement: HTMLElement, requestConfiguration: RequestConfiguration) => void, 
    afterFetch?: (triggerElement: HTMLElement, requestDetail: RequestDetail, response: Response) => void,
    beforeDocumentUpdate?: (triggerElement: HTMLElement, mergeElement: HTMLElement, strategy: MergeStrategyType) => boolean,
    afterDocumentUpdate?: (triggerElement: HTMLElement) => void
    onElementAdded?: (addedElement: HTMLElement) => void,
    onElementMorphed?: (morphedElement: HTMLElement) => void,
    onElementRemoved?: (removedElement: HTMLElement) => void,
    onElementTriggerError?: (triggerElement: HTMLElement, error: unknown) => void,
}

export type ElementCallbacks = {
    beforeFetch?: (requestConfiguration: RequestConfiguration) => void, 
    afterFetch?: (requestDetail: RequestDetail, response: Response) => void,
    beforeDocumentUpdate?: (mergeElement: HTMLElement, strategy: MergeStrategyType) => boolean,
    afterDocumentUpdate?: () => void,
    onElementTriggerError?: (error: unknown) => void,
}

export type RequestConfiguration = {
    trigger: Event,
    action: string,
    method: HttpMethod,
    headers: Headers,
    body: FormData | string | undefined,
    abort: (reason?: string) => void
}

export type RequestDetail = {
    action: string,
    method: HttpMethod,
    redirect: FetchRedirect,
    body: FormData | string | undefined,
    headers: Headers,
    signal: AbortSignal,
}

export type MergeStrategy = {
    target: string,
    strategy: MergeStrategyType
}

export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export type FetchRedirect = "follow" | "error" | "manual";

export type MergeStrategyType = InsertPosition | "swap" | "swapInner" | "morph" | "remove";

export type RxResponseHeaders = 
    "rx-merge" | 
    "rx-morph-ignore-active" | 
    "rx-trigger-close-dialog" | 
    "rx-trigger-focus-element" | 
    "rx-trigger-set-state";

export type SpecialTriggerType = 'initialized' | 'poll' | 'revealed';

export type SpecialTriggerConfig = {
    type: SpecialTriggerType;
}

export type InitializedTrigger = SpecialTriggerConfig & {
    type: 'initialized';
    delay?: number; // Optional delay in milliseconds before triggering
}

export type PollTrigger = SpecialTriggerConfig & {
    type: 'poll';
    interval?: number; // Optional, default 1000ms
}

export type RevealedTrigger = SpecialTriggerConfig & {
    type: 'revealed';
    margin?: string; // Optional, default "0px"
}

export type SpecialTrigger = InitializedTrigger | PollTrigger | RevealedTrigger;

export type TriggerDefinition = string | SpecialTrigger;

export type RxCloseDialogTrigger = {
    dialogId: string,
    onCloseData?: string,
    resetFormId?: string
}

export type RxFocusElementTrigger = {
    elementId: string,
    positionCursorEnd: boolean,
}

export type RxSetStateTrigger = {
    scope: "Session" | "Persistent"
    key: string,
    value?: string | null,
    updateUrl?: boolean,
}

type ElementTriggerState = {
    triggers: Set<string>;
    intervalId?: ReturnType<typeof setInterval>;
    observer?: IntersectionObserver;
}

type HoistedConfig = {
    action: string;
    method: string;
    sourceId: string;
    timestamp: number;
}

type ParsedRxHeaders = {
    merge?: MergeStrategy[];
    setState?: RxSetStateTrigger[];
    closeDialog?: RxCloseDialogTrigger;
    focusElement?: RxFocusElementTrigger;
    morphIgnoreActive?: boolean;
};

const RxRequestHeader = "rx-request";

const _processedScriptTag = "data-rx-script-processed";

const _requestRefTracker: Set<string> = new Set();

const _debouncedRequests: Map<string, (() => Promise<void>) & { _cleanup?: () => void }> = new Map();

const _elementCache: Map<string, HTMLElement> = new Map();

const _elementTriggerState: WeakMap<HTMLElement, ElementTriggerState> = new WeakMap();

const _hoistedConfigs: WeakMap<HTMLElement, HoistedConfig> = new WeakMap();

const _activeLoadingIndicators: Map<string, Set<string>> = new Map();

const _fetchRedirect: FetchRedirect = "follow";

const _callbacks: DocumentCallbacks = {};

let _loadingClasses = {
    hidden: 'rx-loading-hidden',
    visible: 'rx-loading-visible'
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
}

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

    // Add cleanup for page unload scenarios
    window.addEventListener('beforeunload', () => {
        if (document.rxMutationObserver) {
            document.rxMutationObserver.disconnect();
            _debouncedRequests.forEach(req => req._cleanup?.());
            _debouncedRequests.clear();
            clearElementCache();
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
                }
                node.querySelectorAll('[id]').forEach((child: Element) => {
                    if (child.id) {
                        invalidateCachedElement(child.id);
                    }
                });
                if (_callbacks.onElementRemoved) {
                    _callbacks.onElementRemoved(node);
                }
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
            throw new Error(`Space-separated triggers are not supported. Convert "${trimmed}" to JSON array format`);
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
            throw new Error(`Space-separated state keys are no longer supported. Convert "${trimmed}" to JSON array format: ${jsonArray}`);
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
        if (!_activeLoadingIndicators.has(indicatorId)) {
            _activeLoadingIndicators.set(indicatorId, new Set());
        }
        const activeElements = _activeLoadingIndicators.get(indicatorId)!;
        if (show) {
            activeElements.add(ele.id);
            const indicator = getCachedElement(indicatorId);
            if (indicator) {
                indicator.classList.remove(_loadingClasses.hidden);
                indicator.classList.add(_loadingClasses.visible);
            } else if (activeElements.size === 1) { // Only warn once
                console.warn(`Loading indicator element '${indicatorId}' not found`);
            }
        } else {
            activeElements.delete(ele.id);
            // Only hide if no other elements are using it
            if (activeElements.size === 0) {
                const indicator = getCachedElement(indicatorId);
                if (indicator) {
                    indicator.classList.remove(_loadingClasses.visible);
                    indicator.classList.add(_loadingClasses.hidden);
                }
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
        
        if (ele.dataset.rxHoistTo) {
            const hasSpecialTrigger = triggers.some((trigger) => 
                typeof trigger === 'object' && 'type' in trigger
            );
            if (hasSpecialTrigger) {
                throw new Error(
                    `Element ${ele.id} cannot use special triggers ` +
                    `with data-rx-hoist-to. Special triggers have their own lifecycle and cannot be hoisted to another element.`
                );
            }
            const triggerState = _elementTriggerState.get(ele) || { triggers: new Set() };
            triggers.forEach((trigger): void => {
                if (typeof trigger === 'string') {
                    // Check if already added to prevent duplicates
                    if (!triggerState.triggers.has(trigger)) {
                        ele.addEventListener(trigger, elementHoistEventHandler);
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
        const pollInterval = interval || 1000;
        if (pollInterval <= 0) {
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
            configureElement(ele);
            setTriggers(ele);
            if (_callbacks.afterInitializeElement) {
                _callbacks.afterInitializeElement(ele);
            }
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
                ele.removeEventListener(trigger, elementHoistEventHandler);
            });
        }
        const debouncedRequest = _debouncedRequests.get(ele.id);
        if (debouncedRequest) {
            debouncedRequest._cleanup?.();
            _debouncedRequests.delete(ele.id);
        }
        _hoistedConfigs.delete(ele);
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
        }
        Object.defineProperty(ele, "addRxCallbacks", {
            value: addCallbacks,
            writable: false,
        });
        Object.defineProperty(ele, "_rxCallbacks", {
            value: elementCallbacks,
            writable: false,
        });
    }

    // event handlers

    function DOMContentLoaded(): void {
        //observe the whole document for changes
        document.rxMutationObserver.observe(document.documentElement, { childList: true, subtree: true });
        if (_callbacks.beforeDocumentProcessed) {
            _callbacks.beforeDocumentProcessed();
        }
        //process the entire document recursively
        addTriggers(document.body);
        if (_callbacks.afterDocumentProcessed) {
            _callbacks.afterDocumentProcessed();
        }
    }

    function elementHoistEventHandler(this: HTMLElement): void {
        const hoistTargetId = this.dataset.rxHoistTo ?? "";
        const hoistTarget = getCachedElement(hoistTargetId);
        if (!hoistTarget) {
            const err = `Element ${this.id} with "data-rx-hoist-to" ${this.dataset.rxHoistTo} does not reference a valid DOM element.`;
            throw new Error(err);
        }
        _hoistedConfigs.set(hoistTarget, {
            action: this.dataset.rxAction!,  // Always exists - only elements with rxAction get here
            method: this.dataset.rxMethod ?? ((this.tagName === "FORM" || this.closest("form")) ? "POST" : "GET"),
            sourceId: this.id,
            timestamp: Date.now()
        });
        if (!hoistTarget.dataset.rxTrigger) {
            hoistTarget.addEventListener('click', hoistedTargetClickHandler, { once: true });
            hoistTarget.setAttribute('data-rx-trigger', 'hoist-one-shot');
        }
    }
    
    async function hoistedTargetClickHandler(this: HTMLElement, evt: Event): Promise<void> {
        evt.preventDefault();
        const config = _hoistedConfigs.get(this);
        if (!config) {
            console.warn(`No hoisted configuration found for element ${this.id}. The hoisting may have expired.`);
            this.removeAttribute('data-rx-trigger'); // Clean up the temporary marker
            return;
        }
        _hoistedConfigs.delete(this);
        this.removeAttribute('data-rx-trigger'); // Clean up the temporary marker
        const syntheticElement = document.createElement('div');
        syntheticElement.id = `hoist-synthetic-${config.sourceId}-${Date.now()}`;
        syntheticElement.dataset.rxAction = config.action;
        syntheticElement.dataset.rxMethod = config.method;
        if (this.dataset.rxIncludeState) {
            syntheticElement.dataset.rxIncludeState = this.dataset.rxIncludeState;
        }
        configureElement(syntheticElement);
        try {
            await elementTriggerProcessor(syntheticElement, evt);
        } catch (error) {
            console.error(`Failed to process hoisted request from ${config.sourceId}:`, error);
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
        const debounceValue = this.dataset.rxDebounce?.trim().toLowerCase();
        if (debounceValue === undefined) {
            queue(this, evt);
            return;
        }
        const delay = parseInt(debounceValue, 10);
        if (Number.isNaN(delay) || delay <= 0) {
            console.warn(`The data-rx-debounce attribute on element ${this.id} is invalid. It must be a number >= zero`);
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
                console.error('Request queue error:', error);
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
            // Atomic check-and-add to prevent race conditions
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
            } else {
                if (options?.encodeRequestFormDataAsJson === undefined || options.encodeRequestFormDataAsJson === true) {
                    encodeBodyAsJson(request);
                }
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
            const config: RequestConfiguration = {
                trigger: evt,
                action: request.action,
                method: request.method,
                body: request.body,
                headers: request.headers,
                abort: ac.abort.bind(ac),
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
                if (ele._rxCallbacks!.beforeFetch) {
                    ele._rxCallbacks!.beforeFetch(config);
                }
                if (_callbacks.beforeFetch) {
                    _callbacks.beforeFetch(ele, config);
                }
                if (ac.signal.aborted) {
                    return;
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
        const sessionValues = new Map<string, string | null>();
        const localValues = new Map<string, string | null>();
        try {
            stateKeys.forEach((k): void => {
                try {
                    sessionValues.set(k, sessionStorage.getItem(k));
                } catch (storageError) {
                    console.warn(`Failed to read sessionStorage key '${k}':`, storageError instanceof Error ? storageError.message : String(storageError));
                    sessionValues.set(k, null);
                }
            });
            stateKeys.forEach((k): void => {
                try {
                    localValues.set(k, localStorage.getItem(k));
                } catch (storageError) {
                    console.warn(`Failed to read localStorage key '${k}':`, storageError instanceof Error ? storageError.message : String(storageError));
                    localValues.set(k, null);
                }
            });
        } catch (globalError) {
            console.warn('Failed to access browser storage:', globalError instanceof Error ? globalError.message : String(globalError));
            return {};
        }
        const state: Record<string, string> = {};
        stateKeys.forEach((k): void => {
            let v = sessionValues.get(k);
            if (!v) {
                v = localValues.get(k);
            }
            if (v) {
                state[k] = v;
            }
        });
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
        parsed.morphIgnoreActive = response.headers.has("rx-morph-ignore-active");
        return parsed;
    }

    async function responseProcessor(ele: HTMLElement, response: Response | null): Promise<void> {
        if (!response) {
            throw new Error(`Element ${ele.id} has no response after request.`);
        }
        if (response.status >= 400) {
            //dev error response
            document.rxMutationObserver?.disconnect();
            removeTriggers(document.body);
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
            processFocusElementTrigger(ele, parsedHeaders?.focusElement);
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
        console.error(err);
    }

    function toggleDisable(ele: HTMLElement, disable: boolean = false): void {
        let targetElement: HTMLElement | null = null;
        const parentFieldset = ele.closest("fieldset");
        if (parentFieldset) {
            targetElement = parentFieldset;
        } else if (ele instanceof HTMLFormElement) {
            const formControls = ele.querySelectorAll('input, textarea, select, button');
            formControls.forEach(control => {
                if (disable) {
                    control.setAttribute("disabled", "");
                } else {
                    control.removeAttribute("disabled");
                }
            });
            if (ele.id) {
                const associatedControls = document.querySelectorAll(`[form="${ele.id}"]`);
                associatedControls.forEach(control => {
                    if (disable) {
                        control.setAttribute("disabled", "");
                    } else {
                        control.removeAttribute("disabled");
                    }
                });
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
                targetElement.setAttribute("disabled", "");
            } else {
                targetElement.removeAttribute("disabled");
                //targetElement.focus();
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
                //skip any input [type=file] for XMLHttpRequest processing
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

export const razorx = razorxProto as RazorX;