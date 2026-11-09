import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { ErrorMessage } from "../types/messages.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export declare class ErrorHandler extends BaseKindHandler implements KindHandler<"error"> {
    kind: "error";
    actionHandler: ActionHandler;
    plang: Plang;
    constructor(plang: Plang);
    handle(message: ErrorMessage, ctx: HandleCtx): Promise<void>;
    showWhereItHappened(msg: string, ctx: HandleCtx, formExpired?: boolean): boolean;
}
