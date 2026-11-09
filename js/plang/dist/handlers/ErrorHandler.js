import { t } from "../i18n/index.js";
import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
export class ErrorHandler extends BaseKindHandler {
    constructor(plang) {
        super();
        this.kind = "error";
        this.plang = plang;
        this.actionHandler = new ActionHandler(plang);
    }
    async handle(message, ctx) {
        if (message.callback) {
            this.plang.callback.set(message.callback.hash, message.callback);
            const previousHash = message.callback.previousHash;
            if (previousHash) {
                const elements = document.querySelectorAll(`input[name="__plang_callback_hash"][value="${previousHash}"]`);
                for (let i = 0; i < elements.length; i++) {
                    elements[i].value = message.callback.hash;
                }
            }
        }
        if (await this.checkChannelOverride(message, ctx)) {
            return;
        }
        if (message.target) {
            if (!UtilHelper.hasContentModification(message.actions)) {
                message.actions = ['replace'].concat(message.actions ?? []);
            }
            if (ctx.options.submitter) {
                var errorElement = document.querySelector(message.target);
                if (errorElement && ctx.options.submitter && ctx.options.submitter.parentNode) {
                    ctx.options.submitter.parentNode.insertBefore(errorElement, ctx.options.submitter);
                }
            }
            this.actionHandler.applyActions(message, ctx);
            return;
        }
        var msg = message.content ?? message.message ?? 'Unknown error';
        if (!message.debug && this.showWhereItHappened(msg, ctx, message.key === "CallbackExpired")) {
            console.error("[plang error]", message);
            return;
        }
        if (message.debug) {
            if (message.actions) {
                message.actions.push('replace');
            }
            else {
                message.actions = ['replace'];
            }
            const div = document.createElement('div');
            div.textContent = message.content;
            message.content = `<pre>${div.innerHTML}</pre>`;
            this.actionHandler.applyActions(message, ctx);
        }
        else {
            window.UIkit?.notification?.({ message: msg, status: "danger", pos: "top-center", timeout: 5000 });
        }
        console.error("[plang error]", message);
    }
    // An error message with no target used to reach window.UIkit.notification, and UIkit is not
    // loaded in every app, so the optional chaining swallowed it and the user saw nothing at all.
    // The error belongs where the user was working: the element they clicked or the form they
    // submitted. Only when the request came from somewhere with no source element, a poll or a
    // background refresh, does it go to the top of the page.
    // A form past the timeout set on its ask step is refused on purpose. It is not a fault to
    // report, the user has to open the page again.
    showWhereItHappened(msg, ctx, formExpired = false) {
        const source = (ctx?.options?.submitter ?? ctx?.options?.sourceElement);
        const box = document.createElement("div");
        box.className = "plangVilla";
        box.setAttribute("role", "alert");
        const text = document.createElement("p");
        text.className = "plangVillaText";
        text.textContent = t(formExpired ? "errorFormExpired" : "errorFailed");
        const detail = document.createElement("details");
        const sum = document.createElement("summary");
        sum.textContent = t("errorWhatHappened");
        const pre = document.createElement("pre");
        pre.textContent = String(msg);
        detail.appendChild(sum);
        detail.appendChild(pre);
        const ok = document.createElement("button");
        ok.type = "button";
        ok.className = "plangVillaOk";
        ok.textContent = t("errorOk");
        ok.addEventListener("click", () => box.remove());
        box.appendChild(text);
        box.appendChild(detail);
        box.appendChild(ok);
        if (source && source.isConnected) {
            const previous = source.parentElement?.querySelector(":scope > .plangVilla");
            if (previous)
                previous.remove();
            source.insertAdjacentElement("afterend", box);
            box.scrollIntoView({ block: "nearest" });
            return true;
        }
        const top = document.getElementById("plangVillur");
        if (!top)
            return false;
        top.replaceChildren(box);
        return true;
    }
}
//# sourceMappingURL=ErrorHandler.js.map