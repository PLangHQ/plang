import { HistoryNavigator } from "./navigation/HistoryNavigator.js";
import { OverrideNavigator, type HeaderKV, type NavigatorOptions } from "./navigation/OverrideNavigator.js";
import { PollRequest } from "./net/PollRequest.js";
import { ECDSASigner } from "./crypto/ECDSASigner.js";
import { t as defaultT } from "./i18n/index.js";
import type { T } from "./i18n/index.js";
import { ResponseDispatcher, type HandleCtx } from "./net/ResponseDispatcher.js";
import type { BaseMessage, Message, ErrorMessage, RenderMessage } from "./types/messages.js";
import { RenderHandler } from "./handlers/RenderHandler.js";
import { TextHandler } from "./handlers/TextHandler.js";
import { ErrorHandler } from "./handlers/ErrorHandler.js";
import { ExecuteHandler } from "./handlers/ExecuteHandler.js";
import { AskHandler } from "./handlers/AskHandler.js";
import { StreamHandler } from "./handlers/StreamHandler.js";
import { UtilHelper } from "./util/UtilHelper.js";
import { FrameworkRegistry, type ActionOverrideHandler, type ChannelOverrideHandler } from "./actions/ActionOverrides.js";
import type { ActionHandler } from "./handlers/ActionHandler.js";
import type { BaseKindHandler } from "./handlers/BaseKindHandler.js";
import { LogNotificationManager } from "./channels/log/LogNotificationManager.js";
import type { DialogManager } from "./ui/DialogManager.js";
import { Debugger } from "./debug/Debugger.js";
import { DebugBar } from "./debug/DebugBar.js";
import { errorReporter } from "./net/ErrorReporter.js";


declare global {
    interface Window {
        plang: Plang;
    }
}

// How long boot may take before the page is told it failed. Only bounds the wait for the first
// poll response; a later connect still promotes the page to ready.
const BOOT_TIMEOUT_MS = 15_000;


export class Plang {


    public navigator: HistoryNavigator;
    private overrideNav: OverrideNavigator;
    private pollRequest: PollRequest | null = null;
    private ready = false;
    private failed = false;
    private bootTimer: ReturnType<typeof setTimeout> | null = null;
    private signer = new ECDSASigner();
    public callback = new Map<string, object>();
    public errors = errorReporter;
    public tr: T;
    public responses: ResponseDispatcher;
    private actionHandlers: ActionHandler[] = [];
    private kindHandlers: BaseKindHandler[] = [];
    public windowStack: DialogManager[] = [];
    public debugger: Debugger
    public debugBar: DebugBar
    /** Where the signing key ended up. Sent on the first poll so the server can record it. */
    public keyStoreTier: string | null = null;
    constructor(tr: T = defaultT) {
        this.tr = tr;
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
                this.actionHandlers.push((h as any).actionHandler);
            }
        });

        this.kindHandlers.push(renderHandler, textHandler, errorHandler, askHandler);


        // reportCaught, not stackError: it carries the already-reported tag.
        addEventListener("error", (e: ErrorEvent) => {
            if (e.error) this.errors.reportCaught("error", e.error);
            else this.stackError("error", e.message, `${e.filename}:${e.lineno}:${e.colno}`);
        });

        addEventListener("unhandledrejection", (e: PromiseRejectionEvent) => {
            if (e.reason) this.errors.reportCaught("unhandledrejection", e.reason);
            else this.stackError("unhandledrejection", String(e.reason));
        });

        // Errors the inline snippet in the layout caught before this module was parsed.
        this.errors.drainPreBoot((window as any).__plangJsErrors);
        (window as any).__plangJsErrors = null;

        this.useFramework();
    }

    /**
     * Report a JS error. Call this from any catch block; a swallowed failure is one we
     * cannot diagnose, and console.warn/console.error reach nobody.
     */
    public stackError(type: string, message: string, stack?: string) {
        this.errors.report(type, message, stack);
    }

    /** Convenience for catch blocks that hold an unknown. */
    public reportCaught(type: string, err: unknown) {
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
        } catch (e) {
            this.reportCaught("keyStoreTier", e);
        }

        // plang:ready used to be dispatched at the end of init(), one line after the poll was
        // constructed and while its first request was still in flight. It therefore promised
        // nothing about being able to reach the server, yet every listener calls callGoal or
        // post the moment it fires: teacher/templates/landing.html, js/rafbokin/plang-init.ts
        // and cart/payment/Straumur_Complete.html. It now fires from fetch(), the moment a
        // request has actually been answered, which is what all three already assume.
        this.bootTimer = setTimeout(() => {
            this.bootTimer = null;
            if (this.ready) return;
            this.announceFailed("poll");
        }, BOOT_TIMEOUT_MS);

        this.pollRequest = new PollRequest("/?plang.poll=1", {
            onMessage: async (line, ctx) => {
                try {
                    if (line.startsWith('\"ping\"')) return;

                    const msg = JSON.parse(line) as Message;

                    // should call a handler here
                    await this.responses.handle(msg, ctx);

                } catch (e) {
                    if (line !== "ping") {
                        console.warn(this.tr("badLine"), line);
                        this.reportCaught("pollMessage", e);
                    }
                }
            },
        });


        const logManager = new LogNotificationManager();
        this.registerChannelOverrides({
            log: (message: any, ctx: any) => {
                logManager.addLogMessage(message);
            },
            debug: (message: any, ctx: any) => {
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
    private announceReady(): void {
        if (this.ready) return;
        this.ready = true;
        if (this.bootTimer) {
            clearTimeout(this.bootTimer);
            this.bootTimer = null;
        }
        window.dispatchEvent(new CustomEvent('plang:ready', { detail: this }));
    }

    /** Boot did not get as far as a working connection. Fired once. */
    private announceFailed(reason: string): void {
        if (this.failed) return;
        this.failed = true;
        this.errors.report("boot", `plang boot failed: ${reason}`);
        window.dispatchEvent(new CustomEvent('plang:failed', { detail: { reason } }));
    }

    async fetch(
        url: string,
        options?: NavigatorOptions | undefined
    ): Promise<any> {
        try {
            const headers = new Headers();
            headers.set("Accept", "application/plang+json");
            headers.set("X-Requested-With", "XMLHttpRequest");
            if (!options) {
                options = { method : 'GET'}
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
            } as RequestInit;
            const req = new Request(url, requestInit);

            const sigObj = await this.signer.signRequest(req);
            req.headers.set("X-Signature", JSON.stringify(sigObj));
            const res = await window.fetch(req);
            this.announceReady();

            if (UtilHelper.isDownloadResponse(res)) {
                await UtilHelper.doDownloadResponse(res);
                return;
            }

            const body = res.body;
            if (!body) return null;

            const handleCtx = {
                isOk: res.ok,
                options: options,
                init: requestInit,
                headers: headers,
                url: url
            } as HandleCtx;

            const reader = body.getReader();
            const decoder = new TextDecoder();
            let buffer = "";
            while (true) {
                const { value, done } = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, { stream: true });
                const lines = buffer.split("\n");
                buffer = lines.pop() ?? "";

                for (const line of lines) {
                    if (!line.trim() || line.startsWith('\"ping\"')) continue;

                    await this.handleResponse(line, handleCtx);
                }
            }

            if (buffer.trim()) {
                await this.handleResponse(buffer, handleCtx);
            }

            return null;
        } catch (e) {
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

    getPlangHash(body: any) {
        let plangHash: string | null = null;
        if (body instanceof FormData) {
            plangHash = body.get('__plang_callback_hash') as string | null;
        } else if (body && typeof body === 'object') {
            plangHash = body.__plang_hash || null;
        }
        return plangHash;
    }

    public async handleResponse(line: string, ctx: HandleCtx) {
        let env: Message;
        try {
            env = JSON.parse(line) as Message;

            // The server renewed a callback that ran out of its five minutes (or whose nonce was
            // used), so the answer is resent once with the renewed callback, without the user
            // noticing. The resend must carry the new hash: with the old one it sent the expired
            // callback again and the two requests bounced back and forth for ever.
            if ((env as ErrorMessage).key === "CallbackError" && !(ctx.options as any)?.__plangResent) {
                let errorMessage = env as ErrorMessage;
                if (errorMessage.callback) {
                    this.callback.set(errorMessage.callback.hash, errorMessage.callback);

                    const previousHash = errorMessage.callback.previousHash;
                    if (previousHash) {
                        const elements = document.querySelectorAll(`input[name="__plang_callback_hash"][value="${previousHash}"]`);
                        for (let i = 0; i < elements.length; i++) {
                            (elements[i] as HTMLInputElement).value = errorMessage.callback.hash;
                        }
                    }

                    if (ctx.options?.body instanceof FormData) {
                        ctx.options.body.set('__plang_callback_hash', errorMessage.callback.hash);
                    }
                }

                await this.fetch(ctx.url, { ...ctx.options, __plangResent: true } as any);
                return;
            }

            await this.responses.handle(env, ctx);
        } catch (e) {
            console.log(this.tr("badLine"), line, e);
            this.reportCaught("handleResponse", e);
            throw e;
        }
    }

    private setCallback(plangOutput: any) {
        const props = plangOutput?.properties ?? {};
        if (props.path && props.callback) this.callback.set(props.path, props.callback);
    }


    async get(path: string, options?: NavigatorOptions | null) {
        if (!options) options = {} as NavigatorOptions;
        options.method = 'GET';
        return this.fetch(path, options);
    }

    async post(path: string, data: any, options?: NavigatorOptions | null) {
        if (!options) options = {} as NavigatorOptions;
        const body = data instanceof FormData ? data : JSON.stringify(data);
        options.body = body;
        options.method = 'POST';
        return this.fetch(path, options);
    }

    async callGoal(path: string, data: any, callbackOrOptions: any) {
        const body = data instanceof FormData ? data : JSON.stringify(data);

        const isCallback = typeof callbackOrOptions === 'function';
        const callback = isCallback ? callbackOrOptions : null;
        const options = isCallback ? null : callbackOrOptions;

        const headers: HeaderKV[] = [];
        if (options) {
            for (const [k, v] of Object.entries(options)) {
                if (!k.startsWith('p-')) continue;

                headers.push({ key: "p-" + k, value: String(v) });
            }
        }
        return this.fetch(path, { body, method: "POST", headers: headers });
    }

    async onPoll(_line: string) {
        /* PollRequest will call onMessage itself after parsing; keep if you want raw handling */
    }

    /**
         * Register a single action override
         */
    public registerActionOverride(action: string, handler: ActionOverrideHandler) {
        this.actionHandlers.forEach(actionHandler => {
            actionHandler.registerOverride(action, handler);
        });
    }

    /**
     * Register multiple action overrides at once
     */
    public registerActionOverrides(overrides: Record<string, ActionOverrideHandler>) {
        this.actionHandlers.forEach(actionHandler => {
            actionHandler.registerOverrides(overrides);
        });
    }

    public registerChannelOverride(channel: string, handler: ChannelOverrideHandler) {
        this.kindHandlers.forEach(kindHandler => {
            kindHandler.registerChannelOverride(channel, handler);
        });
    }

    public registerChannelOverrides(overrides: Record<string, ChannelOverrideHandler>) {
        this.kindHandlers.forEach(kindHandler => {
            kindHandler.registerChannelOverrides(overrides);
        });
    }

    /**
     * Use a built-in framework adapter
     * @param name - Framework name ('uikit', 'bootstrap', 'tailwind') or 'auto' to detect
     */
    public useFramework(name: string = 'auto') {
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


    setErrorTarget(elementId: string) {
        this.overrideNav.setErrorTarget(elementId);
    }

    removeErrorTarget() {
        this.overrideNav.removeErrorTarget();
    }

    setDefaultTarget(targetSel: string) {
        this.overrideNav.setDefaultTarget(targetSel);
    }
    removeDefaultTarget() {
        this.overrideNav.removeDefaultTarget();
    }

    
    writeCookie(options: CookieOptions): void {
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

interface CookieOptions {
        name: string;
        value: string;
        expiresInSeconds?: number;
        path?: string;
        secure?: boolean;
        sameSite?: 'Strict' | 'Lax' | 'None';
    }
type JsErr = { type: string; message: string; stack?: string; href: string; count: number };