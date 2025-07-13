import { razorx } from './razorx.js'

const triggers = [];

/**
 * 
 * @param {HTMLElement} ele 
 */
function reset(ele) {
    document.getElementById(`${ele.id}-result`).className = "";
}

/**
 * 
 * @param {HTMLElement} ele 
 * @param {boolean} passed 
 */
function setResult(ele, passed) {
    if (passed) {
        document.getElementById(`${ele.id}-result`).className = "pass";
    } else {
        document.getElementById(`${ele.id}-result`).className = "fail";
    }
}

razorx.addCallbacks({
    afterInitializeElement: (ele) => {
        if (ele.id !== "new-swap-target-with-trigger" && ele.id !== "new-morph-target-with-trigger") {
            triggers.push(ele);
        }
        if (ele.id === "test-01") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    setResult(ele, requestConfiguration.trigger.type === "custom-trigger");
                },
            });
        }
        if (ele.id === "test-02") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    setResult(ele, requestConfiguration.method === "POST");
                },
            });
        }
        if (ele.id === "test-03") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    setResult(ele, requestConfiguration.action === "/test-swap");
                },
            });
        }
        if (ele.id === "test-04") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    setResult(ele, requestConfiguration.body);
                },
            });
        }
        if (ele.id === "test-05") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    var body = JSON.parse(requestConfiguration.body);
                    setResult(ele, body.test && body.test === "swap");
                },
            });
        }
        if (ele.id === "test-06") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    setResult(ele, requestConfiguration.headers.has("rx-request"));
                },
            });
        }
        if (ele.id === "test-07") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    setResult(ele, requestConfiguration.abort instanceof Function);
                },
            });
        }
        if (ele.id === "test-08") {
            ele.addRxCallbacks({
                onElementTriggerError: (error) => {
                    setResult(ele, error.message.startsWith(`Element ${ele.id} is already executing a request.`));
                }
            });
        }
        if (ele.id === "test-09") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    requestConfiguration.headers.append("test", "test");
                },
                afterFetch: (requestDetail, response) => {
                    setResult(ele, requestDetail.headers.get("test") !== null && requestDetail.headers.get("test") === response.headers.get("test"));
                }
            });
        }
        if (ele.id === "test-10") {
            ele.addRxCallbacks({
                beforeDocumentUpdate: (mergeElement) => {
                    setResult(ele, mergeElement instanceof HTMLTemplateElement && mergeElement.id === "test-swap-target-fragment");
                }
            });
        }
        if (ele.id === "test-11") {
            ele.addRxCallbacks({
                beforeDocumentUpdate: (_mergeElement, strategy) => {
                    setResult(ele, strategy === "swap")
                }
            });
        }
        if (ele.id === "test-12") {
            ele.addRxCallbacks({
                afterDocumentUpdate: () => {
                    setResult(ele, document.getElementById("test-swap-target").getAttribute("data-merged"));
                }
            });
        }
        if (ele.id === "test-13") {
            ele.addRxCallbacks({
                afterDocumentUpdate: () => {
                    let val = swapTestFn("hello");
                    setResult(ele, val === "hello swap");
                }
            });
        }
        if (ele.id === "test-14") {
            ele.addRxCallbacks({
                beforeDocumentUpdate: (mergeElement) => {
                    setResult(ele, mergeElement instanceof HTMLTemplateElement && mergeElement.id === "test-morph-target-fragment");
                }
            });
        }
        if (ele.id === "test-15") {
            ele.addRxCallbacks({
                beforeDocumentUpdate: (_mergeElement, strategy) => {
                    setResult(ele, strategy === "morph")
                }
            });
        }
        if (ele.id === "test-16") {
            ele.addRxCallbacks({
                afterDocumentUpdate: () => {
                    setResult(ele, document.getElementById("test-morph-target").getAttribute("data-merged"));
                }
            });
        }
        if (ele.id === "test-17") {
            ele.addRxCallbacks({
                afterDocumentUpdate: () => {
                    let val = morphTestFn("hello");
                    setResult(ele, val === "hello morph");
                }
            });
        }
        if (ele.id === "test-18") {
            let target = null;
            ele.addRxCallbacks({
                beforeFetch: () => {
                    target = document.getElementById("test-remove-target");
                },
                afterDocumentUpdate: () => {  
                    if (target === null) {
                        setResult(ele, false);
                    } else {
                        setResult(ele, document.getElementById("test-remove-target") === null);
                        document.getElementById("targets").appendChild(target);
                    }
                }
            });
        }
        if (ele.id === "test-19") {
            let target = null;
            ele.addRxCallbacks({
                beforeFetch: () => {
                    target = document.getElementById("test-remove-target");
                },
                afterDocumentUpdate: () => {  
                    if (target === null) {
                        setResult(ele, false);
                    } else {
                        let swap = document.getElementById("test-swap-target").getAttribute("data-merged");
                        let morph = document.getElementById("test-morph-target").getAttribute("data-merged");
                        setResult(ele, swap && morph && document.getElementById("test-remove-target") === null);
                        document.getElementById("targets").appendChild(target);
                    }
                }
            });
        }
        if (ele.id === "test-20") {
            ele.addRxCallbacks({
                afterDocumentUpdate: () => {  
                    let newTrigger = document.getElementById("new-swap-target-with-trigger");
                    if (!newTrigger) {
                        setResult(ele, false);
                        return;
                    }
                    newTrigger.addRxCallbacks({
                        afterDocumentUpdate: () => {
                            newTrigger = document.getElementById("new-swap-target-with-trigger");
                            if (newTrigger) {
                                setResult(ele, false);
                                return;
                            } else {
                               setResult(ele, true); 
                            }
                        }
                    });
                    newTrigger.click();
                }
            });
        }
        if (ele.id === "test-21") {
            ele.addRxCallbacks({
                afterDocumentUpdate: () => {  
                    let newTrigger = document.getElementById("new-morph-target-with-trigger");
                    if (!newTrigger) {
                        setResult(ele, false);
                        return;
                    }
                    newTrigger.addRxCallbacks({
                        afterDocumentUpdate: () => {
                            newTrigger = document.getElementById("new-morph-target-with-trigger");
                            if (newTrigger) {
                                setResult(ele, false);
                                return;
                            } else {
                               setResult(ele, true); 
                            }
                        }
                    });
                    newTrigger.click();
                }
            });
        }
        if (ele.id === "test-22") {
            ele.addRxCallbacks({
                afterDocumentUpdate: () => {  
                    let target = document.getElementById("test-adjacent-target");
                    if (!target) {
                        setResult(ele, false);
                        return;
                    }
                    setResult(ele, target.firstChild.id === "test-adjacent-target-marker" && target.children.length === 4);
                    document.getElementById("test-adjacent-target-1").remove();
                    document.getElementById("test-adjacent-target-2").remove();
                    document.getElementById("test-adjacent-target-3").remove();
                }
            });
        }
        if (ele.id === "test-23") {
            let resolved = false;
            ele.addRxCallbacks({
                beforeFetch: () => {
                    if (!ele.hasAttribute("disabled")) {
                        setResult(ele, false);
                        resolved = true;
                    }
                },
                onElementTriggerError: () => {
                    //already invoked request
                    setResult(ele, false);
                    resolved = true;
                },
                afterDocumentUpdate: () => {
                    if (resolved) {
                        return;
                    }
                    if (ele.hasAttribute("disabled")) {
                        setResult(ele, false);
                    } else {
                        setResult(ele, true);
                    }
                }
            });
        }
    }
});

/**
 * 
 * @param {Event} evt 
 */
function customTrigger(evt) {
    reset(evt.target);
    evt.preventDefault();
    var e = new CustomEvent("custom-trigger");
    evt.target.dispatchEvent(e);
}
document.getElementById("test-01").onclick = customTrigger;

/**
 * 
 * @param {Event} evt 
 */
function doubleTrigger(evt) {
    reset(evt.target);
    evt.preventDefault();
    var e = new CustomEvent("double-trigger");
    evt.target.dispatchEvent(e);
    evt.target.dispatchEvent(e);
}
document.getElementById("test-08").onclick = doubleTrigger;

/**
 * 
 * @param {Event} evt 
 */
function debouncedTrigger(evt) {
    reset(evt.target);
    evt.preventDefault();
    var e = new CustomEvent("debounced-trigger");
    evt.target.dispatchEvent(e);
    evt.target.dispatchEvent(e);
    evt.target.dispatchEvent(e);
    evt.target.dispatchEvent(e);
}
document.getElementById("test-23").onclick = debouncedTrigger;

document.getElementById("test-all").onclick = () => {
    triggers.forEach(ele => {
        reset(ele);
    });
    triggers.forEach(ele => {
        ele.click();
    });
};
