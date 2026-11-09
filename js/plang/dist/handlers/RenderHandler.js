import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export class RenderHandler extends BaseKindHandler {
    constructor(plang) {
        super();
        this.kind = "render";
        this.actionHandler = new ActionHandler(plang);
    }
    async handle(message, ctx) {
        if (!UtilHelper.hasContentModification(message.actions)) {
            message.actions = ['replace'].concat(message.actions ?? []);
        }
        if (await this.checkChannelOverride(message, ctx)) {
            return;
        }
        this.actionHandler.applyActions(message, ctx);
    }
}
//# sourceMappingURL=RenderHandler.js.map