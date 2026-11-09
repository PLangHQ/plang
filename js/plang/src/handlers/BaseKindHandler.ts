import type { ChannelOverrideHandler } from "../actions/ActionOverrides.js";
import type { BaseMessage } from "../types/messages.js";
import type { HandleCtx } from "../net/ResponseDispatcher.js";

export abstract class BaseKindHandler {
    private channelOverrides: Map<string, ChannelOverrideHandler> = new Map();

    public registerChannelOverride(channel: string, handler: ChannelOverrideHandler) {
        this.channelOverrides.set(channel, handler);
    }

    public registerChannelOverrides(overrides: Record<string, ChannelOverrideHandler>) {
        Object.entries(overrides).forEach(([channel, handler]) => {
            this.registerChannelOverride(channel, handler);
        });
    }

    protected async checkChannelOverride(message: BaseMessage, ctx: HandleCtx): Promise<boolean> {
        if (message.channel) {
            const override = this.channelOverrides.get(message.channel);
            if (override) {
                await override(message as any, ctx);
                return true; // Override handled it
            }
        }
        return false; // No override, continue with default behavior
    }
}