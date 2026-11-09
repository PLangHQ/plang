import { HistoryNavigator } from "./navigation/HistoryNavigator.js";
import { type NavigatorOptions } from "./navigation/OverrideNavigator.js";
import type { T } from "./i18n/index.js";
import { ResponseDispatcher, type HandleCtx } from "./net/ResponseDispatcher.js";
import { type ActionOverrideHandler, type ChannelOverrideHandler } from "./actions/ActionOverrides.js";
import type { DialogManager } from "./ui/DialogManager.js";
import { Debugger } from "./debug/Debugger.js";
import { DebugBar } from "./debug/DebugBar.js";
declare global {
    interface Window {
        plang: Plang;
    }
}
export declare class Plang {
    navigator: HistoryNavigator;
    private overrideNav;
    private pollRequest;
    private ready;
    private failed;
    private bootTimer;
    private signer;
    callback: Map<string, object>;
    errors: import("./net/ErrorReporter.js").ErrorReporter;
    tr: T;
    responses: ResponseDispatcher;
    private actionHandlers;
    private kindHandlers;
    windowStack: DialogManager[];
    debugger: Debugger;
    debugBar: DebugBar;
    /** Where the signing key ended up. Sent on the first poll so the server can record it. */
    keyStoreTier: string | null;
    constructor(tr?: T);
    /**
     * Report a JS error. Call this from any catch block; a swallowed failure is one we
     * cannot diagnose, and console.warn/console.error reach nobody.
     */
    stackError(type: string, message: string, stack?: string): void;
    /** Convenience for catch blocks that hold an unknown. */
    reportCaught(type: string, err: unknown): void;
    MyIdentity(): Promise<any>;
    logout(): Promise<void>;
    init(): Promise<void>;
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
    private announceReady;
    /** Boot did not get as far as a working connection. Fired once. */
    private announceFailed;
    fetch(url: string, options?: NavigatorOptions | undefined): Promise<any>;
    getPlangHash(body: any): string | null;
    handleResponse(line: string, ctx: HandleCtx): Promise<void>;
    private setCallback;
    get(path: string, options?: NavigatorOptions | null): Promise<any>;
    post(path: string, data: any, options?: NavigatorOptions | null): Promise<any>;
    callGoal(path: string, data: any, callbackOrOptions: any): Promise<any>;
    onPoll(_line: string): Promise<void>;
    /**
         * Register a single action override
         */
    registerActionOverride(action: string, handler: ActionOverrideHandler): void;
    /**
     * Register multiple action overrides at once
     */
    registerActionOverrides(overrides: Record<string, ActionOverrideHandler>): void;
    registerChannelOverride(channel: string, handler: ChannelOverrideHandler): void;
    registerChannelOverrides(overrides: Record<string, ChannelOverrideHandler>): void;
    /**
     * Use a built-in framework adapter
     * @param name - Framework name ('uikit', 'bootstrap', 'tailwind') or 'auto' to detect
     */
    useFramework(name?: string): void;
    setErrorTarget(elementId: string): void;
    removeErrorTarget(): void;
    setDefaultTarget(targetSel: string): void;
    removeDefaultTarget(): void;
    writeCookie(options: CookieOptions): void;
}
interface CookieOptions {
    name: string;
    value: string;
    expiresInSeconds?: number;
    path?: string;
    secure?: boolean;
    sameSite?: 'Strict' | 'Lax' | 'None';
}
export {};
