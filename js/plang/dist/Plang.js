import { HistoryNavigator } from "./navigation/HistoryNavigator.js";
import { OverrideNavigator } from "./navigation/OverrideNavigator.js";
import { PollRequest } from "./net/PollRequest.js";
import { registerNavigationSigner } from "./net/NavigationSigner.js";
import { ECDSASigner } from "./crypto/ECDSASigner.js";
import { t as defaultT } from "./i18n/index.js";
import { ResponseDispatcher } from "./net/ResponseDispatcher.js";
import { RenderHandler } from "./handlers/RenderHandler.js";
import { TextHandler } from "./handlers/TextHandler.js";
import { ErrorHandler } from "./handlers/ErrorHandler.js";
import { ExecuteHandler } from "./handlers/ExecuteHandler.js";
import { AskHandler } from "./handlers/AskHandler.js";
import { StreamHandler } from "./handlers/StreamHandler.js";
import { UtilHelper } from "./util/UtilHelper.js";
import { FrameworkRegistry } from "./actions/ActionOverrides.js";
import { LogNotificationManager } from "./channels/log/LogNotificationManager.js";
import { Debugger } from "./debug/Debugger.js";
import { DebugBar } from "./debug/DebugBar.js";
import { errorReporter } from "./net/ErrorReporter.js";
// How long boot may take before the page is told it failed. Only bounds the wait for the first
// poll response; a later connect still promotes the page to ready.
const BOOT_TIMEOUT_MS = 15_000;
export class Plang {
    constructor(tr = defaultT) {
        this.pollRequest = null;
        this.ready = false;
        this.failed = false;
        this.bootTimer = null;
        this.signer = new ECDSASigner();
        this.callback = new Map();
        this.errors = errorReporter;
        this.actionHandlers = [];
        this.kindHandlers = [];
        this.windowStack = [];
        /** Where the signing key ended up. Sent on the first poll so the server can record it. */
        this.keyStoreTier = null;
        // The server signed a form's callback again after it took an answer (`plang.renewCallback`), so a form that stays on
        // the page can be sent again. The form holds the old hash; it gets the new one. An arrow function: an execute
        // message calls it with the handler as `this`.
        this.renewCallback = (callback) => {
            if (!callback?.hash)
                return;
            this.callback.set(callback.hash, callback);
            if (!callback.previousHash)
                return;
            this.callback.delete(callback.previousHash);
            const elements = document.querySelectorAll(`input[name="__plang_callback_hash"][value="${callback.previousHash}"]`);
            for (let i = 0; i < elements.length; i++) {
                elements[i].value = callback.hash;
            }
        };
        this.tr = tr;
        errorReporter.useSigner(this.signer);
        this.navigator = new HistoryNavigator(this, tr);
        this.overrideNav = new OverrideNavigator(this);
        this.responses = new ResponseDispatcher(this.tr);
        this.debugger = new Debugger(this);
        this.debugBar = new DebugBar();
        const renderHandler = new RenderHandler(this);
        const textHandler = new TextHandler(this);
        const errorHandler = new ErrorHandler(this);
        const askHandler = new AskHandler(this);
        this.responses
            .register(renderHandler)
            .register(textHandler)
            .register(errorHandler)
            .register(new ExecuteHandler(this))
            .register(askHandler)
            .register(new StreamHandler());
        [renderHandler, textHandler, errorHandler, askHandler].forEach(h => {
            if ('actionHandler' in h) {
                this.actionHandlers.push(h.actionHandler);
            }
        });
        this.kindHandlers.push(renderHandler, textHandler, errorHandler, askHandler);
        // reportCaught, not stackError: it carries the already-reported tag.
        addEventListener("error", (e) => {
            if (e.error)
                this.errors.reportCaught("error", e.error);
            else
                this.stackError("error", e.message, `${e.filename}:${e.lineno}:${e.colno}`);
        });
        addEventListener("unhandledrejection", (e) => {
            if (e.reason)
                this.errors.reportCaught("unhandledrejection", e.reason);
            else
                this.stackError("unhandledrejection", String(e.reason));
        });
        // Errors the inline snippet in the layout caught before this module was parsed.
        this.errors.drainPreBoot(window.__plangJsErrors);
        window.__plangJsErrors = null;
        this.useFramework();
    }
    /**
     * Report a JS error. Call this from any catch block; a swallowed failure is one we
     * cannot diagnose, and console.warn/console.error reach nobody.
     */
    stackError(type, message, stack) {
        this.errors.report(type, message, stack);
    }
    /** Convenience for catch blocks that hold an unknown. */
    reportCaught(type, err) {
        this.errors.reportCaught(type, err);
    }
    async MyIdentity() {
        return this.signer.MyIdentity();
    }
    async logout() {
        await this.signer.deleteDb();
        location.href = "/";
    }
    async init() {
        // Resolve storage BEFORE the poll opens. The poll sends the tier on its first
        // connect, and the tier is only known once the store has been probed, so starting
        // the poll first would always report nothing.
        try {
            this.keyStoreTier = await this.signer.storeTier();
        }
        catch (e) {
            this.reportCaught("keyStoreTier", e);
        }
        void registerNavigationSigner(this.keyStoreTier, (type, err) => this.reportCaught(type, err));
        // plang:ready used to be dispatched at the end of init(), one line after the poll was
        // constructed and while its first request was still in flight. It therefore promised
        // nothing about being able to reach the server, yet every listener calls callGoal or
        // post the moment it fires: teacher/templates/landing.html, js/rafbokin/plang-init.ts
        // and cart/payment/Straumur_Complete.html. It now fires from fetch(), the moment a
        // request has actually been answered, which is what all three already assume.
        this.bootTimer = setTimeout(() => {
            this.bootTimer = null;
            if (this.ready)
                return;
            this.announceFailed("poll");
        }, BOOT_TIMEOUT_MS);
        this.pollRequest = new PollRequest("/?plang.poll=1", {
            onMessage: async (line, ctx) => {
                try {
                    if (line.startsWith('\"ping\"'))
                        return;
                    const msg = JSON.parse(line);
                    // should call a handler here
                    await this.responses.handle(msg, ctx);
                }
                catch (e) {
                    if (line !== "ping") {
                        console.warn(this.tr("badLine"), line);
                        this.reportCaught("pollMessage", e);
                    }
                }
            },
        });
        const logManager = new LogNotificationManager();
        this.registerChannelOverrides({
            log: (message, ctx) => {
                logManager.addLogMessage(message);
            },
            debug: (message, ctx) => {
                this.debugBar.handleMessage(message);
            }
        });
    }
    /**
     * A request has been answered, so requests work. Fired once, from fetch().
     *
     * Not from PollRequest: fetch() consumes the whole stream itself and only returns when the
     * poll cycle ends, so awaiting it there means ready would fire minutes late or never. The
     * moment window.fetch resolves is the moment a signed round trip is proven, whatever the
     * status code: a 500 still means the transport works, which is what callers need to know.
     *
     * A late connect still counts. A slow network that recovers after the boot timeout gets a
     * working page, and the earlier plang:failed stands as the record that it was slow.
     */
    announceReady() {
        if (this.ready)
            return;
        this.ready = true;
        if (this.bootTimer) {
            clearTimeout(this.bootTimer);
            this.bootTimer = null;
        }
        window.dispatchEvent(new CustomEvent('plang:ready', { detail: this }));
    }
    /** Boot did not get as far as a working connection. Fired once. */
    announceFailed(reason) {
        if (this.failed)
            return;
        this.failed = true;
        this.errors.report("boot", `plang boot failed: ${reason}`);
        window.dispatchEvent(new CustomEvent('plang:failed', { detail: { reason } }));
    }
    async fetch(url, options) {
        try {
            const headers = new Headers();
            headers.set("Accept", "application/plang+json");
            headers.set("X-Requested-With", "XMLHttpRequest");
            if (!options) {
                options = { method: 'GET' };
            }
            if (options.body && !(options.body instanceof FormData)) {
                headers.set("Content-Type", "application/json");
            }
            options.headers?.forEach((a) => headers.set(a.key, a.value));
            let plangHash = this.getPlangHash(options.body);
            if (plangHash != null && this.callback.has(plangHash)) {
                headers.set("X-Callback", btoa(JSON.stringify(this.callback.get(plangHash))));
                this.callback.delete(plangHash);
            }
            // Errors used to ride out on this header. They now go straight to /js-error the
            // moment they happen, because piggybacking loses every error that stops the next
            // request from ever being made.
            let requestInit = {
                body: (options.method == 'POST') ? options.body : null,
                headers: headers,
                method: options.method ?? 'GET'
            };
            let sent = await this.signAndSend(url, requestInit);
            let res = sent.res;
            // A signature lives five minutes. A phone that holds a request longer than that
            // (locked screen, a stalled connection the browser replays) delivers it expired, or
            // replays it with a nonce the server has already seen; a device whose clock is wrong
            // signs it expired from the start. The server checks the signature before running
            // anything, so nothing has happened yet and one resend with a fresh signature is safe,
            // also for a POST. It is sent only when it can be answered differently.
            if ((res.status === 401 || res.status === 403) && await this.isStaleSignature(res)) {
                const retry = await this.signAgain(url, requestInit, sent.signature, res);
                if (retry)
                    res = retry;
            }
            this.announceReady();
            if (UtilHelper.isDownloadResponse(res)) {
                await UtilHelper.doDownloadResponse(res);
                return;
            }
            const handleCtx = {
                isOk: res.ok,
                options: options,
                init: requestInit,
                headers: headers,
                url: url
            };
            // A failed request with no message in its body (a 404 for a path with no route, a 502 from a proxy) still
            // fails: onFailure gets an error made from the status. It got nothing and the page did nothing.
            let handled = 0;
            const failWithoutMessage = async () => {
                if (res.ok || handled > 0)
                    return;
                const message = { kind: "error", status: res.status, key: "HttpError", content: `${res.status} ${res.statusText}`.trim() };
                await this.handleResponse(JSON.stringify(message), handleCtx);
            };
            const body = res.body;
            if (!body) {
                await failWithoutMessage();
                return null;
            }
            const reader = body.getReader();
            const decoder = new TextDecoder();
            let buffer = "";
            while (true) {
                const { value, done } = await reader.read();
                if (done)
                    break;
                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split("\n");
                buffer = lines.pop() ?? "";
                for (const line of lines) {
                    if (!line.trim() || line.startsWith('\"ping\"'))
                        continue;
                    handled++;
                    await this.handleResponse(line, handleCtx);
                }
            }
            if (buffer.trim()) {
                handled++;
                await this.handleResponse(buffer, handleCtx);
            }
            await failWithoutMessage();
            return null;
        }
        catch (e) {
            // Callers of fetch() mostly log and move on, so without this a failed request
            // leaves no trace anywhere. Report, then rethrow so behaviour is unchanged.
            //
            // Tagged by WHICH request failed, because that decides whether a network level
            // failure is worth hearing about. The poll is open essentially all the time, so
            // every navigation and closed tab aborts it and the browser calls that a network
            // error: noise. Any other request is something the visitor actually asked for,
            // so a network failure there is a real failed action and must still be reported.
            this.reportCaught(url.includes("plang.poll") ? "poll" : "fetch", e);
            throw e;
        }
    }
    async signAndSend(url, requestInit) {
        const req = new Request(url, requestInit);
        const signature = await this.signer.signRequest(req);
        req.headers.set("X-Signature", JSON.stringify(signature));
        return { res: await window.fetch(req), signature };
    }
    // Signs the request again after the server refused the first signature as stale, and sends it only when the
    // new signature differs where the first failed: another nonce, and a time the server's clock accepts.
    // Returns null when it would be refused the same way.
    async signAgain(url, requestInit, first, refused) {
        const serverDate = refused.headers.get("Date");
        if (serverDate)
            this.signer.adjustClock(new Date(serverDate));
        const req = new Request(url, requestInit);
        const signature = await this.signer.signRequest(req);
        if (signature.nonce === first.nonce)
            return null;
        if (serverDate && signature.expires && new Date(signature.expires).getTime() <= new Date(serverDate).getTime())
            return null;
        req.headers.set("X-Signature", JSON.stringify(signature));
        return await window.fetch(req);
    }
    // The webserver says why it refused the signature in X-Signature-Error ("expired" or "nonce"), since the body is
    // the app's own error page. A runtime before 0.1.19.72 sends no header; then its error text is looked for.
    async isStaleSignature(res) {
        const reason = res.headers.get("X-Signature-Error");
        if (reason)
            return reason === "expired" || reason === "nonce";
        try {
            const text = await res.clone().text();
            return text.includes("Signature expired") || text.includes("The signature is to old") || text.includes("Nonce has been used");
        }
        catch {
            return false;
        }
    }
    getPlangHash(body) {
        let plangHash = null;
        if (body instanceof FormData) {
            plangHash = body.get('__plang_callback_hash');
        }
        else if (body && typeof body === 'object') {
            plangHash = body.__plang_hash || null;
        }
        return plangHash;
    }
    async handleResponse(line, ctx) {
        let env;
        try {
            env = JSON.parse(line);
            // The server renewed a callback that ran out of its five minutes (or whose nonce was
            // used), so the answer is resent once with the renewed callback, without the user
            // noticing. The resend must carry the new hash: with the old one it sent the expired
            // callback again and the two requests bounced back and forth for ever.
            if (env.key === "CallbackError" && !ctx.options?.__plangResent) {
                let errorMessage = env;
                if (errorMessage.callback) {
                    this.callback.set(errorMessage.callback.hash, errorMessage.callback);
                    const previousHash = errorMessage.callback.previousHash;
                    if (previousHash) {
                        const elements = document.querySelectorAll(`input[name="__plang_callback_hash"][value="${previousHash}"]`);
                        for (let i = 0; i < elements.length; i++) {
                            elements[i].value = errorMessage.callback.hash;
                        }
                    }
                    if (ctx.options?.body instanceof FormData) {
                        ctx.options.body.set('__plang_callback_hash', errorMessage.callback.hash);
                    }
                }
                await this.fetch(ctx.url, { ...ctx.options, __plangResent: true });
                return;
            }
            await this.responses.handle(env, ctx);
        }
        catch (e) {
            console.log(this.tr("badLine"), line, e);
            this.reportCaught("handleResponse", e);
            throw e;
        }
    }
    setCallback(plangOutput) {
        const props = plangOutput?.properties ?? {};
        if (props.path && props.callback)
            this.callback.set(props.path, props.callback);
    }
    async get(path, options) {
        if (!options)
            options = {};
        options.method = 'GET';
        return this.fetch(path, options);
    }
    async post(path, data, options) {
        if (!options)
            options = {};
        const body = data instanceof FormData ? data : JSON.stringify(data);
        options.body = body;
        options.method = 'POST';
        return this.fetch(path, options);
    }
    async callGoal(path, data, callbackOrOptions) {
        const body = data instanceof FormData ? data : JSON.stringify(data);
        // callGoal(path, data, callback) or callGoal(path, data, options). A callback is called with each message the
        // goal answers when the request succeeds (onSuccess). Options are NavigatorOptions (onSuccess, onFailure, ...);
        // an entry whose key starts with p- is sent as a header of that name.
        const isCallback = typeof callbackOrOptions === 'function';
        const options = isCallback ? { onSuccess: callbackOrOptions } : { ...(callbackOrOptions ?? {}) };
        const headers = [];
        for (const [k, v] of Object.entries(options)) {
            if (!k.startsWith('p-'))
                continue;
            headers.push({ key: k, value: String(v) });
            delete options[k];
        }
        return this.fetch(path, { ...options, body, method: "POST", headers: headers });
    }
    async onPoll(_line) {
        /* PollRequest will call onMessage itself after parsing; keep if you want raw handling */
    }
    /**
         * Register a single action override
         */
    registerActionOverride(action, handler) {
        this.actionHandlers.forEach(actionHandler => {
            actionHandler.registerOverride(action, handler);
        });
    }
    /**
     * Register multiple action overrides at once
     */
    registerActionOverrides(overrides) {
        this.actionHandlers.forEach(actionHandler => {
            actionHandler.registerOverrides(overrides);
        });
    }
    registerChannelOverride(channel, handler) {
        this.kindHandlers.forEach(kindHandler => {
            kindHandler.registerChannelOverride(channel, handler);
        });
    }
    registerChannelOverrides(overrides) {
        this.kindHandlers.forEach(kindHandler => {
            kindHandler.registerChannelOverrides(overrides);
        });
    }
    /**
     * Use a built-in framework adapter
     * @param name - Framework name ('uikit', 'bootstrap', 'tailwind') or 'auto' to detect
     */
    useFramework(name = 'auto') {
        if (name === 'auto') {
            const detected = FrameworkRegistry.detectFramework();
            if (!detected) {
                console.warn('Could not auto-detect framework');
                return;
            }
            name = detected;
        }
        const adapter = FrameworkRegistry.get(name);
        if (!adapter) {
            console.warn(`Framework "${name}" not found. Available: uikit, bootstrap, tailwind`);
            return;
        }
        this.registerActionOverrides(adapter.getOverrides());
        console.log(`Using ${name} framework adapter`);
    }
    setErrorTarget(elementId) {
        this.overrideNav.setErrorTarget(elementId);
    }
    removeErrorTarget() {
        this.overrideNav.removeErrorTarget();
    }
    setDefaultTarget(targetSel) {
        this.overrideNav.setDefaultTarget(targetSel);
    }
    removeDefaultTarget() {
        this.overrideNav.removeDefaultTarget();
    }
    writeCookie(options) {
        const date = new Date();
        date.setTime(date.getTime() + (options.expiresInSeconds ?? 60 * 60 * 24 * 7) * 1000);
        let cookie = `${encodeURIComponent(options.name)}=${encodeURIComponent(options.value)}`;
        cookie += `;expires=${date.toUTCString()}`;
        cookie += `;path=${options.path ?? '/'}`;
        cookie += `;SameSite=${options.sameSite ?? 'Strict'}`;
        if (options.secure ?? true) {
            cookie += ';Secure';
        }
        document.cookie = cookie;
    }
}
//# sourceMappingURL=Plang.js.map