import type { FrameworkAdapter, ActionOverrideHandler } from "./ActionOverrides.js";
export declare class UiKitAdapter implements FrameworkAdapter {
    getName(): string;
    getOverrides(): Record<string, ActionOverrideHandler>;
}
