import type { FrameworkAdapter, ActionOverrideHandler } from "./ActionOverrides.js";
export declare class TailwindAdapter implements FrameworkAdapter {
    getName(): string;
    getOverrides(): Record<string, ActionOverrideHandler>;
}
