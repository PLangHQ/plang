function compose(mw, handler) {
    return mw.reduceRight((next, fn) => (d, c) => Promise.resolve(fn(d, c, () => Promise.resolve(next(d, c)))), handler);
}
export class ResponseDispatcher {
    constructor(tr) {
        this.map = new Map();
        this.tr = tr;
    }
    set(kind, handler) {
        const e = this.map.get(kind) ?? { mws: [], handler, composed: handler };
        e.handler = handler;
        e.composed = compose(e.mws, e.handler);
        this.map.set(kind, e);
        return this;
    }
    register(h) {
        return this.set(h.kind, (d, c) => h.handle(d, c));
    }
    before(kind, fn) {
        const e = this.map.get(kind) ?? { mws: [], handler: async () => { }, composed: async () => { } };
        e.mws.push(async (d, c, next) => { await fn(d, c); await next(); });
        e.composed = compose(e.mws, e.handler);
        this.map.set(kind, e);
        return this;
    }
    after(kind, fn) {
        const e = this.map.get(kind) ?? { mws: [], handler: async () => { }, composed: async () => { } };
        e.mws.push(async (d, c, next) => { await next(); await fn(d, c); });
        e.composed = compose(e.mws, e.handler);
        this.map.set(kind, e);
        return this;
    }
    async handle(d, ctx) {
        if (ctx.isOk && ctx.options.onSuccess)
            return void (await ctx.options.onSuccess(d, ctx));
        if (!ctx.isOk && ctx.options.onFailure)
            return void (await ctx.options.onFailure(d, ctx));
        const e = this.map.get(String(d?.kind ?? ""));
        if (!e)
            throw new Error(this.tr("fnNotFound", { fn: String(d?.kind) }));
        await e.composed(d, ctx);
    }
}
//# sourceMappingURL=ResponseDispatcher.js.map