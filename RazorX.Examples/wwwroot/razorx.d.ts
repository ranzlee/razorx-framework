declare global {
    interface Document {
        rxMutationObserver: MutationObserver;
    }
    interface HTMLElement {
        dataset: {
            rxIgnore?: string;
            rxAction?: string;
            rxMethod?: string;
            rxTrigger?: string;
            rxDisableInFlight?: string;
            rxDebounce?: string;
        };
        addRxCallbacks?: (callbacks: ElementCallbacks) => void;
        _rxCallbacks?: ElementCallbacks;
    }
}
export type RazorX = {
    init: (options?: Options) => void;
    addCallbacks: (callbacks: DocumentCallbacks) => void;
};
export type Options = {
    addCookieToRequestHeader?: string | string[];
    encodeRequestFormDataAsJson?: boolean;
};
export type DocumentCallbacks = {
    beforeDocumentProcessed?: () => void;
    afterDocumentProcessed?: () => void;
    beforeInitializeElement?: (element: HTMLElement) => boolean;
    afterInitializeElement?: (element: HTMLElement) => void;
    beforeFetch?: (triggerElement: HTMLElement, requestConfiguration: RequestConfiguration) => void;
    afterFetch?: (triggerElement: HTMLElement, requestDetail: RequestDetail, response: Response) => void;
    beforeDocumentUpdate?: (triggerElement: HTMLElement, mergeElement: HTMLElement, strategy: MergeStrategyType) => boolean;
    afterDocumentUpdate?: (triggerElement: HTMLElement) => void;
    onElementAdded?: (addedElement: HTMLElement) => void;
    onElementMorphed?: (morphedElement: HTMLElement) => void;
    onElementRemoved?: (removedElement: HTMLElement) => void;
    onElementTriggerError?: (triggerElement: HTMLElement, error: unknown) => void;
};
export type ElementCallbacks = {
    beforeFetch?: (requestConfiguration: RequestConfiguration) => void;
    afterFetch?: (requestDetail: RequestDetail, response: Response) => void;
    beforeDocumentUpdate?: (mergeElement: HTMLElement, strategy: MergeStrategyType) => boolean;
    afterDocumentUpdate?: () => void;
    onElementTriggerError?: (error: unknown) => void;
};
export type RequestConfiguration = {
    trigger: Event;
    action: string;
    method: HttpMethod;
    headers: Headers;
    body: FormData | string;
    abort: (reason?: string) => void;
};
export type RequestDetail = {
    action: string;
    method: HttpMethod;
    redirect: FetchRedirect;
    body: FormData | string;
    headers: Headers;
    signal: AbortSignal;
};
export type MergeStrategy = {
    target: string;
    strategy: MergeStrategyType;
};
export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
export type FetchRedirect = "follow" | "error" | "manual";
export type MergeStrategyType = "swap" | "afterbegin" | "afterend" | "beforebegin" | "beforeend" | "morph" | "remove";
export declare const RxRequestHeader = "rx-request";
export declare enum RxResponseHeaders {
    Merge = "rx-merge",
    MorphIgnoreActive = "rx-morph-ignore-active"
}
export declare const razorx: RazorX;
