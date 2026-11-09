import type { Plang } from "../Plang";
export declare class Debugger {
    private static readonly DEBUG_ATTR;
    private static readonly DEBUG_CLASS;
    private isAltPressed;
    private tooltip;
    private stylesInjected;
    private currentDebugElement;
    private hideTimeout;
    private plang;
    constructor(plang: Plang);
    private init;
    sendMessage(json: string): Promise<void>;
    private injectStyles;
    private setupKeyboardListeners;
    private setupMouseListeners;
    private showDebugOverlays;
    private hideDebugOverlays;
    private formatElapsed;
    private showTooltip;
    private positionTooltip;
    private hideTooltip;
    /**
     * Mark an element with debug information
     */
    markElement(element: HTMLElement, debugInfo: any): void;
    /**
     * Mark only the first child element with debug information
     */
    markElementAndChildren(element: HTMLElement, debugInfo: any): void;
    /**
     * Process a message and mark its element if debug info exists
     */
    processMessage(message: any): void;
    /**
     * Clean up event listeners and tooltip
     */
    destroy(): void;
}
