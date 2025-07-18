import { Idiomorph } from "idiomorph";

declare global {
    interface Document {
        rxMutationObserver: MutationObserver
    }

    interface HTMLElement {
        dataset: {
            rxIgnore?: string, //data-rx-ignore
            rxAction?: string, //data-rx-action
            rxMethod?: string, //data-rx-method
            rxTrigger?: string, //data-rx-trigger
            rxDisableInFlight?: string, //data-rx-disable-in-flight
            rxDebounce?: string //data-rx-debounce
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
    body: FormData | string,
    abort: (reason?: string) => void
}

export type RequestDetail = {
    action: string,
    method: HttpMethod,
    redirect: FetchRedirect,
    body: FormData | string,
    headers: Headers,
    signal: AbortSignal,
}

export type MergeStrategy = {
    target: string,
    strategy: MergeStrategyType
}

export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export type FetchRedirect = "follow" | "error" | "manual";

//TODO: union with InsertPosition
export type MergeStrategyType = "swap" | "afterbegin" | "afterend" | "beforebegin" | "beforeend" | "morph" | "remove";

export const RxRequestHeader = "rx-request";

export enum RxResponseHeaders {
    Merge = "rx-merge",
    MorphIgnoreActive = "rx-morph-ignore-active"
}

const _processedScriptTag = "data-rx-script-processed";

const _requestRefTracker: Set<string> = new Set();

const _debouncedRequests: Map<string, (ele: HTMLElement, evt: Event) => Promise<void>> = new Map();

const _fetchRedirect: FetchRedirect = "follow";

const _callbacks: DocumentCallbacks = {};

const _isFirefox = navigator.userAgent.toLowerCase().includes("firefox");

const _addCallbacks = (callbacks: DocumentCallbacks) => {
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

    if (document.rxMutationObserver) {
        //already initialized
        return;
    }

    if (callbacks) {
        _addCallbacks(callbacks);
    }

    document.rxMutationObserver = new MutationObserver(recs => {
        recs.forEach(rec => {
            if (rec.type !== "childList") {
                return;
            }
            rec.removedNodes.forEach(node => { 
                if (!(node instanceof HTMLElement)) {
                    return;
                }
                removeTriggers(node);
                if (_callbacks.onElementRemoved) {
                    _callbacks.onElementRemoved(node);
                }
            });
            rec.addedNodes.forEach(node => { 
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
    
    document.addEventListener("DOMContentLoaded", DOMContentLoaded);

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
                sendError(ele, err);
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

    function toggleDisable(ele: HTMLElement, disable: boolean = false) {
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

    function debounce(func: (ele: HTMLElement, evt: Event) => Promise<void>, delay: number): (ele: HTMLElement, evt: Event) => Promise<void> {
        let timeoutId: number | null = null;
        let pending: Array<{ 
            resolve: (value: void) => void; 
            reject: (reason?: unknown) => void 
        }> = [];
        return (ele: HTMLElement, evt: Event): Promise<void> => {
            return new Promise((resolve, reject) => {
                if (timeoutId) {
                    clearTimeout(timeoutId);
                }
                pending.push({ resolve, reject });
                timeoutId = setTimeout(() => {
                    timeoutId = null;
                    Promise.resolve(func(ele, evt))
                        .then((result) => {
                            pending.forEach(({ resolve: res }) => res(result)); 
                        })
                        .catch((error: unknown) => {
                            pending.forEach(({ reject: rej }) => rej(error));
                        })
                        .finally(() => {
                            pending = []; 
                        });
                }, delay);
            });
        };
    }


    async function elementTriggerEventHandler(this: HTMLElement, evt: Event): Promise<void> {
        //TODO: is request queueing also needed?
        if (!this.dataset.rxDebounce) {
            await elementTriggerProcessor(this, evt);
            return;
        }
        const delay = parseInt(this.dataset.rxDebounce, 10);
        if (Number.isNaN(delay) || delay <= 0) {
            throw new Error(`Element ${this.id} data-rx-debounce attribute value must be a valid number greater than zero.`);
        }
        let debounceElementTrigger = _debouncedRequests.get(this.id);
        if (debounceElementTrigger) {
            await debounceElementTrigger(this, evt);
        } else {
            debounceElementTrigger = debounce(elementTriggerProcessor, delay);
            _debouncedRequests.set(this.id, debounceElementTrigger);
            await debounceElementTrigger(this, evt);
        }
    }

    async function elementTriggerProcessor(ele: HTMLElement, evt: Event): Promise<void> {
        evt.preventDefault();
        try {   
            _debouncedRequests.delete(ele.id);
            if (_requestRefTracker.has(ele.id)) {
                throw new Error(`Element ${ele.id} is already executing a request.`);
            }
            let form: HTMLFormElement | null = null;
            if ("form" in ele && ele.form instanceof HTMLFormElement) {
                form = ele.form; 
            }
            if (!form) {
                form = ele.closest("form");
            }
            const body = new FormData(form ?? undefined, evt instanceof SubmitEvent ? evt.submitter : null);
            if (!form && "name" in ele && "value" in ele && typeof ele.name === "string" && typeof ele.value === "string") {
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
                    options.addCookieToRequestHeader.forEach(cookie => {
                        addCookieToRequest(request, cookie);
                    });
                } else {
                    addCookieToRequest(request, options.addCookieToRequestHeader);
                }
            }
            if (options?.encodeRequestFormDataAsJson === undefined
                || options.encodeRequestFormDataAsJson === true) {
                encodeBodyAsJson(request);
            }
            if (/GET|DELETE/.test(request.method!)) {
                const params = new URLSearchParams(request.body! as unknown as Record<string, string>);
                if (params.size) {
                    request.action += (/\?/.test(request.action!) ? "&" : "?") + params;
                }
                request.body = "";
            }
            const config: RequestConfiguration = {
                trigger: evt,
                action: request.action,
                method: request.method,
                body: request.body,
                headers: request.headers,
                abort: ac.abort.bind(ac),
            }
            _requestRefTracker.add(ele.id);
            const disableElement = ele.dataset.rxDisableInFlight ?? null;
            let response: Response | null = null;
            try {
                if (disableElement !== null && disableElement.toLowerCase() !== "false") {
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
            } catch(error: unknown) {
                sendError(ele, error);
            } finally {
                _requestRefTracker.delete(ele.id);
                if (disableElement !== null && disableElement.toLowerCase() !== "false") {
                    toggleDisable(ele, false);
                }
            }
            if (!response) {
                sendError(ele, `Element ${ele.id} has no response after request.`);
                return;
            }
            if (response.status === 202) {
                //used to issue a follow-up GET request for rendering
                const location = response.headers.get("location");
                if (location && location.trim() !== "") {
                    window.location.replace(location);
                }
                return; 
            }
            if (response.status === 204) {
                //skip response merge 
                return;
            }
            if (response.status >= 400) {
                //dev error response
                document.rxMutationObserver?.disconnect();
                removeTriggers(document.body);
                document.head.innerHTML = "<title>Error</title>";
                const contentType = response.headers.get("content-type");
                if (contentType && (contentType.includes("application/json") || contentType.includes("application/problem+json"))) {
                    const formattedJson = JSON.stringify(await response.json(), null, 2); 
                    document.body.innerHTML = `<pre><code>${formattedJson}</code></pre>`;				
                } else {
                    document.body.innerText = await response.text();
                }
                return;
            }
            if (document.startViewTransition !== undefined) {
                await document.startViewTransition(async () => await mergeFragments(ele, response)).finished;
            } else {
                await mergeFragments(ele, response);
            }
            if (ele._rxCallbacks!.afterDocumentUpdate) {
                ele._rxCallbacks!.afterDocumentUpdate();
            }
            if (_callbacks.afterDocumentUpdate) {
                _callbacks.afterDocumentUpdate(ele);
            }
        } catch(error: unknown) {
            sendError(ele, error);
        } 
    } 

    function processScript(script: HTMLScriptElement): void {
        if (script.hasAttribute(_processedScriptTag)) {
            script.removeAttribute(_processedScriptTag);
            return;
        }
        const newScript = document.createElement("script");
        Array.from(script.attributes).forEach(attr => {
            newScript.setAttribute(attr.name, attr.value);
        });
        newScript.setAttribute(_processedScriptTag, "");
        newScript.textContent = script.textContent;
        newScript.async = false;
        const parent = script.parentNode;
        parent?.insertBefore(newScript, script);
        script.remove();
    }

    function normalizeScriptTags(fragment: HTMLElement): void {
        if (!_isFirefox) {
            return;
        }
        if (fragment instanceof HTMLScriptElement) {
            processScript(fragment);
            return;
        }
        Array.from(fragment.querySelectorAll("script")).forEach(script => {
            processScript(script);
        });
    }

    function getFragment(fragments: ChildNode[], mergeStrategy: MergeStrategy): HTMLTemplateElement | undefined {
        const fragmentId = `${mergeStrategy.target}-fragment`;
        const fragment = fragments.find(f => f instanceof HTMLTemplateElement && f.id === fragmentId) as HTMLTemplateElement | undefined;
        if (!fragment) {
            throw new Error(`Expected a response body fragment with id="${fragmentId}"`);
        }
        if (!fragment.hasChildNodes) {
            throw new Error(`Expected one or more child elements in fragment with id="${fragmentId}"`);
        }
        return fragment;
    }

    function getTarget(triggerElement: HTMLElement, fragment: HTMLTemplateElement, mergeStrategy: MergeStrategy): HTMLElement | undefined {
        const target = document.getElementById(mergeStrategy.target);
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

    async function mergeFragments(triggerElement: HTMLElement, response: Response): Promise<void> {
        const merge = response?.headers.get(RxResponseHeaders.Merge);
        if (!merge) {
            throw new Error(`Expected a "${RxResponseHeaders.Merge}" header object.`);
        }
        const mergeStrategyArray: MergeStrategy[] = JSON.parse(merge);
        const parser = new DOMParser();
        const doc = parser.parseFromString("<body><template>" + await response.text() + "</template></body>", "text/html");
        const template = doc.body.querySelector("template")?.content;
        const fragments = Array.from(template?.childNodes ?? []);
        const swaps = mergeStrategyArray.filter(s => {
            if (s.strategy === "swap" 
                || s.strategy === "afterbegin"
                || s.strategy === "afterend"
                || s.strategy === "beforebegin"
                || s.strategy === "beforeend") {
                return true;
            }
            return false;
        });
        swaps.forEach(s => {
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
            } else {
                const newContent = Array.from(fragment.content.children);
                if (newContent.length === 0) {
                    return;
                }
                //insert the first element based on the strategy
                target.insertAdjacentElement(s.strategy as InsertPosition, newContent[0]);
                let thisEle = newContent[0];
                //insert the remainder afterend of the previous element
                for (let i = 1; i < newContent.length; i++) {
                    thisEle.insertAdjacentElement("afterend", newContent[i]);
                    thisEle = newContent[i];
                }
            }
        });
        const morphs = mergeStrategyArray.filter(s => s.strategy === "morph");
        morphs.forEach(s => {
            const fragment = getFragment(fragments, s);
            if (!fragment) {
                return;
            }
            const target = getTarget(triggerElement, fragment, s);
            if (!target) {
                return;
            }
            const ignoreActive = response?.headers.has(RxResponseHeaders.MorphIgnoreActive);
            Idiomorph.morph(target, Array.from(fragment.content.children), { 
                morphStyle: "outerHTML", 
                ignoreActiveValue: ignoreActive,
            })?.forEach(n => {
                if (!(n instanceof HTMLElement)) {
                    return;
                }
                //TODO: EDGE CASE - what if the data-rx-trigger attribute is morphed?
                //addTriggers(node);
                if (_callbacks.onElementMorphed) {
                    _callbacks.onElementMorphed(n);
                }
            });
        });
        const removals = mergeStrategyArray.filter(s => s.strategy === "remove");
        removals.forEach(r => {
            const target = document.getElementById(r.target);
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
        detail.body?.forEach((value: FormDataEntryValue, key: string) => {
            if (value instanceof Blob) {
                //skip any input [type=file] for XMLHttpRequest processing
                return;
            }
            if (Object.hasOwn(object, key)) {
                if (!Array.isArray(object[key])) {
                    object[key] = [object[key]];
                }
                object[key].push(value);
            } else {
                object[key] = value;
            }
        })
        detail.body = JSON.stringify(object);
    }

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

    function addTriggers(ele: HTMLElement) {
        const firstIgnore = ele.closest("[data-rx-ignore]");
        if (firstIgnore && firstIgnore instanceof HTMLElement && firstIgnore.dataset.rxIgnore?.toLowerCase() !== "false") {
            return;
        }
        //if (ele.matches(`[${RxAttributes.Action}]`)) {
        if (ele.dataset.rxAction) {
            let initializeElement = true;
            if (_callbacks.beforeInitializeElement) {
                initializeElement = _callbacks.beforeInitializeElement(ele);
            }
            if (initializeElement) {
                if (!ele.id || ele.id.trim() === "") {
                    const err = `Element with "data-rx-action" must have a unique ID.`;
                    throw new Error(err);
                }
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
                let rxTrigger = ele.dataset.rxTrigger;
                //TODO: allow multiple triggers -e.g., "click keydown"
                if (!rxTrigger) {
                    rxTrigger = ele.matches("form")
                        ? "submit" 
                        : ele.matches("input:not([type=button]),select,textarea") ? "change" : "click";
                    ele.setAttribute("data-rx-trigger", rxTrigger);
                }
                //id is required and mustn't be modified
                Object.freeze(ele.id);
                ele.addEventListener(ele.dataset.rxTrigger!, elementTriggerEventHandler);
                if (_callbacks.afterInitializeElement) {
                    _callbacks.afterInitializeElement(ele);
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
                addTriggers(child);
            }
        }
    }

    function removeTriggers(ele: HTMLElement) {
        if (ele.dataset.rxTrigger) {	
            //remove the event handler reference
            ele.removeEventListener(ele.dataset.rxTrigger, elementTriggerEventHandler);
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
}

const razorxProto: unknown = {
    init: Object.freeze(_init),
    addCallbacks: Object.freeze(_addCallbacks)
}

export const razorx = razorxProto as RazorX;