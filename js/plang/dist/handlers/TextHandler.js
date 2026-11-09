import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export class TextHandler extends BaseKindHandler {
    constructor(plang) {
        super();
        this.kind = "text";
        this.actionHandler = new ActionHandler(plang);
    }
    async handle(message, ctx) {
        if (!UtilHelper.hasContentModification(message.actions)) {
            message.actions = ['append'].concat(message.actions ?? []);
        }
        if (await this.checkChannelOverride(message, ctx)) {
            return;
        }
        if (!message.skipNewLine)
            message.content += "<br />";
        this.actionHandler.applyActions(message, ctx);
    }
}
//# sourceMappingURL=TextHandler.js.map