import type { ChannelOverrideHandler } from "../actions/ActionOverrides.js";
import type { BaseMessage } from "../types/messages.js";
import type { HandleCtx } from "../net/ResponseDispatcher.js";
export declare abstract class BaseKindHandler {
    private channelOverrides;
    registerChannelOverride(channel: string, handler: ChannelOverrideHandler): void;
    registerChannelOverrides(overrides: Record<string, ChannelOverrideHandler>): void;
    protected checkChannelOverride(message: BaseMessage, ctx: HandleCtx): Promise<boolean>;
}
