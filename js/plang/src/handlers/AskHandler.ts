import type { T } from "../i18n/index.js";
import type { HistoryNavigator } from "../navigation/HistoryNavigator.js";
import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { AskMessage } from "../types/messages.js";
import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";

export class AskHandler extends BaseKindHandler implements KindHandler<"ask"> {
    kind = "ask" as const;
    actionHandler: ActionHandler;
    plang : Plang;

    constructor(plang : Plang) {
        super();
        this.plang = plang;
        this.actionHandler = new ActionHandler(plang);
    }


    async handle(message: AskMessage, ctx: HandleCtx) {
        if (!UtilHelper.hasContentModification(message.actions)) {
            message.actions = ['replace'].concat(message.actions ?? []);
        }

        if (await this.checkChannelOverride(message, ctx)) {
            return; 
        }
        if (message.callback) {
            if (message.content.toLowerCase().indexOf('<form') != -1) {
                message.content = message.content.replace(
                    /<form([^>]*)>/gi,
                    `<form$1><input type="hidden" name="__plang_callback_hash" value="${message.callback.hash}">`
                );
            } else {
                let form = document.querySelector('form[action="' + message.callback.path + '"]') as HTMLFormElement;
                if (form) {
                    let inputItem = form.querySelector('input[name="__plang_callback_hash"]') as HTMLInputElement;
                    if (!inputItem) {
                        inputItem = document.createElement('input');
                        inputItem.type = 'hidden';
                        inputItem.name = '__plang_callback_hash';
                        form.append(inputItem);
                    }
                    inputItem.value = message.callback.hash;
                    
                } else {
                    console.error(`Could not find form pointing to ${message.callback.path}`);
                }
            }
    
            this.plang.callback.set(message.callback.hash, message.callback);
        }
        this.actionHandler.applyActions(message, ctx)
    }
}
