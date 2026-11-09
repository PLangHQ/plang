import type { T } from "../i18n/index.js";
import type { HandleCtx } from "./ResponseDispatcher.js";
export interface PollOptions {
    onMessage?: (line: string, ctx: HandleCtx) => void;
    reconnectDelay?: number;
}
export declare class PollRequest {
    private url;
    private onMessage;
    private reconnectDelay;
    private controller;
    private tr;
    constructor(url?: string, options?: PollOptions, tr?: T);
    close(): void;
    private connect;
    private streamToLines;
}
