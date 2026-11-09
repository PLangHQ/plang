import type { DomMessage } from "../types/messages.js";
import type { HandleCtx } from "../net/ResponseDispatcher.js";
export type ActionOverrideHandler = (nodes: NodeListOf<HTMLElement>, message: DomMessage, ctx: HandleCtx) => void;
export type ChannelOverrideHandler = (message: DomMessage, ctx: HandleCtx) => void | Promise<void>;
export interface FrameworkAdapter {
    getName(): string;
    getOverrides(): Record<string, ActionOverrideHandler>;
    getChannelOverrides?(): Record<string, ChannelOverrideHandler>;
}
export declare class FrameworkRegistry {
    private static adapters;
    static register(adapter: FrameworkAdapter): void;
    static get(name: string): FrameworkAdapter | undefined;
    static detectFramework(): string | null;
}
