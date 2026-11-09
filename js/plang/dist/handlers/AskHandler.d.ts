import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { AskMessage } from "../types/messages.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export declare class AskHandler extends BaseKindHandler implements KindHandler<"ask"> {
    kind: "ask";
    actionHandler: ActionHandler;
    plang: Plang;
    constructor(plang: Plang);
    handle(message: AskMessage, ctx: HandleCtx): Promise<void>;
}
