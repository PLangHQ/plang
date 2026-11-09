import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { ExecuteMessage } from "../types/messages.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
interface Instruction {
    "selector": string;
    "member": string;
    "value": string;
    "kind": "Property" | "Attribute" | "Style" | "Event";
    "type": string;
}
export declare class ExecuteHandler extends BaseKindHandler implements KindHandler<"execute"> {
    kind: "execute";
    plang: Plang;
    actionHandler: ActionHandler;
    constructor(plang: Plang);
    handle(message: ExecuteMessage, ctx: HandleCtx): Promise<void>;
    resolveFunction(path: string, context?: any): Function | null;
    private resolveAction;
    show(message: ExecuteMessage): Promise<void>;
    redirect(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx): Promise<void>;
    navigate(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx): Promise<void>;
    replaceState(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx): Promise<void>;
    uiRemove(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx): Promise<void>;
    dialogCommand(closeAll: any): Promise<void>;
    uiInstruction(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx): Promise<void>;
}
export {};
