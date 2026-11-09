import type { DomMessage } from "../types/messages.js";
import type { HandleCtx } from "../net/ResponseDispatcher.js";
import { UiKitAdapter } from "./UiKitAdapter.js";
import { BootstrapAdapter } from "./BootstrapAdapter.js";
import { TailwindAdapter } from "./TailwindAdapter.js";

export type ActionOverrideHandler = (
    nodes: NodeListOf<HTMLElement>,
    message: DomMessage,
    ctx: HandleCtx
) => void;

export type ChannelOverrideHandler = (
  message: DomMessage,
  ctx: HandleCtx
) => void | Promise<void>;


export interface FrameworkAdapter {
    getName(): string;
    getOverrides(): Record<string, ActionOverrideHandler>;
    getChannelOverrides?(): Record<string, ChannelOverrideHandler>;
}

// Registry for built-in adapters
export class FrameworkRegistry {
    private static adapters = new Map<string, FrameworkAdapter>();

    static register(adapter: FrameworkAdapter) {
        this.adapters.set(adapter.getName(), adapter);
    }

    static get(name: string): FrameworkAdapter | undefined {
        return this.adapters.get(name);
    }

    static detectFramework(): string | null {
        if ((window as any).UIkit) return 'uikit';
        if ((window as any).bootstrap) return 'bootstrap';
        return null;
    }
}

// Registered here rather than in a `static {}` block. Static initialization blocks are ES2022 and
// Safari only parses them from 16.4, so on 16.1 the whole module graph failed to parse and the
// client never booted: no identity, no cart, nothing. A parse error cannot be caught at runtime,
// which is why it showed up only in the pre-boot trap in the layout.
FrameworkRegistry.register(new UiKitAdapter());
FrameworkRegistry.register(new BootstrapAdapter());
FrameworkRegistry.register(new TailwindAdapter());