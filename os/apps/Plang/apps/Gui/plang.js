(() => {
    function callGoal(goalName, parameters, outputTarget, domOperation) {
        console.log(goalName, parameters, outputTarget, domOperation);
        window.chrome.webview.postMessage({ goalName, parameters, outputTarget, domOperation });
    }
    window.callGoal = callGoal;

    
    function updateContent(content, cssSelector, domOperation) {
        const element = document.querySelector(cssSelector);
        if (!element) {
            console.error(`Element not found: ${cssSelector}`);
            return;
        }

        if (domOperation === 'innerHTML') {
            element.innerHTML = content;
        } else if (domOperation === 'outerHTML') {
            element.outerHTML = content;
        } else if (domOperation === 'append') {
            element.innerHTML += content;
        } else {
            element.innerHTML = content;
        }
    }
    
    window.updateContent = updateContent;
})();

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => document.querySelectorAll(selector);

document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll("*").forEach((element) => {
        Array.from(element.attributes).forEach((attr) => {
            if (attr.name.startsWith("@")) {
                const eventType = attr.name.slice(1); // Remove "@" (e.g., "@click" -> "click")
                const handlerCode = attr.value;

                try {
                    // If handler is a function name, use it
                    if (typeof window[handlerCode] === "function") {
                        element.addEventListener(eventType, window[handlerCode]);
                    } else {
                        // Otherwise, execute it as inline JavaScript
                        element.addEventListener(eventType, function () {
                            eval(handlerCode);
                        });
                    }
                } catch (error) {
                    console.error(`Error binding event '${eventType}':`, error);
                }

                // Remove @event attribute after processing (optional)
                element.removeAttribute(attr.name);
            }
        });
    });
});