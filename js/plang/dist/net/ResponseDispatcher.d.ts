import type { T } from "../i18n/index.js";
import type { NavigatorOptions } from "../navigation/OverrideNavigator.js";
import type { Message } from "../types/messages.js";
export interface HandleCtx {
    isOk: boolean;
    options: NavigatorOptions;
    url: string;
    headers: Headers;
    init: RequestInit;
}
type FinalHandler = (data: Message, ctx: HandleCtx) => Promise<void> | void;
export type MessageOf<K extends Message["kind"]> = Extract<Message, {
    kind: K;
}>;
export interface KindHandler<K extends Message["kind"]> {
    kind: K;
    handle: (env: MessageOf<K>, ctx: HandleCtx) => Promise<void> | void;
}
export declare class ResponseDispatcher {
    private tr;
    private map;
    constructor(tr: T);
    set(kind: string, handler: FinalHandler): this;
    register<K extends Message["kind"]>(h: KindHandler<K>): this;
    before(kind: string, fn: (d: Message, c: HandleCtx) => Promise<void> | void): this;
    after(kind: string, fn: (d: Message, c: HandleCtx) => Promise<void> | void): this;
    handle(d: Message, ctx: HandleCtx): Promise<undefined>;
}
export {};
