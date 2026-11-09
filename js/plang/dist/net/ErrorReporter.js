const ENDPOINT = "/js-error";
const MAX_DISTINCT_PER_PAGE = 20;
const COALESCE_MS = 5000;
const QUEUE_KEY = "plang.jsErrorQueue";
const MAX_QUEUED = 20;
// Errors are sent the moment they are recorded, on their own request.
//
// They used to be buffered and attached as an X-Js-Error header to the next outgoing
// request. That loses every error whose own failure stops further traffic, which is the
// only class that matters: a browser where the poll cannot start reports nothing, shows a
// spinner forever, and we never hear about it. Identity creation failing is exactly that
// shape, and it is why teachers reported a broken invite link while our errors table stayed
// empty.
//
// Rules this has to keep:
//   - never use plang.fetch, or a failure inside the reporter re-enters what it is reporting
//   - never report a failure of the reporter itself, or one broken endpoint becomes a loop
//   - first sighting of a message goes immediately, repeats are coalesced, because the poll
//     retries on a timer and would otherwise send forever
export class ErrorReporter {
    constructor() {
        this.seen = new Map();
        this.pending = new Map();
        this.sending = false;
        this.flushTimer = null;
        this.capped = false;
        // Set the first time a send fails. After that we only queue for the next page load.
        // Without it a dead endpoint plus a retrying poll means a POST every coalesce window,
        // forever, which is the sort of thing that turns one outage into two.
        this.transportDown = false;
        // The page is going away. Leaving the site aborts the long lived poll, and the browser
        // surfaces that as a network error like any other. Without this guard every ordinary
        // navigation and every closed tab logs a failure, which buries the real ones.
        this.unloading = false;
        addEventListener("pagehide", () => {
            this.flush();
            this.unloading = true;
        });
        addEventListener("beforeunload", () => {
            this.flush();
            this.unloading = true;
        });
        // Hidden is a tab switch, not a teardown, so flush but keep listening.
        addEventListener("visibilitychange", () => {
            if (document.visibilityState === "hidden")
                this.flush();
        });
        this.drainQueue();
    }
    static isExpectedNetworkNoise(type, message) {
        if (!ErrorReporter.networkPaths.has(type))
            return false;
        const m = message.toLowerCase();
        return ErrorReporter.networkNoise.some(x => m.includes(x));
    }
    /**
     * A DOMException carries no stack at all in Chrome. That is the whole IndexedDB,
     * storage, crypto and quota family, which is exactly the family that breaks on locked
     * down machines, so the reports that matter most arrived with nothing to act on.
     * When the error brings no stack, capture one here. It shows where we caught it rather
     * than where it was thrown, which is still enough to find the code.
     */
    static captureSite() {
        const s = new Error("captured at report site").stack;
        if (!s)
            return "";
        // Drop the frames belonging to the reporter itself, they say nothing useful.
        return s.split("\n").filter(l => !l.includes("ErrorReporter")).join("\n");
    }
    /**
     * Classified on the stack the browser gave us, before captureSite() substitutes one:
     * a synthetic stack is always ours and would make every foreign error look like ours.
     *
     * A frame in our own files wins over every foreign marker. Mislabelling noise costs a
     * click, mislabelling a real fault hides it.
     */
    static classify(message, stack) {
        const ours = `${location.origin}/js/`;
        // The browser sanitises a cross origin script down to this and strips the stack, so
        // it cannot have come from a file of ours: those are same origin and report in full.
        if (!stack)
            return message.startsWith("Script error.") ? "injected" : "unknown";
        if (stack.includes(ours))
            return "ours";
        if (ErrorReporter.injectedStack.test(stack))
            return "injected";
        if (stack.includes(`@${location.origin}/`) || stack.includes(`(${location.origin}/`))
            return "injected";
        return "unknown";
    }
    /** Record an error and send it. Safe to call from anywhere, including a catch block. */
    report(type, message, stack) {
        if (this.sending || this.unloading)
            return;
        if (!message)
            message = String(type);
        if (ErrorReporter.ignored.some(x => message.includes(x)))
            return;
        if (ErrorReporter.isExpectedNetworkNoise(type, message))
            return;
        // A bare "Script error." with no stack is only ever a cross origin script, never ours.
        if (!stack && message.startsWith("Script error."))
            return;
        const origin = ErrorReporter.classify(message, stack);
        if (!stack)
            stack = ErrorReporter.captureSite();
        const key = `${type}|${message}`;
        const existing = this.seen.get(key);
        if (existing) {
            existing.count++;
            this.pending.set(key, existing);
            this.scheduleFlush();
            return;
        }
        if (this.seen.size >= MAX_DISTINCT_PER_PAGE) {
            if (!this.capped) {
                this.capped = true;
                this.send([{
                        type: "reporter",
                        message: `js error cap reached (${MAX_DISTINCT_PER_PAGE} distinct)`,
                        href: location.href,
                        count: 1,
                        origin: "ours",
                    }]);
            }
            return;
        }
        const err = { type, message, stack, href: location.href, count: 1, origin };
        this.seen.set(key, err);
        this.send([{ ...err }]);
    }
    /**
     * Convenience for catch blocks, which hold an unknown rather than an Error.
     *
     * The same failure usually passes through several layers on its way up: a poll failure
     * is also a fetch failure. Each layer reports, so one fault produced one row per layer
     * and the table read as several unrelated problems. Tagging the error object means the
     * innermost layer wins, which is the one with the useful context.
     */
    reportCaught(type, err) {
        const e = err;
        if (e && typeof e === "object") {
            if (e.__plangReported)
                return;
            try {
                e.__plangReported = true;
            }
            catch { /* frozen error, report it anyway */ }
        }
        // "The operation is insecure" does not say it is a SecurityError, and the class name
        // is what identifies the family. DOMException has no stack, so this is often the only
        // clue about what kind of failure it was.
        const name = e?.name && e.name !== "Error" ? `${e.name}: ` : "";
        this.report(type, name + (e?.message ?? String(err)), e?.stack);
    }
    /** Errors buffered by the inline snippet before this module existed. */
    drainPreBoot(items) {
        if (!Array.isArray(items))
            return;
        for (const i of items) {
            const e = i;
            if (e && typeof e.message === "string") {
                this.report(String(e.type ?? "error"), e.message, e.stack);
            }
        }
    }
    scheduleFlush() {
        if (this.flushTimer)
            return;
        this.flushTimer = setTimeout(() => {
            this.flushTimer = null;
            this.flush();
        }, COALESCE_MS);
    }
    flush() {
        if (this.flushTimer) {
            clearTimeout(this.flushTimer);
            this.flushTimer = null;
        }
        if (!this.pending.size)
            return;
        const batch = [...this.pending.values()].map(e => ({ ...e }));
        this.pending.clear();
        this.send(batch);
    }
    send(batch) {
        if (!batch.length)
            return;
        if (this.transportDown) {
            this.queue(batch);
            return;
        }
        // The guard is what stops a failure inside the reporter becoming a loop: anything
        // thrown or logged while we are inside send() is dropped by report() above.
        this.sending = true;
        try {
            const body = JSON.stringify({ errors: batch, path: location.pathname });
            // Raw fetch on purpose. plang.fetch signs the request, and signing is one of the
            // things that can be broken when we most need to report. keepalive lets the
            // request outlive the page, which is what makes an error on the way out survive.
            void fetch(ENDPOINT, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body,
                keepalive: true,
                credentials: "same-origin",
            }).catch(() => {
                this.transportDown = true;
                this.queue(batch);
            });
        }
        catch {
            this.transportDown = true;
            this.queue(batch);
        }
        finally {
            this.sending = false;
        }
    }
    /** Offline or endpoint unreachable: keep it and try again on the next page load. */
    queue(batch) {
        try {
            const raw = sessionStorage.getItem(QUEUE_KEY);
            const existing = raw ? JSON.parse(raw) : [];
            const merged = [...existing, ...batch].slice(-MAX_QUEUED);
            sessionStorage.setItem(QUEUE_KEY, JSON.stringify(merged));
        }
        catch {
            // sessionStorage can be unavailable in exactly the locked down browsers this
            // code exists for. Losing a queued copy is acceptable; looping is not.
        }
    }
    drainQueue() {
        let batch = [];
        try {
            const raw = sessionStorage.getItem(QUEUE_KEY);
            if (!raw)
                return;
            sessionStorage.removeItem(QUEUE_KEY);
            batch = JSON.parse(raw);
        }
        catch {
            return;
        }
        if (Array.isArray(batch) && batch.length)
            this.send(batch);
    }
}
// Reported conditions that are not faults. They are dropped here rather than server side
// so they never cost a request, and because the same list has to hold for the inline
// pre-boot snippet in the layout, which sends on its own when this module never loads.
//
// ResizeObserver loop: the browser finishes the work on the next frame. Nothing is broken.
// signing key fell back: the key lives in localStorage or memory instead of IndexedDB.
//   The page works, the visitor is merely worse off, and it is recorded where it belongs,
//   on the identity (identities.keyStore, written from the poll). Headless crawlers have
//   no IndexedDB and produced these in bulk, which buried real errors.
// The last two are chatter from injected scripts: mail security scanners and extensions.
ErrorReporter.ignored = [
    "ResizeObserver loop",
    "signing key fell back",
    "Object Not Found Matching Id",
    "No Listener:",
];
// Network-class failures on our own request layer are expected and unactionable, so they
// are dropped rather than recorded.
//
// The poll is a long lived request. Leaving a page, closing a tab, sleeping a laptop or
// losing wifi all abort it, and the browser reports that as an ordinary network failure.
// In the first 30 minutes on production these were 19 of 26 rows, 73% of everything, and
// an error table nobody can read is the exact problem this reporter exists to solve.
//
// An unloading guard was tried first and is not enough: real visitors leave pages in ways
// a headless test does not, and Safari orders pagehide differently, which is why it
// contributes "Load failed" where Chrome contributes "network error".
//
// This deliberately gives up on spotting a poll endpoint that is down. That failure is
// far more visible server side, as a traffic collapse, than it ever was here. What must
// NOT be dropped is a non-network failure on the same path: a browser that cannot create
// an identity reports SecurityError, not a network error, so it still comes through.
ErrorReporter.networkNoise = [
    "network error",
    "load failed",
    "failed to fetch",
    "the network connection was lost",
    "the operation was aborted",
    "aborterror",
    "cancelled",
];
// ONLY the poll. Deliberately not "fetch": a network failure on a request the visitor
// actually asked for, adding to the cart, searching, navigating, is a real failed action
// and has to be reported. Only the always-open poll produces the aborts that are noise.
ErrorReporter.networkPaths = new Set(["poll"]);
// Anything an extension injects runs with the page as its source, so its frames carry the
// document URL, an extension scheme or a masked URL, never a file we serve.
ErrorReporter.injectedStack = /-extension:\/\/|webkit-masked-url/;
// A module level singleton, deliberately.
//
// The first version reached the reporter through window.plang, which is only assigned after
// the Plang constructor returns. Anything constructed inside that constructor, including the
// debug classes, therefore reported nothing. The IndexedDB failure lives in exactly one of
// those, and it is the earliest signal we have that a browser cannot create an identity, so
// losing it defeats the purpose. Importing the instance removes the ordering question.
export const errorReporter = new ErrorReporter();
//# sourceMappingURL=ErrorReporter.js.map