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
                    console.log("test-01 beforeFetch");
                    setResult(ele, requestConfiguration.trigger.type === "custom-trigger");
                },
                afterDocumentUpdate: () => {
                    console.log("test-01 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-02") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    console.log("test-02 beforeFetch");
                    setResult(ele, requestConfiguration.method === "POST");
                },
                afterDocumentUpdate: () => {
                    console.log("test-02 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-03") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    console.log("test-03 beforeFetch");
                    setResult(ele, requestConfiguration.action === "/test-swap");
                },
                afterDocumentUpdate: () => {
                    console.log("test-03 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-04") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    console.log("test-04 beforeFetch");
                    setResult(ele, requestConfiguration.body);
                },
                afterDocumentUpdate: () => {
                    console.log("test-04 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-05") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    console.log("test-05 beforeFetch");
                    var body = JSON.parse(requestConfiguration.body);
                    setResult(ele, body.test && body.test === "swap");
                },
                afterDocumentUpdate: () => {
                    console.log("test-05 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-06") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    console.log("test-06 beforeFetch");
                    setResult(ele, requestConfiguration.headers.has("rx-request"));
                },
                afterDocumentUpdate: () => {
                    console.log("test-06 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-07") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    console.log("test-07 beforeFetch");
                    setResult(ele, requestConfiguration.abort instanceof Function);
                },
                afterDocumentUpdate: () => {
                    console.log("test-07 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-08") {
            ele.addRxCallbacks({
                onElementTriggerError: (error) => {
                    console.log("test-08 onElementTriggerError");
                    setResult(ele, error.message.startsWith(`Element ${ele.id} is already executing a request.`));
                }
            });
        }
        if (ele.id === "test-09") {
            ele.addRxCallbacks({
                beforeFetch: (requestConfiguration) => {
                    console.log("test-09 beforeFetch");
                    requestConfiguration.headers.append("test", "test");
                },
                afterFetch: (requestDetail, response) => {
                    setResult(ele, requestDetail.headers.get("test") !== null && requestDetail.headers.get("test") === response.headers.get("test"));
                },
                afterDocumentUpdate: () => {
                    console.log("test-09 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-10") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-10 beforeFetch");
                },
                beforeDocumentUpdate: (mergeElement) => {
                    setResult(ele, mergeElement instanceof HTMLTemplateElement && mergeElement.id === "test-swap-target-fragment");
                },
                afterDocumentUpdate: () => {
                    console.log("test-10 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-11") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-11 beforeFetch");
                },
                beforeDocumentUpdate: (_mergeElement, strategy) => {
                    setResult(ele, strategy === "swap")
                },
                afterDocumentUpdate: () => {
                    console.log("test-11 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-12") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-12 beforeFetch");
                },
                afterDocumentUpdate: () => {
                    console.log("test-12 afterDocumentUpdate");
                    setResult(ele, document.getElementById("test-swap-target").getAttribute("data-merged"));
                }
            });
        }
        if (ele.id === "test-13") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-13 beforeFetch");
                },
                afterDocumentUpdate: () => {
                    console.log("test-13 afterDocumentUpdate");
                    let val = swapTestFn("hello");
                    setResult(ele, val === "hello swap");
                }
            });
        }
        if (ele.id === "test-14") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-14 beforeFetch");
                },
                beforeDocumentUpdate: (mergeElement) => {
                    setResult(ele, mergeElement instanceof HTMLTemplateElement && mergeElement.id === "test-morph-target-fragment");
                },
                afterDocumentUpdate: () => {
                    console.log("test-14 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-15") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-15 beforeFetch");
                },
                beforeDocumentUpdate: (_mergeElement, strategy) => {
                    setResult(ele, strategy === "morph")
                },
                afterDocumentUpdate: () => {
                    console.log("test-15 afterDocumentUpdate");
                }
            });
        }
        if (ele.id === "test-16") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-16 beforeFetch");
                },
                afterDocumentUpdate: () => {
                    console.log("test-16 afterDocumentUpdate");
                    setResult(ele, document.getElementById("test-morph-target").getAttribute("data-merged"));
                }
            });
        }
        if (ele.id === "test-17") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-17 beforeFetch");
                },
                afterDocumentUpdate: () => {
                    console.log("test-17 afterDocumentUpdate");
                    let val = morphTestFn("hello");
                    setResult(ele, val === "hello morph");
                }
            });
        }
        if (ele.id === "test-18") {
            let target = null;
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-18 beforeFetch");
                    target = document.getElementById("test-remove-target");
                },
                afterDocumentUpdate: () => {  
                    console.log("test-18 afterDocumentUpdate");
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
                    console.log("test-19 beforeFetch");
                    target = document.getElementById("test-remove-target");
                },
                afterDocumentUpdate: () => {  
                    console.log("test-19 afterDocumentUpdate");
                    if (target === null) {
                        setResult(ele, false);
                    } else {
                        let swap = document.getElementById("test-swap-target").getAttribute("data-merged");
                        let morph = document.getElementById("test-morph-target").getAttribute("data-merged");
                        let removed = document.getElementById("test-remove-target") === null;
                        if (!(swap && morph && removed)) {
                            console.log(`something's up dawg!`)
                        }
                        setResult(ele, swap && morph && removed);
                        document.getElementById("targets").appendChild(target);
                    }
                }
            });
        }
        if (ele.id === "test-20") {
            ele.addRxCallbacks({
                beforeFetch: () => {
                    console.log("test-20 beforeFetch");
                },
                afterDocumentUpdate: () => {  
                    console.log("test-20 afterDocumentUpdate");
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
                beforeFetch: () => {
                    console.log("test-21 beforeFetch");
                },
                afterDocumentUpdate: () => {  
                    console.log("test-21 afterDocumentUpdate");
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
                beforeFetch: () => {
                    console.log("test-22 beforeFetch");
                },
                afterDocumentUpdate: () => {  
                    console.log("test-22 afterDocumentUpdate");
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
                    console.log("test-23 beforeFetch");
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
                    console.log("test-23 afterDocumentUpdate");
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
    setTimeout(() => {
        evt.target.dispatchEvent(e);
        setTimeout(() => {
            evt.target.value = "2";
            evt.target.dispatchEvent(e);
        }, 500);
    }, 500);
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
