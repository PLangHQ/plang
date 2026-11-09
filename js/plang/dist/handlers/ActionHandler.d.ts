import type { ActionOverrideHandler } from "../actions/ActionOverrides.js";
import { Debugger } from "../debug/Debugger.js";
import type { T } from "../i18n/index.js";
import type { HistoryNavigator } from "../navigation/HistoryNavigator.js";
import type { HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { DomMessage } from "../types/messages.js";
export declare class ActionHandler {
    tr: T;
    navigator: HistoryNavigator;
    debugger: Debugger;
    private actionOverrides;
    private static readonly ELEMENT_CREATING_ACTIONS;
    plang: Plang;
    constructor(plang: Plang);
    applyActions(message: DomMessage, ctx: HandleCtx): Promise<void>;
    applyAction(action: string, message: DomMessage, ctx: HandleCtx): Promise<void>;
    registerOverride(action: string, handler: ActionOverrideHandler): void;
    registerOverrides(overrides: Record<string, ActionOverrideHandler>): void;
    private insertedScriptList;
    private insertScripts;
    private insertStyles;
}
