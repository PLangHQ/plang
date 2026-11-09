import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { RenderMessage } from "../types/messages.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export declare class RenderHandler extends BaseKindHandler implements KindHandler<"render"> {
    kind: "render";
    actionHandler: ActionHandler;
    constructor(plang: Plang);
    handle(message: RenderMessage, ctx: HandleCtx): Promise<void>;
}
