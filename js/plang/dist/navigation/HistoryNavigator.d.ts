import type { T } from "../i18n/index.js";
import type { Plang } from "../Plang.js";
import type { NavigatorOptions } from "./OverrideNavigator.js";
export type HistoryStateShape = {
    url: string;
    options: NavigatorOptions;
    scroll: [number, number] | null;
};
export declare class HistoryNavigator {
    private plang;
    private tr;
    private elementMap;
    constructor(plang: Plang, tr?: T);
    navigate(url: string, options: NavigatorOptions): Promise<unknown>;
    saveScrollPosition(): void;
    setState(newState: HistoryStateShape, replace?: boolean): void;
    private swapSourceElement;
}
