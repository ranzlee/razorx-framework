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
            rxAllowEventDefault?: string;
            rxDisableInFlight?: string;
            rxDebounce?: string;
            rxPollInterval?: string;
            rxDisableQueueing?: string;
            rxHoistTo?: string;
            rxIncludeState?: string;
            rxRevealMargin?: string;
            rxLoadingIndicator?: string;
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
    loadingIndicatorClasses?: {
        hidden?: string;
        visible?: string;
    };
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
    body: FormData | string | undefined;
    abort: (reason?: string) => void;
};
export type RequestDetail = {
    action: string;
    method: HttpMethod;
    redirect: FetchRedirect;
    body: FormData | string | undefined;
    headers: Headers;
    signal: AbortSignal;
};
export type MergeStrategy = {
    target: string;
    strategy: MergeStrategyType;
};
export type HttpMethod = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
export type FetchRedirect = "follow" | "error" | "manual";
export type MergeStrategyType = InsertPosition | "swap" | "swapInner" | "morph" | "remove";
export type RxResponseHeaders = "rx-merge" | "rx-morph-ignore-active" | "rx-trigger-close-dialog" | "rx-trigger-focus-element" | "rx-trigger-set-state";
export type RxExtendedEvents = "rx:initialized" | "rx:poll" | "rx:revealed";
export type RxCloseDialogTrigger = {
    dialogId: string;
    onCloseData?: string;
    resetFormId?: string;
};
export type RxFocusElementTrigger = {
    elementId: string;
    positionCursorEnd: boolean;
};
export type RxSetStateTrigger = {
    scope: "Session" | "Persistent";
    key: string;
    value?: string | null;
};
export declare const RxRequestHeader = "rx-request";
export declare const razorx: RazorX;
