import type { ChannelOverrideHandler } from "../actions/ActionOverrides.js";
import type { T } from "../i18n/index.js";
import type { HistoryNavigator } from "../navigation/HistoryNavigator.js";
import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { TextMessage } from "../types/messages.js";
import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";

export class TextHandler extends BaseKindHandler implements KindHandler<"text"> {
    kind = "text" as const;
    actionHandler: ActionHandler;
    constructor(plang : Plang) {
        super();
        this.actionHandler = new ActionHandler(plang);
    }

    async handle(message: TextMessage, ctx: HandleCtx) {

        if (!UtilHelper.hasContentModification(message.actions)) {
            message.actions = ['append'].concat(message.actions ?? []);
        }

        if (await this.checkChannelOverride(message, ctx)) {
            return; 
        }
        if (!message.skipNewLine) message.content += "<br />";
        this.actionHandler.applyActions(message, ctx);
    }
}
