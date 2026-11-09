import type { T } from "../i18n/index.js";
import type { HistoryNavigator } from "../navigation/HistoryNavigator.js";
import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { RenderMessage, Message } from "../types/messages.js";
import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";


export class RenderHandler extends BaseKindHandler implements KindHandler<"render"> {
    kind = "render" as const;
    actionHandler: ActionHandler;

    constructor(plang : Plang) {
        super();
        this.actionHandler = new ActionHandler(plang);
    }

    async handle(message: RenderMessage, ctx: HandleCtx) {
        if (!UtilHelper.hasContentModification(message.actions)) {
            message.actions = ['replace'].concat(message.actions ?? []);
        }

        if (await this.checkChannelOverride(message, ctx)) {
            return; 
        }

        this.actionHandler.applyActions(message, ctx);

    }

}
