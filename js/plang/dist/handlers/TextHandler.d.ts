import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { TextMessage } from "../types/messages.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export declare class TextHandler extends BaseKindHandler implements KindHandler<"text"> {
    kind: "text";
    actionHandler: ActionHandler;
    constructor(plang: Plang);
    handle(message: TextMessage, ctx: HandleCtx): Promise<void>;
}
