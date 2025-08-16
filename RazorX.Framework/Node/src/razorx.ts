import { Idiomorph } from "idiomorph";

declare global {
    interface Document {
        rxMutationObserver: MutationObserver
    }

    interface HTMLElement {
        dataset: {
            // all dataset props must be strings
            rxIgnore?: string, //data-rx-ignore
            rxAction?: string, //data-rx-action
            rxMethod?: string, //data-rx-method
            rxTrigger?: string, //data-rx-trigger
            rxAllowEventDefault?: string //data-rx-allow-default
            rxDisableInFlight?: string, //data-rx-disable-in-flight
            rxDebounce?: string //data-rx-debounce
            rxPollInterval?: string //data-rx-poll-interval
            rxDisableQueueing?: string // data-rx-disable-queueing
            rxHoistTo?: string //data-rx-hoist-to transfer rx behaviors to another element
            rxIncludeState?: string //data-rx-include-state
            rxRevealMargin?: string //data-rx-reveal-margin
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

export type RxExtendedEvents = "rx:initialized" | "rx:poll" | "rx:revealed";

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
}

export const RxRequestHeader = "rx-request";

type ElementTriggerState = {
    triggers: Set<string>;
    intervalId?: number;
    observer?: IntersectionObserver;
}

const _processedScriptTag = "data-rx-script-processed";

const _requestRefTracker: Set<string> = new Set();

const _debouncedRequests: Map<string, (() => Promise<void>) & { _cleanup?: () => void }> = new Map();

const _elementCache: Map<string, HTMLElement | null> = new Map();

const _elementTriggerState: WeakMap<HTMLElement, ElementTriggerState> = new WeakMap();

const _fetchRedirect: FetchRedirect = "follow";

const _callbacks: DocumentCallbacks = {};

const _isFirefox = navigator.userAgent.toLowerCase().includes("firefox");

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

const _init = (options?: Options, callbacks?: DocumentCallbacks): void => {

    // initialization

    if (document.rxMutationObserver) {
        // Document already processed - this is intentional
        console.debug("Document already has active MutationObserver");
        return;
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

    // Element cache helper functions
    function getCachedElement(id: string): HTMLElement | null {
        if (_elementCache.has(id)) {
            return _elementCache.get(id) || null;
        }
        const element = document.getElementById(id);
        _elementCache.set(id, element);
        return element;
    }

    function clearElementCache(): void {
        _elementCache.clear();
    }

    function invalidateCachedElement(id: string): void {
        _elementCache.delete(id);
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
        const triggers = ele.dataset.rxTrigger!.split(/\s+/);
        // Validate that special triggers are not combined with hoist
        if (ele.dataset.rxHoistTo) {
            const specialTriggers = ["rx:initialized", "rx:poll", "rx:revealed"];
            const hasSpecialTrigger = triggers.some((trigger) => 
                specialTriggers.includes(trigger.trim().toLowerCase())
            );
            
            if (hasSpecialTrigger) {
                throw new Error(
                    `Element ${ele.id} cannot use special triggers (rx:initialized, rx:poll, rx:revealed) ` +
                    `with data-rx-hoist-to. Special triggers have their own lifecycle and cannot be hoisted to another element.`
                );
            }
            const triggerState = _elementTriggerState.get(ele) || { triggers: new Set() };
            triggers.forEach((trigger): void => {
                // Check if already added to prevent duplicates
                if (!triggerState.triggers.has(trigger)) {
                    ele.addEventListener(trigger, elementHoistEventHandler);
                    triggerState.triggers.add(trigger);
                }
            });
            _elementTriggerState.set(ele, triggerState);
        } else {
            const rxInitialized: RxExtendedEvents = "rx:initialized";
            const rxPoll: RxExtendedEvents = "rx:poll";
            const rxRevealed: RxExtendedEvents = "rx:revealed";
            // Get or create trigger state for tracking
            const triggerState = _elementTriggerState.get(ele) || { triggers: new Set() };
            triggers.forEach((trigger): void => {
                const trimmedTrigger = trigger.trim().toLowerCase();
                if (trimmedTrigger === rxInitialized) {
                    initializedTrigger(ele, rxInitialized);
                } else if (trimmedTrigger === rxPoll) {
                    pollTrigger(ele, rxPoll);
                    triggerState.triggers.add(rxPoll);
                } else if (trimmedTrigger === rxRevealed) {
                    revealedTrigger(ele, rxRevealed);
                    triggerState.triggers.add(rxRevealed);
                } else {
                    if (triggerState.triggers.has(trigger)) {
                        return;
                    }
                    ele.addEventListener(trigger, elementTriggerEventHandler);
                    triggerState.triggers.add(trigger);
                }
            });
            _elementTriggerState.set(ele, triggerState);
        }
    }

    function initializedTrigger(ele: HTMLElement, rxInitialized: string): void {
        const evt = new CustomEvent(rxInitialized)
        elementTriggerProcessor(ele, evt);
    }

    function pollTrigger(ele: HTMLElement, rxPoll: string): void {
        const existingState = _elementTriggerState.get(ele);
        if (existingState?.intervalId) {
            console.warn(`Polling already active for element ${ele.id}`);
            return;
        }
        let interval = 1000;
        const intervalSetting = ele.dataset.rxPollInterval?.trim().toLowerCase();
        if (intervalSetting === undefined) {
            console.warn(`The data-rx-poll-interval attribute on element ${ele.id} was not found. Default value of 1000 ms used.`);
        } else {
            interval = parseInt(intervalSetting, 10);
            if (Number.isNaN(interval) || interval <= 0) {
                interval = 1000;
                console.warn(`The data-rx-poll-interval attribute on element ${ele.id} is invalid. Default value of 1000 ms used.`);
            }    
        }
        const evt = new CustomEvent(rxPoll)
        const intervalId = setInterval(() => {
            elementTriggerProcessor(ele, evt);
        }, interval);
        const state = _elementTriggerState.get(ele) || { triggers: new Set() };
        state.intervalId = intervalId;
        _elementTriggerState.set(ele, state);
    }

    function revealedTrigger(ele: HTMLElement, rxRevealed: string): void {
        const existingState = _elementTriggerState.get(ele);
        if (existingState?.observer) {
            console.warn(`Observer already active for element ${ele.id}`);
            return;
        }
        let rootMargin = "0px";
        const revealMarginSetting = ele.dataset.rxRevealMargin?.trim();
        if (revealMarginSetting !== undefined) {
            if (revealMarginSetting === "") {
                console.warn(`The data-rx-reveal-margin attribute on element ${ele.id} is empty. Default value of "0px" used.`);
            } else {
                // Basic validation for CSS margin format
                const marginPattern = /^-?\d+px(\s+-?\d+px)*$/;
                if (marginPattern.test(revealMarginSetting)) {
                    rootMargin = revealMarginSetting;
                } else {
                    console.warn(`The data-rx-reveal-margin attribute on element ${ele.id} has invalid format "${revealMarginSetting}". Must be CSS margin format (e.g., "200px", "100px 0px"). Default value of "0px" used.`);
                }
            }
        }
        const observer = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting && entry.target === ele) {
                        const evt = new CustomEvent(rxRevealed);
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
        const firstIgnore = ele.closest("[data-rx-ignore]");
        if (firstIgnore && firstIgnore instanceof HTMLElement) { 
            const ignore = firstIgnore.dataset.rxIgnore?.trim().toLowerCase();
            if (ignore !== "" && ignore !== "true" && ignore !== "false") {
                console.warn(`The data-rx-ignore attribute on element ${firstIgnore.id} is invalid. It should be either a Boolean (no value) or ="true" or ="false"`);
            }
            if (ignore !== "false") {
                return;
            }  
        }
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
        // Clean up debounced requests
        const debouncedRequest = _debouncedRequests.get(ele.id);
        if (debouncedRequest) {
            debouncedRequest._cleanup?.();
            _debouncedRequests.delete(ele.id);
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
        Array.from(this.attributes).forEach((attr: Attr): void => {
            if (attr.name === "data-rx-action" || attr.name === "data-rx-method") {
                hoistTarget.setAttribute(attr.name, attr.value);
            }
        });
        if (!hoistTarget.addRxCallbacks) {
            configureElement(hoistTarget);
            setTriggers(hoistTarget);
        } 
        if (_callbacks.afterInitializeElement) {
            _callbacks.afterInitializeElement(hoistTarget);
        }
    }

    function elementTriggerEventHandler(this: HTMLElement, evt: Event): void {
        const allowEventDefault = this.dataset.rxAllowEventDefault?.trim().toLowerCase();
        if (allowEventDefault !== undefined && allowEventDefault !== "" && allowEventDefault !== "true" && allowEventDefault !== "false") {
            console.warn(`The data-rx-allow-event-default attribute on element ${this.id} is invalid. It should be either a Boolean (no value) or ="true" or ="false"`);
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
        if (ele.dataset.rxDisableQueueing !== undefined && ele.dataset.rxDisableQueueing.toLowerCase() !== "false") {
            elementTriggerProcessor(ele, evt);
            return;
        }
        _requestQueue = _requestQueue.finally(async (): Promise<void> => {
            try {
                await elementTriggerProcessor(ele, evt);
            } catch (error: unknown) {
                console.error('Request queue error:', error);
            }
        });
    }

    function debounce(ele: HTMLElement, evt: Event, delay: number): (() => Promise<void>) & { _cleanup?: () => void } {
        let timeoutId: number | null = null;
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
                action: ele.dataset.rxAction ?? "", 
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
            if (/GET|DELETE/.test(request.method!)) {
                const params = request.body instanceof FormData 
                    ? new URLSearchParams(request.body! as unknown as Record<string, string>)
                    : new URLSearchParams(request.body);
                //const params = new URLSearchParams(request.body.toString());
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
                stateParams.forEach((value, key) => url.searchParams.append(key, value));
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
                if (disableElement !== undefined && disableElement.toLowerCase() !== "false") {
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
                if (disableElement !== undefined && disableElement.toLowerCase() !== "false") {
                    toggleDisable(ele, false);
                }
            }
            await responseProcessor(ele, response);
        } catch(error: unknown) {
            sendError(ele, error);
        } 
    }

    function collectState(ele: HTMLElement): Record<string, string> {
        if (!ele.dataset.rxIncludeState) {
            return {};
        }
        const stateKeys = ele.dataset.rxIncludeState!.split(/\s+/);
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
        processSetStateTrigger(ele, response);
        if (response.status === 202) {
            //used to issue a follow-up GET request for rendering
            const location = response.headers.get("location");
            if (location && location.trim() !== "") {
                window.location.assign(location);
            }
            return; 
        }
        processCloseDialogTrigger(ele, response);
        const mergeHeader: RxResponseHeaders = "rx-merge";
        const merge = response?.headers.get(mergeHeader);
        if (!merge) {
            throw new Error(`Expected a "${mergeHeader}" header object.`);
        }
        let mergeStrategyArray: MergeStrategy[];
        try {
            mergeStrategyArray = JSON.parse(merge);
        } catch (parseError) {
            const errorMsg = `Failed to parse "${mergeHeader}" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
            console.error(errorMsg, { header: merge });
            throw new Error(errorMsg);
        }
        if (response.status === 204) {
            const removals = mergeStrategyArray.filter((s: MergeStrategy): boolean => s.strategy === "remove");
            if (removals.length > 0) {
                if (document.startViewTransition !== undefined) {
                    await document.startViewTransition(async () => removeElements(ele, removals)).finished;
                } else {
                    removeElements(ele, removals);
                }
            }
        } else {
            if (document.startViewTransition !== undefined) {
                await document.startViewTransition(async () => await mergeFragments(ele, response, mergeStrategyArray)).finished;
            } else {
                await mergeFragments(ele, response, mergeStrategyArray);
            }
        }
        if (ele._rxCallbacks!.afterDocumentUpdate) {
            ele._rxCallbacks!.afterDocumentUpdate();
        }
        if (_callbacks.afterDocumentUpdate) {
            _callbacks.afterDocumentUpdate(ele);
        }
        processFocusElementTrigger(ele, response);
    }

    function processSetStateTrigger(ele: HTMLElement, response: Response): void {
        const setStateHeader: RxResponseHeaders = "rx-trigger-set-state";
        const setStateTriggerString = response.headers.get(setStateHeader);
        if (!setStateTriggerString) {
            return;
        }
        let setStateTriggers: RxSetStateTrigger[];
        try {
            setStateTriggers  = JSON.parse(`[${setStateTriggerString}]`);
        } catch(parseError) {
            const errorMsg = `Failed to parse "${setStateHeader}" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
            console.error(errorMsg, { header: setStateTriggerString });
            const error = new Error(errorMsg);
            if (ele._rxCallbacks?.onElementTriggerError) {
                ele._rxCallbacks.onElementTriggerError(error);
            }
            if (_callbacks.onElementTriggerError) {
                _callbacks.onElementTriggerError(ele, error);
            }
            return;
        }
        for (let i = 0; i < setStateTriggers.length; i++) {
            const setStateTrigger = setStateTriggers[i]!;
            if (!setStateTrigger.key || !setStateTrigger.scope) {
                const errorMsg = `Invalid "${setStateHeader}" structure - missing required fields: key, scope`;
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
    }

    function processCloseDialogTrigger(ele: HTMLElement, response: Response): void {
        const closeDialogHeader: RxResponseHeaders = "rx-trigger-close-dialog";
        const closeDialogTriggerString = response.headers.get(closeDialogHeader);
        if (!closeDialogTriggerString) {
            return;
        }
        let closeDialogTrigger: RxCloseDialogTrigger;
        try {
            closeDialogTrigger = JSON.parse(closeDialogTriggerString);
        } catch (parseError) {
            const errorMsg = `Failed to parse "${closeDialogHeader}" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
            console.error(errorMsg, { header: closeDialogTriggerString });
            const error = new Error(errorMsg);
            if (ele._rxCallbacks?.onElementTriggerError) {
                ele._rxCallbacks.onElementTriggerError(error);
            }
            if (_callbacks.onElementTriggerError) {
                _callbacks.onElementTriggerError(ele, error);
            }
            return;
        }
        if (!closeDialogTrigger.dialogId) {
            const errorMsg = `Invalid "${closeDialogHeader}" structure - missing required field: dialogId`;
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

    function processFocusElementTrigger(ele: HTMLElement, response: Response): void {
        const focusElementTriggerHeader: RxResponseHeaders = "rx-trigger-focus-element";
        const focusElementTriggerString = response.headers.get(focusElementTriggerHeader);
        if (!focusElementTriggerString) {
            return;
        }
        let focusElementTrigger: RxFocusElementTrigger;
        try {
            focusElementTrigger = JSON.parse(focusElementTriggerString);
        } catch (parseError) {
            const errorMsg = `Failed to parse "${focusElementTriggerHeader}" header as JSON: ${parseError instanceof Error ? parseError.message : String(parseError)}`;
            console.error(errorMsg, { header: focusElementTriggerString });
            const error = new Error(errorMsg);
            if (ele._rxCallbacks?.onElementTriggerError) {
                ele._rxCallbacks.onElementTriggerError(error);
            }
            if (_callbacks.onElementTriggerError) {
                _callbacks.onElementTriggerError(ele, error);
            }
            return;
        }
        if (!focusElementTrigger.elementId) {
            const errorMsg = `Invalid "${focusElementTriggerHeader}" structure - missing required field: elementId`;
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
                if (!Array.isArray(object[key]!)) {
                    object[key] = [object[key]!];
                }
                object[key].push(value);
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

    async function mergeFragments(triggerElement: HTMLElement, response: Response, mergeStrategyArray: MergeStrategy[]): Promise<void> {
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
            const ignoreActiveHeader: RxResponseHeaders = "rx-morph-ignore-active"
            const ignoreActive = response?.headers.has(ignoreActiveHeader);
            Idiomorph.morph(target, Array.from(fragment.content.children), { 
                morphStyle: "outerHTML", 
                ignoreActiveValue: ignoreActive,
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