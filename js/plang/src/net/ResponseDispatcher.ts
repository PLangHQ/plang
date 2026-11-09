    import type { T } from "../i18n/index.js";
    import type { NavigatorOptions } from "../navigation/OverrideNavigator.js";
    import type { Message } from "../types/messages.js";

    export interface HandleCtx {
    isOk: boolean;
    options : NavigatorOptions
    url: string;
    headers: Headers;
    init : RequestInit;
    }

    type Middleware = (data: Message, ctx: HandleCtx, next: () => Promise<void>) => Promise<void> | void;
    type FinalHandler = (data: Message, ctx: HandleCtx) => Promise<void> | void;


    export type MessageOf<K extends Message["kind"]> = Extract<Message, { kind: K }>;

    export interface KindHandler<K extends Message["kind"]> {
    kind: K;
    handle: (env: MessageOf<K>, ctx: HandleCtx) => Promise<void> | void;
    }

    function compose(mw: Middleware[], handler: FinalHandler): FinalHandler {
    return mw.reduceRight<FinalHandler>((next, fn) => (d, c) => Promise.resolve(fn(d, c, () => Promise.resolve(next(d, c)))), handler);
    }

    type Entry = { mws: Middleware[]; handler: FinalHandler; composed: FinalHandler };

    export class ResponseDispatcher {
    private tr: T;
    private map = new Map<string, Entry>();

    constructor(tr: T) { this.tr = tr; }

    set(kind: string, handler: FinalHandler) {
        const e = this.map.get(kind) ?? { mws: [], handler, composed: handler };
        e.handler = handler; e.composed = compose(e.mws, e.handler);
        this.map.set(kind, e); return this;
    }

    register<K extends Message["kind"]>(h: KindHandler<K>): this {
        return this.set(h.kind, (d, c) => h.handle(d as MessageOf<K>, c));
    }

    before(kind: string, fn: (d: Message, c: HandleCtx) => Promise<void> | void) {
        const e = this.map.get(kind) ?? { mws: [], handler: async () => {}, composed: async () => {} };
        e.mws.push(async (d, c, next) => { await fn(d, c); await next(); });
        e.composed = compose(e.mws, e.handler); this.map.set(kind, e); return this;
    }

    after(kind: string, fn: (d: Message, c: HandleCtx) => Promise<void> | void) {
        const e = this.map.get(kind) ?? { mws: [], handler: async () => {}, composed: async () => {} };
        e.mws.push(async (d, c, next) => { await next(); await fn(d, c); });
        e.composed = compose(e.mws, e.handler); this.map.set(kind, e); return this;
    }

    async handle(d: Message, ctx: HandleCtx) {
        if (ctx.isOk && ctx.options.onSuccess) return void (await ctx.options.onSuccess(d, ctx));
        if (!ctx.isOk && ctx.options.onFailure) return void (await ctx.options.onFailure(d, ctx));
        
        const e = this.map.get(String(d?.kind ?? ""));
        if (!e) throw new Error(this.tr("fnNotFound", { fn: String((d as any)?.kind) }));
        await e.composed(d, ctx);
    }
    }
