import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export class AskHandler extends BaseKindHandler {
    constructor(plang) {
        super();
        this.kind = "ask";
        this.plang = plang;
        this.actionHandler = new ActionHandler(plang);
    }
    async handle(message, ctx) {
        if (!UtilHelper.hasContentModification(message.actions)) {
            message.actions = ['replace'].concat(message.actions ?? []);
        }
        if (await this.checkChannelOverride(message, ctx)) {
            return;
        }
        if (message.callback) {
            if (message.content.toLowerCase().indexOf('<form') != -1) {
                const hashInput = `<input type="hidden" name="__plang_callback_hash" value="${message.callback.hash}">`;
                const ownsCallback = (inner) => /name\s*=\s*["']callback["']/i.test(inner);
                const forms = message.content.match(/<form[^>]*>[\s\S]*?<\/form>/gi) ?? [];
                if (forms.some(ownsCallback)) {
                    message.content = message.content.replace(/<form([^>]*)>([\s\S]*?)<\/form>/gi, (all, attributes, inner) => ownsCallback(inner) ? `<form${attributes}>${hashInput}${inner}</form>` : all);
                }
                else {
                    message.content = message.content.replace(/<form([^>]*)>/gi, `<form$1>${hashInput}`);
                }
            }
            else {
                let form = document.querySelector('form[action="' + message.callback.path + '"]');
                if (form) {
                    let inputItem = form.querySelector('input[name="__plang_callback_hash"]');
                    if (!inputItem) {
                        inputItem = document.createElement('input');
                        inputItem.type = 'hidden';
                        inputItem.name = '__plang_callback_hash';
                        form.append(inputItem);
                    }
                    inputItem.value = message.callback.hash;
                }
                else {
                    console.error(`Could not find form pointing to ${message.callback.path}`);
                }
            }
            this.plang.callback.set(message.callback.hash, message.callback);
        }
        this.actionHandler.applyActions(message, ctx);
    }
}
//# sourceMappingURL=AskHandler.js.map