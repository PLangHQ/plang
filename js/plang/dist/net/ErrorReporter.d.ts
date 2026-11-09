/** Whose code the stack points at. Browser extensions and injected content scripts report
 *  through window.onerror exactly like our own, and only the stack tells them apart. */
export type JsErrOrigin = "ours" | "injected" | "unknown";
export type JsErr = {
    type: string;
    message: string;
    stack?: string;
    href: string;
    count: number;
    origin: JsErrOrigin;
};
export declare class ErrorReporter {
    private seen;
    private pending;
    private sending;
    private flushTimer;
    private capped;
    private transportDown;
    private unloading;
    constructor();
    private static readonly ignored;
    private static readonly networkNoise;
    private static readonly networkPaths;
    private static isExpectedNetworkNoise;
    /**
     * A DOMException carries no stack at all in Chrome. That is the whole IndexedDB,
     * storage, crypto and quota family, which is exactly the family that breaks on locked
     * down machines, so the reports that matter most arrived with nothing to act on.
     * When the error brings no stack, capture one here. It shows where we caught it rather
     * than where it was thrown, which is still enough to find the code.
     */
    private static captureSite;
    private static readonly injectedStack;
    /**
     * Classified on the stack the browser gave us, before captureSite() substitutes one:
     * a synthetic stack is always ours and would make every foreign error look like ours.
     *
     * A frame in our own files wins over every foreign marker. Mislabelling noise costs a
     * click, mislabelling a real fault hides it.
     */
    private static classify;
    /** Record an error and send it. Safe to call from anywhere, including a catch block. */
    report(type: string, message: string, stack?: string): void;
    /**
     * Convenience for catch blocks, which hold an unknown rather than an Error.
     *
     * The same failure usually passes through several layers on its way up: a poll failure
     * is also a fetch failure. Each layer reports, so one fault produced one row per layer
     * and the table read as several unrelated problems. Tagging the error object means the
     * innermost layer wins, which is the one with the useful context.
     */
    reportCaught(type: string, err: unknown): void;
    /** Errors buffered by the inline snippet before this module existed. */
    drainPreBoot(items: unknown): void;
    private scheduleFlush;
    private flush;
    private send;
    /** Offline or endpoint unreachable: keep it and try again on the next page load. */
    private queue;
    private drainQueue;
}
export declare const errorReporter: ErrorReporter;
