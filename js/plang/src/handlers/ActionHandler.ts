import type { ActionOverrideHandler } from "../actions/ActionOverrides.js";
import { Debugger } from "../debug/Debugger.js";
import type { T } from "../i18n/index.js";
import type { HistoryNavigator, HistoryStateShape } from "../navigation/HistoryNavigator.js";
import type { HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { DomMessage, Dom } from "../types/messages.js";
import { DialogManager } from "../ui/DialogManager.js";
import { ActionSorter } from "../util/SortActions.js";
import { UtilHelper } from "../util/UtilHelper.js";

export class ActionHandler {
    tr: T;
    navigator: HistoryNavigator;
    debugger: Debugger;
    private actionOverrides: Map<string, ActionOverrideHandler> = new Map();
    private static readonly ELEMENT_CREATING_ACTIONS = new Set(['showDialog', 'showModal']);
    plang : Plang;
    constructor(plang : Plang) {
        this.tr = plang.tr;
        this.navigator = plang.navigator;
        this.debugger = plang.debugger;
        this.plang = plang;
    }

    public async applyActions(message: DomMessage, ctx: HandleCtx) {

        let actions = ActionSorter.sort(message.actions);
        const { html, scripts, styles, elementId } = UtilHelper.extractHtmlAndScripts(message.content);
        message.dom = { html, scripts, styles, elementId } as Dom;
        if (!message.target) {
              message.target = ActionHandler.ELEMENT_CREATING_ACTIONS.has(actions[0]) ? '#plangDialog' : '#main'
        }

        if (actions.includes('navigate')) {
            this.navigator.saveScrollPosition();
        }
        if (ctx.options.beforeActions) {
            let fn = window[(ctx.options.beforeActions as any)] as any;
            if (fn) fn.call(this, message, ctx);
        }
        for (const action of actions) {
            await this.applyAction(action, message, ctx);
        }

        if (ctx.options.afterActions) {
            let fn = window[(ctx.options.afterActions as any)] as any;
            if (fn) fn.call(this, message, ctx);
        }
    }

    public async applyAction(
        action: string,
        message: DomMessage, ctx: HandleCtx
    ) {
        const targetSel = message.target ?? (!ActionHandler.ELEMENT_CREATING_ACTIONS.has(action) ? '#main' : "#plangDialog");
        const nodes = document.querySelectorAll<HTMLElement>(targetSel);
        
        if (nodes.length == 0 && !ActionHandler.ELEMENT_CREATING_ACTIONS.has(action)) {
            UtilHelper.elementNotFound(targetSel, message, ctx);
            return;
        }


        const id = message.dom.elementId?.id ?? crypto.randomUUID();
        const dom = message.dom;
        if (message.debug) {
            this.debugger.processMessage(message);
        }

        const override = this.actionOverrides.get(action);
        if (override) {
            override(nodes, message, ctx);
            return;
        }

        switch (action) {
            case "replace":
            case "replaceSelf": {
                action = action === "replace" ? "innerHTML" : "outerHTML";
                if ((action as any).reRender === false && dom.elementId) {
                    const exists = document.getElementById(dom.elementId.id);
                    if (exists) return;
                }
                this.insertStyles(dom.styles);
                nodes.forEach((container) => {
                    const anyC = container as any;
                    if (typeof anyC[action] === "function") anyC[action](...Array.from(dom.html.childNodes));
                    else anyC[action] = dom.html.innerHTML;


                });
                await this.insertScripts(dom.scripts, id);

                break;
            }

            case "append":
            case "appendOrReplace":
            case "prepend":
            case "prependOrReplace": {
                this.insertStyles(dom.styles);
                nodes.forEach((container) => {
                    const frag = document.createDocumentFragment();
                    Array.from(dom.html.childNodes).forEach((n) => frag.appendChild(n.cloneNode(true)));
                    if (action.indexOf("OrReplace") != -1) {
                        var elements = container.querySelectorAll('#' + id);
                        for (let i = 0; elements && i < elements.length; i++) {
                            elements[i].remove();
                        }
                    }

                    if (action.indexOf("append") != -1) {
                        container.append(frag);
                    } else {
                        container.prepend(frag);
                    }
                });
                await this.insertScripts(dom.scripts, id);
                break;
            }

            case "scrollIntoView": {
                if (ctx.options.popstate) {
                    const coords = ctx.options.scroll ?? [0, 0];
                    scrollTo(coords[0], coords[1]);
                    break;
                }
                
                nodes.forEach((el) => {
                    el.scrollIntoView({ behavior: "auto", block: "start" })
                });
                break;
            }

            case "scrollToTop": {
                if (ctx.options.popstate) {
                    const coords = ctx.options.scroll ?? [0, 0];
                    scrollTo(coords[0], coords[1]);
                    break;
                }
                window.scrollTo(0, 0);
                
                break;
            }

            case "focus": {
                nodes.forEach((el) => el.focus());
                break;
            }
            /*
                  case "highlight": {
                    const cls = (a as any).className ?? "plang-highlight";
                    const ms = (a as any).durationMs ?? 800;
                    nodes.forEach((el) => {
                      el.classList.add(cls);
                      setTimeout(() => el.classList.remove(cls), ms);
                    });
                    break;
                  }
            */
            case "show": {
                nodes.forEach((el) => el.style.display = 'block');
                break;
            }

            case "hide": {
                nodes.forEach((el) => el.style.display = 'none');
                break;
            }

            case "showModal": {
                let dialogManager = new DialogManager(this.plang);
                dialogManager.showDialog(targetSel, true);
                this.plang.windowStack.push(dialogManager);
                break;
            }

            case "showDialog": {
                let dialogManager = new DialogManager(this.plang);
                dialogManager.showDialog(targetSel, false);
                this.plang.windowStack.push(dialogManager);
                break;
            }
            
            case "hideModal":
            case "hideDialog": {
                let dialogManager = this.plang.windowStack.pop();
                if (dialogManager) {
                    dialogManager.close();
                }
                break;
            }

            case "notify": {
                //const { message, status, timeout, position } = a;
                (window as any).UIkit?.notification?.({ message: message.content });
                break;
            }

            case "alert": {
                alert(message.content);
                break;
            }
            /*
                  case "badge": {
                    nodes.forEach((el) => el.setAttribute("data-badge", String(env.content)));
                    break;
                  }
            
                  case "vibrate": {
                    if (navigator.vibrate) navigator.vibrate(100);
                    break;
                  }
            */
            case "navigate": {
                if ((ctx.options as any).popstate) return;

                let options = Object.fromEntries(
                    Object.entries(ctx.options).filter(([_, v]) => !(v instanceof HTMLElement))
                );
                let state = { url: ctx.url, options: options } as HistoryStateShape

                this.navigator.setState(state, false)

                break;
            }

            case "replaceState": {
                let state = { url: ctx.url, options: ctx.options } as HistoryStateShape

                this.navigator.setState(state, true);

                break;
            }

            case "reload": {
                location.reload();
                break;
            }
        }
    }

    public registerOverride(action: string, handler: ActionOverrideHandler) {
        this.actionOverrides.set(action, handler);
    }

    public registerOverrides(overrides: Record<string, ActionOverrideHandler>) {
        Object.entries(overrides).forEach(([action, handler]) => {
            this.registerOverride(action, handler);
        });
    }


    private insertedScriptList: string[] = [];

    private async insertScripts(scripts: HTMLScriptElement[], id: string) {
        const copyAttrs = (from: Element, to: Element) => {
            for (const { name, value } of Array.from(from.attributes)) if (name !== "id") to.setAttribute(name, value);
        };

        let counter = 0;
        for (const srcScript of scripts) {
            const hashed = srcScript.src || `${id}-${counter}`;
            document.getElementById(hashed)?.remove();

            const el = document.createElement("script");
            copyAttrs(srcScript, el);
            el.id = hashed;

            try {
                if (srcScript.src) {
                    if (!el.hasAttribute("type") || el.getAttribute("type") === "text/javascript") (el as HTMLScriptElement).async = false;
                    const p = new Promise<void>((resolve, reject) => {
                        (el as HTMLScriptElement).onload = () => resolve();
                        (el as HTMLScriptElement).onerror = (e) => reject(e);
                    });
                    (el as HTMLScriptElement).src = srcScript.src;
                    document.head.appendChild(el);
                    await p;
                } else {
                    if (srcScript.text.indexOf('DOMContentLoaded') != -1) {
                        console.warn('Code wrapped in document.addEventListener("DOMContentLoaded"...., will not run. DOM is already loaded so that event will not run again. Best is to have (() => { // your code })();')
                    }
                    (el as HTMLScriptElement).text = `${srcScript.text}\n//# sourceURL=${id}-${counter}.js`;
                    document.head.appendChild(el);
                    this.insertedScriptList.push(hashed);
                    counter++;
                }
            } catch (e) {
                console.error(e);
                this.plang.reportCaught("injectScript", e);
            }
        }
    }

    private insertStyles(styles: HTMLStyleElement[]) {
        for (const s of styles) {
            try {
                document.head.appendChild(s);
            } catch (e) {
                console.error(e);
                this.plang.reportCaught("injectStyle", e);
            }
        }
    }

}