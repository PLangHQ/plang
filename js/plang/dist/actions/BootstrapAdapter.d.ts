import type { FrameworkAdapter, ActionOverrideHandler } from "./ActionOverrides.js";
export declare class BootstrapAdapter implements FrameworkAdapter {
    getName(): string;
    getOverrides(): Record<string, ActionOverrideHandler>;
}
