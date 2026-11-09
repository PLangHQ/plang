import { errorReporter } from "../net/ErrorReporter.js";
export class Debugger {
    constructor(plang) {
        this.isAltPressed = false;
        this.tooltip = null;
        this.stylesInjected = false;
        this.currentDebugElement = null;
        this.hideTimeout = null;
        this.init();
        this.plang = plang;
    }
    init() {
        this.injectStyles();
        this.setupKeyboardListeners();
        this.setupMouseListeners();
    }
    async sendMessage(json) {
        const handleCtx = {
            isOk: true,
            options: {},
            init: {},
            headers: {},
            url: '/'
        };
        this.plang.handleResponse(json, handleCtx);
    }
    injectStyles() {
        if (this.stylesInjected || document.getElementById('debug-styles')) {
            return;
        }
        const style = document.createElement('style');
        style.id = 'debug-styles';
        style.textContent = `
      .${Debugger.DEBUG_CLASS} {
        position: relative;
        --debug-border-color: #ff6b6b;
      }
      
      .${Debugger.DEBUG_CLASS}.show-debug::after {
        content: '';
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        border: 2px solid var(--debug-border-color);
        pointer-events: none;
        z-index: 999801;
      }
      
      .${Debugger.DEBUG_CLASS}.debug-warning.show-debug::after {
        border-color: #ff6b6b;
        box-shadow: 0 0 8px rgba(255, 107, 107, 0.5);
      }
      
      .debug-tooltip {
        position: fixed;
        background: rgba(0, 0, 0, 0.9);
        color: #fff;
        padding: 8px 12px;
        border-radius: 4px;
        font-family: monospace;
        font-size: 12px;
        z-index: 999901;
        pointer-events: auto;
        white-space: pre-wrap;
        max-width: 400px;
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
      }
      
      .debug-tooltip a {
        color: #4fc3f7;
        text-decoration: underline;
      }
      
      .debug-tooltip a:hover {
        color: #81d4fa;
      }
    `;
        document.head.appendChild(style);
        this.stylesInjected = true;
    }
    setupKeyboardListeners() {
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Alt' && !this.isAltPressed) {
                this.isAltPressed = true;
                this.showDebugOverlays();
            }
        });
        document.addEventListener('keyup', (e) => {
            if (e.key === 'Alt' && this.isAltPressed) {
                this.isAltPressed = false;
                this.hideDebugOverlays();
            }
        });
        // Handle window blur (Alt+Tab, etc.)
        window.addEventListener('blur', () => {
            if (this.isAltPressed) {
                this.isAltPressed = false;
                this.hideDebugOverlays();
            }
        });
        // Fallback: check actual Alt key state periodically when mouse moves
        document.addEventListener('mousemove', (e) => {
            if (this.isAltPressed && !e.altKey) {
                this.isAltPressed = false;
                this.hideDebugOverlays();
            }
            else if (!this.isAltPressed && e.altKey) {
                this.isAltPressed = true;
                this.showDebugOverlays();
            }
        });
    }
    setupMouseListeners() {
        document.addEventListener('mouseover', (e) => {
            if (!this.isAltPressed)
                return;
            const target = e.target;
            // Clear any pending hide timeout
            if (this.hideTimeout) {
                clearTimeout(this.hideTimeout);
                this.hideTimeout = null;
            }
            // Don't change tooltip when hovering over it
            if (this.tooltip && this.tooltip.contains(target)) {
                return;
            }
            const debugElement = target.closest(`.${Debugger.DEBUG_CLASS}`);
            if (debugElement && debugElement !== this.currentDebugElement) {
                this.currentDebugElement = debugElement;
                this.showTooltip(debugElement, e);
            }
        });
        document.addEventListener('mouseout', (e) => {
            if (!this.isAltPressed)
                return;
            const target = e.target;
            const relatedTarget = e.relatedTarget;
            // Don't hide if moving to tooltip
            if (this.tooltip && relatedTarget && (this.tooltip.contains(relatedTarget) || this.tooltip === relatedTarget)) {
                return;
            }
            const debugElement = target.closest(`.${Debugger.DEBUG_CLASS}`);
            if (debugElement) {
                // Clear any existing timeout
                if (this.hideTimeout) {
                    clearTimeout(this.hideTimeout);
                }
                // Delay hiding to allow moving to tooltip
                this.hideTimeout = window.setTimeout(() => {
                    if (this.tooltip && !this.tooltip.matches(':hover')) {
                        this.hideTooltip();
                        this.currentDebugElement = null;
                    }
                    this.hideTimeout = null;
                }, 100);
            }
        });
    }
    showDebugOverlays() {
        const debugElements = document.querySelectorAll(`.${Debugger.DEBUG_CLASS}`);
        debugElements.forEach(el => {
            el.classList.add('show-debug');
            const debugInfo = el.getAttribute(Debugger.DEBUG_ATTR);
            if (debugInfo) {
                try {
                    const data = JSON.parse(debugInfo);
                    const ms = data.step?.Stopwatch?.ElapsedMilliseconds ?? 0;
                    if (ms > 50) {
                        el.classList.add('debug-warning');
                    }
                }
                catch (e) {
                    errorReporter.reportCaught("debugAttr", e);
                }
            }
        });
    }
    hideDebugOverlays() {
        const debugElements = document.querySelectorAll('.show-debug');
        debugElements.forEach(el => {
            el.classList.remove('show-debug');
            el.classList.remove('debug-warning');
        });
        this.hideTooltip();
        this.currentDebugElement = null;
    }
    formatElapsed(ms) {
        if (ms < 1000)
            return `${ms}ms`;
        if (ms < 60000)
            return `${(ms / 1000).toFixed(2)}s`;
        const mins = Math.floor(ms / 60000);
        const secs = ((ms % 60000) / 1000).toFixed(1);
        return `${mins}m ${secs}s`;
    }
    showTooltip(element, e) {
        const debugInfo = element.getAttribute(Debugger.DEBUG_ATTR);
        if (!debugInfo)
            return;
        if (!this.tooltip) {
            this.tooltip = document.createElement('div');
            this.tooltip.className = 'debug-tooltip';
            document.body.appendChild(this.tooltip);
        }
        try {
            const data = JSON.parse(debugInfo);
            const ms = data.step?.Stopwatch?.ElapsedMilliseconds ?? 0;
            const isWarning = ms > 50;
            const elapsed = this.formatElapsed(ms);
            const timeStyle = isWarning ? 'color: #ff6b6b; font-weight: bold;' : 'color: #4fc3f7;';
            const warningIcon = isWarning ? ' ⚠️' : '';
            let html = `<span style="${timeStyle}">${elapsed}${warningIcon}</span>
Goal: ${data.goal.name}
Step: ${data.step.text}
File: <a href="vscode://file/${data.goal.absolutePath.replace(/\\/g, '/')}:${data.step.line}" target="_blank">${data.goal.path}:${data.step.line}</a>`;
            this.tooltip.innerHTML = html;
            if (isWarning) {
                element.style.setProperty('--debug-border-color', '#ff6b6b');
            }
        }
        catch (err) {
            this.tooltip.textContent = debugInfo;
            errorReporter.reportCaught("debugTooltip", err);
        }
        this.positionTooltip(e);
        this.tooltip.style.display = 'block';
    }
    positionTooltip(e) {
        if (!this.tooltip)
            return;
        const offset = 10;
        let x = e.clientX + offset;
        let y = e.clientY + offset;
        // Prevent tooltip from going off-screen
        const rect = this.tooltip.getBoundingClientRect();
        if (x + rect.width > window.innerWidth) {
            x = e.clientX - rect.width - offset;
        }
        if (y + rect.height > window.innerHeight) {
            y = e.clientY - rect.height - offset;
        }
        this.tooltip.style.left = `${x}px`;
        this.tooltip.style.top = `${y}px`;
    }
    hideTooltip() {
        if (this.tooltip) {
            this.tooltip.style.display = 'none';
        }
    }
    /**
     * Mark an element with debug information
     */
    markElement(element, debugInfo) {
        element.setAttribute(Debugger.DEBUG_ATTR, JSON.stringify(debugInfo));
        element.classList.add(Debugger.DEBUG_CLASS);
    }
    /**
     * Mark only the first child element with debug information
     */
    markElementAndChildren(element, debugInfo) {
        this.markElement(element, debugInfo);
        /*
                for (let i = 0; i < element.childNodes.length; i++) {
                    if (element.childNodes[i].nodeType === Node.ELEMENT_NODE) {
                        this.markElement(element.childNodes[i] as HTMLElement, debugInfo);
                        break; // Only mark the first one
                    }
                }*/
    }
    /**
     * Process a message and mark its element if debug info exists
     */
    processMessage(message) {
        if (message.dom?.html && message.debug) {
            this.markElementAndChildren(message.dom.html, message.debug);
        }
    }
    /**
     * Clean up event listeners and tooltip
     */
    destroy() {
        if (this.hideTimeout) {
            clearTimeout(this.hideTimeout);
            this.hideTimeout = null;
        }
        if (this.tooltip && this.tooltip.parentNode) {
            this.tooltip.parentNode.removeChild(this.tooltip);
            this.tooltip = null;
        }
        this.isAltPressed = false;
    }
}
Debugger.DEBUG_ATTR = 'data-debug-info';
Debugger.DEBUG_CLASS = 'debug-enabled';
//# sourceMappingURL=Debugger.js.map