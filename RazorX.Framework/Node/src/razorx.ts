import { Idiomorph } from "idiomorph";

declare global {
    interface Document {
        rxMutationObserver: MutationObserver;
    }

    interface HTMLElement {
        trigger: string | null,
        interceptors: ElementInterceptors,
    }
}

export interface RazorX {
    init: (options?: Options) => void,
    interceptors: DocumentInterceptors
}

export interface Options {
    log?: boolean,
    addRequestVerificationTokenCookieToRequestHeader?: boolean, //true
    requestVerificationTokenCookieName?: string, //"RequestVerificationToken"
    encodeRequestFormDataAsJson?: boolean, //true
}

export interface DocumentInterceptors {
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

export interface ElementInterceptors {
    beforeFetch?: (requestConfiguration: RequestConfiguration) => void, 
    afterFetch?: (requestDetail: RequestDetail, response: Response) => void,
    beforeDocumentUpdate?: (mergeElement: HTMLElement, strategy: MergeStrategyType) => boolean,
    afterDocumentUpdate?: () => void,
    onElementTriggerError?: (error: unknown) => void,
}

export interface RequestConfiguration {
    trigger: Event,
    action: string,
    method: HttpMethod,
    headers: Headers,
    body: FormData | string,
    abort: (reason?: string) => void
}

export interface RequestDetail {
    action: string,
    method: HttpMethod,
    redirect: FetchRedirect,
    body: FormData | string,
    headers: Headers,
    signal: AbortSignal,
}

export interface MergeStrategy {
    target: string,
    strategy: MergeStrategyType
}

export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export type FetchRedirect = "follow" | "error" | "manual";

export type MergeStrategyType = "swap" | "morph" | "remove";

const _interceptors: DocumentInterceptors = {};

const _requests: Map<string, RequestDetail> = new Map();

const init = (options?: Options): void => {

    if (document.rxMutationObserver) {
        //already initialized
        return;
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
                if (options?.log) {
                    console.log("rxMutationObserver: Removing DOM Element.");
                    console.warn(node);
                }
                removeTriggers(node);
                if (_interceptors.onElementRemoved) {
                    _interceptors.onElementRemoved(node);
                }
            });
            rec.addedNodes.forEach(node => { 
                if (!(node instanceof HTMLElement)) {
                    return;
                }
                if (options?.log) {
                    console.log("rxMutationObserver: Adding DOM Element.");
                    console.warn(node);
                }
                addTriggers(node);
                if (_interceptors.onElementAdded) {
                    _interceptors.onElementAdded(node);
                }
            });
        });
    });
    
    document.addEventListener("DOMContentLoaded", DOMContentLoaded);
    
    const requestVerificationTokenCookieName = "RequestVerificationToken";

    function getMethod(ele: HTMLElement): HttpMethod {
        let m = ele.getAttribute("rx-method")?.trim().toUpperCase() ?? "";
        switch (m) {
            case "GET":
            case "POST": 
            case "PUT":
            case "PATCH":
            case "DELETE":
                return m;
            default:
                const err = `${m} is not a valid HTTP method.`;
                sendError(ele, err);
                throw new Error(err);
        }
    }

    function sendError(ele: HTMLElement, err: unknown): void {
        if (ele.interceptors.onElementTriggerError) {
            ele.interceptors.onElementTriggerError(err);
        }
        if (_interceptors.onElementTriggerError) {
            _interceptors.onElementTriggerError(ele, err);
        }
        console.error(err);
    }

    async function elementTriggerEventHandler(this: HTMLElement, evt: Event): Promise<void> {
        evt.preventDefault();
        try {   
            if (_requests.has(this.id)) {
                throw new Error(`Element ${this.id} is already executing a request.`);
            }
            if (options?.log) {
                console.log("elementTriggerEventHandler: Request triggered for element.");
                console.warn(this);
            }
            let form: HTMLFormElement | null = null;
            if ("form" in this && this.form instanceof HTMLFormElement) {
                form = this.form; 
            }
            if (!form) {
                form = this.closest("form");
            }
            let body = new FormData(form ?? undefined, evt instanceof SubmitEvent ? evt.submitter : null);
            if (!form && "name" in this && "value" in this && typeof this.name === "string" && typeof this.value === "string") {
                body.append(this.name, this.value);
            }
            const ac = new AbortController();
            let request: RequestDetail = {
                action: this.getAttribute("rx-action") ?? "/", 
                method: getMethod(this),
                redirect: "follow",
                body,
                headers: new Headers({ "rx-request": "true" }),
                signal: ac.signal,
            };
            if (options?.addRequestVerificationTokenCookieToRequestHeader === undefined 
                || options.addRequestVerificationTokenCookieToRequestHeader === true) {
                addAntiforgeryCookieToRequest(request);
                if (options?.log) {
                    console.log("elementTriggerEventHandler: Antiforgery cookie added to request.");
                }
            }
            if (options?.encodeRequestFormDataAsJson === undefined
                || options.encodeRequestFormDataAsJson === true) {
                encodeBodyAsJson(request);
                if (options?.log) {
                    console.log("elementTriggerEventHandler: FormData converted to JSON.");
                }
            }
            if (/GET|DELETE/.test(request.method!)) {
                let params = new URLSearchParams(request.body! as unknown as Record<string, string>);
                if (params.size) {
                    request.action += (/\?/.test(request.action!) ? "&" : "?") + params;
                }
                request.body = "";
                if (options?.log) {
                    console.log("elementTriggerEventHandler: GET/DELETE Request body converted to URLSearchParams.");
                }
            }
            if (options?.log) {
                console.log("elementTriggerEventHandler: RequestDetail created.");
                console.warn(request);
            }
            let config: RequestConfiguration = {
                trigger: evt,
                action: request.action,
                method: request.method,
                body: request.body,
                headers: request.headers,
                abort: ac.abort.bind(ac),
            }
            if (options?.log) {
                console.log("elementTriggerEventHandler: RequestConfiguration created.");
                console.warn(config);
            }
            if (this.interceptors.beforeFetch) {
                this.interceptors.beforeFetch(config);
            }
            if (_interceptors.beforeFetch) {
                _interceptors.beforeFetch(this, config);
            }
            if (ac.signal.aborted) {
                if (options?.log) {
                    console.log("elementTriggerEventHandler: Request aborted before fetch for element.");
                    console.warn(this);
                }
                _requests.delete(this.id);
                return;
            }
            _requests.set(this.id, request);
            let response: Response | null = null;
            try {
                if (options?.log) {
                    console.log(`elementTriggerEventHandler: Fetching ${request.action} for element.`);
                    console.warn(this);
                }
                response = await fetch(request.action, request);
                if (ac.signal.aborted) {
                    if (options?.log) {
                        console.log("elementTriggerEventHandler: Request aborted during fetch for element.");
                        console.warn(this);
                    }
                    return;
                }
                if (this.interceptors.afterFetch) {
                    this.interceptors.afterFetch(request, response);
                }
                if (_interceptors.afterFetch) {
                    _interceptors.afterFetch(this, request, response);
                }
            } catch(error: unknown) {
                sendError(this, error);
            } finally {
                _requests.delete(this.id);
            }
            if (!response) {
                sendError(this, `Element ${this.id} has no response after request.`);
                return;
            }
            if (response.status === 202) {
                //used to issue a follow-up GET request for rendering
                if (options?.log) {
                    console.log("elementTriggerEventHandler: Response 202 for element.");
                    console.warn(this);
                }
                const location = response.headers.get("location");
                if (location && location.trim() !== "") {
                    if (options?.log) {
                        console.log(`elementTriggerEventHandler: Response 202 replacing location with ${location}.`);
                    }
                    window.location.replace(location);
                }
                return; 
            }
            if (response.status === 204) {
                //skip response merge 
                if (options?.log) {
                    console.log("elementTriggerEventHandler: Response 204 for element.");
                    console.warn(this);
                }
                return;
            }
            if (response.status >= 400) {
                //dev error response
                if (options?.log) {
                    console.log(`elementTriggerEventHandler: Response ${response.status} for element.`);
                    console.warn(this);
                }
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
            if (options?.log) {
                console.log("elementTriggerEventHandler: Response merge processing for request triggered by element.");
                console.warn(this);
            }
            if (document.startViewTransition !== undefined) {
                await document.startViewTransition(async () => await mergeFragments(this, response)).finished;
            } else {
                if (options?.log) {
                    console.log("elementTriggerEventHandler: startViewTransition is not supported.");
                }
                await mergeFragments(this, response);
            }
            if (this.interceptors.afterDocumentUpdate) {
                this.interceptors.afterDocumentUpdate();
            }
            if (_interceptors.afterDocumentUpdate) {
                _interceptors.afterDocumentUpdate(this);
            }
        } catch(error: unknown) {
            sendError(this, error);
        } 
    } 

    function normalizeScriptTags(fragment: HTMLElement): void {
        Array.from(fragment.querySelectorAll("script")).forEach(script => {
            const newScript = document.createElement("script");
            Array.from(script.attributes).forEach(attr => {
                newScript.setAttribute(attr.name, attr.value);
            });
            newScript.textContent = script.textContent;
            newScript.async = false;
            const parent = script.parentNode;
            parent?.insertBefore(newScript, script);
            script.remove();
        });
    }

    function getFragment(fragments: ChildNode[], mergeStrategy: MergeStrategy): HTMLTemplateElement | undefined {
        const fragmentId = `${mergeStrategy.target}-fragment`;
        const fragment = fragments.find(f => f instanceof HTMLTemplateElement && f.id === fragmentId) as HTMLTemplateElement | undefined;
        if (!fragment) {
            throw new Error(`Expected a response body fragment with id=\"${fragmentId}\"`);
        }
        if (!fragment.hasChildNodes) {
            throw new Error(`Expected one or more child elements in fragment with id=\"${fragmentId}\"`);
        }
        return fragment;
    }

    function getTarget(triggerElement: HTMLElement, fragment: HTMLTemplateElement, mergeStrategy: MergeStrategy): HTMLElement | undefined {
        const target = document.getElementById(mergeStrategy.target);
        if (!target) {
            throw new Error(`Expected an HTML element with id=\"${mergeStrategy.target}\"`);
        }
        if (triggerElement.interceptors.beforeDocumentUpdate && triggerElement.interceptors.beforeDocumentUpdate(fragment, mergeStrategy.strategy) === false) {
            return;
        }
        if (_interceptors.beforeDocumentUpdate && _interceptors.beforeDocumentUpdate(triggerElement, fragment, mergeStrategy.strategy) === false) {
            return;
        }
        return target;
    }

    async function mergeFragments(triggerElement: HTMLElement, response: Response): Promise<void> {
        const merge = response?.headers.get("rx-merge");
        if (!merge) {
            throw new Error("Expected a \"rx-merge\" header object.");
        }
        const mergeStrategyArray: Array<MergeStrategy> = JSON.parse(merge);
        const parser = new DOMParser();
        const doc = parser.parseFromString("<body><template>" + await response.text() + "</template></body>", "text/html");
        const template = doc.body.querySelector("template")?.content;
        const fragments = Array.from(template?.childNodes ?? []);
        const swaps = mergeStrategyArray.filter(s => s.strategy === "swap");
        const isFirefox = navigator.userAgent.toLowerCase().includes("firefox");
        swaps.forEach(s => {
            const fragment = getFragment(fragments, s);
            if (!fragment) {
                return;
            }
            const target = getTarget(triggerElement, fragment, s);
            if (!target) {
                return;
            }
            target.replaceWith(fragment.content);
            //special processing required for script elements in firefox
            if (!isFirefox) {
                return;
            }
            const newTarget = document.getElementById(target.id);
            if (newTarget) {
                normalizeScriptTags(newTarget);
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
            const ignoreActive = response?.headers.get("rx-morph-ignore-active") === "True";
            Idiomorph.morph(target, Array.from(fragment.content.children), { 
                morphStyle: "outerHTML", 
                ignoreActiveValue: ignoreActive,
            })?.forEach(n => {
                if (!(n instanceof HTMLElement)) {
                    return;
                }
                addTriggers(n);
                if (isFirefox) {
                    //special processing required for script elements in firefox
                    normalizeScriptTags(n);
                }
                if (_interceptors.onElementMorphed) {
                    _interceptors.onElementMorphed(n);
                }
            });
        });
        const removals = mergeStrategyArray.filter(s => s.strategy === "remove");
        removals.forEach(r => {
            const target = document.getElementById(r.target);
            if (!target) {
                return;
            }
            if (triggerElement.interceptors.beforeDocumentUpdate && triggerElement.interceptors.beforeDocumentUpdate(target, r.strategy) === false) {
                return;
            }
            if (_interceptors.beforeDocumentUpdate && _interceptors.beforeDocumentUpdate(triggerElement, target, r.strategy) === false) {
                return;
            }
            target.remove();
        });
    }

    function addAntiforgeryCookieToRequest(detail: RequestDetail): void {
        let tokenName = options?.requestVerificationTokenCookieName ?? requestVerificationTokenCookieName;
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${tokenName}=`);
        if (parts.length !== 2) {
            return;
        }
        if (!detail.headers) {
            return;
        }
        detail.headers.set(`${tokenName}`, parts.pop()!.split(";").shift() ?? "");
    }

    function encodeBodyAsJson(detail: RequestDetail): void {
        detail.headers?.set("Content-Type", "application/json");
        const object: any = {};
        if (!(detail.body instanceof FormData)) {
            return;
        }
        detail.body?.forEach((value: FormDataEntryValue, key: string) => {
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
        document.rxMutationObserver.observe(document.documentElement, { childList: true, subtree: true });
        if (_interceptors.beforeDocumentProcessed) {
            _interceptors.beforeDocumentProcessed();
        }
        addTriggers(document.body);
        if (_interceptors.afterDocumentProcessed) {
            _interceptors.afterDocumentProcessed();
        }
    }

    function addTriggers(ele: HTMLElement) {
        if (ele.closest("[rx-ignore]") !== null) {
            return;
        }
        if (ele.matches("[rx-action]")) {
            let initializeElement = true;
            if (_interceptors.beforeInitializeElement) {
                initializeElement = _interceptors.beforeInitializeElement(ele);
            }
            if (initializeElement) {
                if (!ele.id || ele.id.trim() === "") {
                    const err = "Element with \"rx-action\" must have a unique ID.";
                    throw new Error(err);
                }
                //enforce the existence of the element.interceptors property
                Object.defineProperty(ele, "interceptors", {
                    value: {},
                    writable: false,
                });
                ele.trigger = ele.getAttribute("rx-trigger");
                if (!ele.trigger) {
                    ele.trigger = ele.matches("form")
                        ? "submit" 
                        : ele.matches("input:not([type=button]),select,textarea") ? "change" : "click";
                }
                //id is required and mustn't be modified
                Object.freeze(ele.id);
                //trigger modification is not allowed since we hook/unhook the event listener based on it
                Object.freeze(ele.trigger);
                ele.addEventListener(ele.trigger, elementTriggerEventHandler);
                if (_interceptors.afterInitializeElement) {
                    _interceptors.afterInitializeElement(ele);
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
        if (ele.trigger) {	
            ele.removeEventListener(ele.trigger, elementTriggerEventHandler);
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

const razorxProto: any = {
    init: Object.freeze(init),
}

//enforce the existence of the razorx.interceptors property
Object.defineProperty(razorxProto, "interceptors", {
    value: _interceptors,
    writable: false,
});

export const razorx = razorxProto as RazorX;