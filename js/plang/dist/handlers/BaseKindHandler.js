export class BaseKindHandler {
    constructor() {
        this.channelOverrides = new Map();
    }
    registerChannelOverride(channel, handler) {
        this.channelOverrides.set(channel, handler);
    }
    registerChannelOverrides(overrides) {
        Object.entries(overrides).forEach(([channel, handler]) => {
            this.registerChannelOverride(channel, handler);
        });
    }
    async checkChannelOverride(message, ctx) {
        if (message.channel) {
            const override = this.channelOverrides.get(message.channel);
            if (override) {
                await override(message, ctx);
                return true; // Override handled it
            }
        }
        return false; // No override, continue with default behavior
    }
}
//# sourceMappingURL=BaseKindHandler.js.map