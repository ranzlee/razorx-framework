import { razorx } from './razorx.js'

const triggers = [];
let swapResult = true;
let morphResult = true;

razorx.interceptors.beforeDocumentProcessed = () => {
    const details = document.getElementById("test-init-details");
    details.replaceChildren([]);
    addDetail(details, "Hook:beforeDocumentProcessed invoked.");
    razorx.interceptors.afterInitializeElement = (ele) => {
        if (ele.id === "test-swap") {
            ele.interceptors.beforeFetch = swapBeforeFetch;
            ele.interceptors.afterFetch = swapAfterFetch;
            ele.interceptors.beforeDocumentUpdate = swapBeforeDocumentUpdate;
            ele.interceptors.afterDocumentUpdate = swapAfterDocumentUpdate;
            ele.interceptors.onElementTriggerError = swapOnElementTriggerError;
        }
        if (ele.id === "test-morph") {
            ele.interceptors.beforeDocumentUpdate = morphBeforeDocumentUpdate;
            ele.interceptors.afterDocumentUpdate = morphAfterDocumentUpdate;
        }
        triggers.push(ele);
        addDetail(details, `Element ${ele.id} initialized.`);
    }
}

razorx.interceptors.afterDocumentProcessed = () => {
    const count = triggers.length;
    const details = document.getElementById("test-init-details");
    addDetail(details, "Hook:afterDocumentProcessed invoked.");
    addDetail(details, `${count} of 3 elements initialized.`, count !== 3);
    if (count === 3) {
        document.getElementById("test-init-result").style.color = "limegreen";
    } else {
        document.getElementById("test-init-result").style.color = "lightcoral";
    }
    triggers.forEach(ele => { 
        ele.click();
    });
}

function addDetail(list, msg, isError = false) {
    const li = document.createElement("li");
    li.innerText = msg;
    li.style.color = isError ? "lightcoral" : "limegreen";
    if (!isError) {
        li.setAttribute("data-passed", null);
    }
    list.appendChild(li); 
}

/**
 * 
 * @param {import('./razorx.js').RequestConfiguration} requestConfiguration 
 */
function swapBeforeFetch(requestConfiguration) {
    swapResult = true;
    const details = document.getElementById("test-swap-details");
    details.replaceChildren([]);
    addDetail(details, "Hook:swapBeforeFetch invoked.");
    try {
        if (requestConfiguration.trigger.type === "custom-trigger") {
            addDetail(details, "RequestConfiguration custom trigger validated.");
        } else {
            swapResult = false;
            addDetail(details, "RequestConfiguration custom trigger was not equal to \"custom-trigger\".", true);
        }
        if (requestConfiguration.method === "POST") {
            addDetail(details, "RequestConfiguration method validated.");
        } else {
            swapResult = false;
            addDetail(details, "RequestConfiguration method was not equal to \"POST\".", true);
        }
        if (requestConfiguration.action === "/test-swap") {
            addDetail(details, "RequestConfiguration action validated.");
        } else {
            swapResult = false;
            addDetail(details, "RequestConfiguration action was not equal to \"/test-swap\".", true);
        }
        if (requestConfiguration.body) {
            addDetail(details, "RequestConfiguration body validated.");
        } else {
            swapResult = false;
            addDetail(details, "RequestConfiguration body was not defined.", true);
        }
        var body = JSON.parse(requestConfiguration.body);
        if (body.test && body.test === "swap") {
            addDetail(details, "RequestConfiguration body JSON validated.");
        } else {
            swapResult = false;
            addDetail(details, "RequestConfiguration body JSON was not equal to { test: \"swap\" }.", true);
        }
        if (requestConfiguration.headers.get("rx-request") === "true") {
            addDetail(details, "RequestConfiguration \"rx-request\" header validated.");
        } else {
            swapResult = false;
            addDetail(details, "RequestConfiguration \"rx-request\" header was not equal to \"true\".", true);
        }
        if (requestConfiguration.abort instanceof Function) {
            addDetail(details, "RequestConfiguration abort availability validated.");
        } else {
            swapResult = false;
            addDetail(details, "RequestConfiguration abort was not set.", true);
        }
        requestConfiguration.headers.append("test", "swap");
    } catch (error) { 
        console.error(error);
    }
}

/**
 * 
 * @param {import('./razorx.js').RequestDetail} requestDetail
 * @param {Response} response 
 */
function swapAfterFetch(requestDetail, response) {
    const details = document.getElementById("test-swap-details");
    addDetail(details, "Hook:swapAfterFetch invoked.");
    try {
        if (requestDetail.headers.get("test") !== null && requestDetail.headers.get("test") === response.headers.get("test")) {
            addDetail(details, "Response header validated.");
        } else {
            swapResult = false;
            addDetail(details, "Response header was not equal to \"swap\".", true);
        }
    } catch(error) { 
        console.error(error);
    }
}

/**
 * 
 * @param {HTMLElement} mergeElement 
 * @param {import('./razorx.js').MergeStrategyType} strategy 
 */
function swapBeforeDocumentUpdate(mergeElement, strategy) {
    const details = document.getElementById("test-swap-details");
    addDetail(details, "Hook:swapBeforeDocumentUpdate invoked.");
    try {
        if (mergeElement instanceof HTMLTemplateElement && mergeElement.id === "test-swap-target-fragment") {
            addDetail(details, "Hook:beforeDocumentUpdate target validated.");
        } else {
            swapResult = false;
            addDetail(details, "Hook:beforeDocumentUpdate target is not the expected element.", true);
        }
        if (strategy === "swap") {
            addDetail(details, "Hook:beforeDocumentUpdate strategy validated.");
        } else {
            swapResult = false;
            addDetail(details, "Hook:beforeDocumentUpdate strategy was not equal to \"swap\".", true);
        }
    } catch(error) { 
        console.error(error);
    }
}

/**
 * 
 * @param {unknown} error 
 */
function swapOnElementTriggerError(error) {
    const details = document.getElementById("test-swap-details");
    addDetail(details, `Hook:swapOnElementTriggerError invoked (${error}).`);
}

function swapAfterDocumentUpdate() {
    const details = document.getElementById("test-swap-details");
    addDetail(details, "Hook:swapAfterDocumentUpdate invoked.");
    try {
        if (document.getElementById("test-swap-target").getAttribute("data-merged")) {
            addDetail(details, "Element swap validated.");
        } else {
            swapResult = false;
            addDetail(details, "Element swap result failed.", true);
        }
        let val = swapTestFn("hello");
        if (val === "hello swap") {
            addDetail(details, "Swapped in script execution validated.");
        } else {
            swapResult = false;
            addDetail(details, "Swapped in script execution failed.", true);
        }
    } catch(error) { 
        console.error(error);
    }
    const assertsPassed = Array.from(details.children).filter(e => e.getAttribute("data-passed")).length;
    if (assertsPassed === 17) {
        addDetail(details, `${assertsPassed} of 17 asserts passed.`);
    } else {
        swapResult = false;
        addDetail(details, `${assertsPassed} of 17 asserts passed.`, true);
    }
    if (swapResult) {
        document.getElementById("test-swap-result").style.color = "limegreen";
    } else {
        document.getElementById("test-swap-result").style.color = "lightcoral";
    }
}

/**
 * 
 * @param {HTMLElement} mergeElement 
 * @param {import('./razorx.js').MergeStrategyType} strategy 
 */
function morphBeforeDocumentUpdate(mergeElement, strategy) {
    morphResult = true;
    const details = document.getElementById("test-morph-details");
    details.replaceChildren([]);
    addDetail(details, "Hook:morphBeforeDocumentUpdate invoked.");
    try {
        if (mergeElement instanceof HTMLTemplateElement && mergeElement.id === "test-morph-target-fragment") {
            addDetail(details, "Hook:beforeDocumentUpdate target validated.");
        } else {
            morphResult = false;
            addDetail(details, "Hook:beforeDocumentUpdate target is not the expected element.", true);
        }
        if (strategy === "morph") {
            addDetail(details, "Hook:beforeDocumentUpdate strategy validated.");
        } else {
            morphResult = false;
            addDetail(details, "Hook:beforeDocumentUpdate strategy was not equal to \"morph\".", true);
        }
    } catch(error) { 
        console.error(error);
    }
}

function morphAfterDocumentUpdate() {
    const details = document.getElementById("test-morph-details");
    addDetail(details, "Hook:morphAfterDocumentUpdate invoked.");
    try {
        if (document.getElementById("test-morph-target").getAttribute("data-merged")) {
            addDetail(details, "Element morph validated.");
        } else {
            morphResult = false;
            addDetail(details, "Element morph result failed.", true);
        }
        let val = morphTestFn("hello");
        if (val === "hello morph") {
            addDetail(details, "Morphed in script execution validated.");
        } else {
            morphResult = false;
            addDetail(details, "Morphed in script execution failed.", true);
        }
    } catch(error) { 
        console.error(error);
    }
    const assertsPassed = Array.from(details.children).filter(e => e.getAttribute("data-passed")).length;
    if (assertsPassed === 6) {
        addDetail(details, `${assertsPassed} of 6 asserts passed.`);
    } else {
        morphResult = false;
        addDetail(details, `${assertsPassed} of 6 asserts passed.`, true);
    }
    if (morphResult) {
        document.getElementById("test-morph-result").style.color = "limegreen";
    } else {
        document.getElementById("test-morph-result").style.color = "lightcoral";
    }
}
