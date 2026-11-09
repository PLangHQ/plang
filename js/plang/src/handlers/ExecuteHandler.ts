import type { HistoryStateShape } from "../navigation/HistoryNavigator.js";
import type { KindHandler, HandleCtx } from "../net/ResponseDispatcher.js";
import type { Plang } from "../Plang.js";
import type { DomMessage, ExecuteMessage } from "../types/messages.js";
import { UtilHelper } from "../util/UtilHelper.js";
import { ActionHandler } from "./ActionHandler.js";
import { BaseKindHandler } from "./BaseKindHandler.js";
import { FrameworkRegistry, type ActionOverrideHandler } from "../actions/ActionOverrides.js";


interface Instruction {
    "selector": string;
    "member": string;
    "value": string;
    "kind": "Property" | "Attribute" | "Style" | "Event",
    "type" : string;
}

export class ExecuteHandler extends BaseKindHandler implements KindHandler<"execute"> {
    kind = "execute" as const;
    plang : Plang;
    actionHandler: ActionHandler;
    
    constructor(plang : Plang) {
        super();

        this.plang = plang;
        this.actionHandler = new ActionHandler(plang);
    }


    async handle(message: ExecuteMessage, ctx: HandleCtx) {
        let data : any = [];
        if (message.data) {
            if (typeof message.data === 'string') {
                data = [message.data];
            } else if (Array.isArray(message.data)) {
                data = [message.data];
            } else {
                data = Object.values(message.data as any)
            }
        }

        const f = this.resolveFunction(message.function, this) || 
                        this.resolveFunction(message.function, window);
        if (f) {
            f.call(this, ...data, message, ctx);

            // An execute message carries actions like every other kind and they were dropped here,
            // so `navigate to "/admin", scroll to top` moved the url and left the page where it
            // was. The function itself is skipped, it has just been called. Two details matter:
            // the render that came before this message is applied without being awaited, so the
            // dom is still settling and scrolling now is undone as the new content lays out, and
            // applyAction reads message.dom, which only a render message carries.
            const actions = (message.actions ?? []).filter((a) => a !== message.function);
            if (actions.length) {
                const domMessage = {
                    ...message,
                    content: "",
                    dom: { html: document.createElement("div"), scripts: [], styles: [], elementId: null }
                } as any;
                requestAnimationFrame(async () => {
                    for (const action of actions) {
                        await this.actionHandler.applyAction(action, domMessage, ctx);
                    }
                });
            }
            return;
        }

        
        console.warn("Missing window function:", message.function);
    } 

    resolveFunction(path: string, context: any = window): Function | null {
        const parts = path.split('.');
        let obj: any = context;

        for (let i = 0; i < parts.length - 1; i++) {
            obj = obj[parts[i]];
            if (!obj) return null;
        }

        const method = obj[parts[parts.length - 1]];
        if (typeof method === 'function') {
            return method.bind(obj);
        }

        return null;
    }
    // Resolves a member name to a framework action (show, hide, replace, append …) when the
    // detected adapter provides one. Returns undefined for ordinary dom members like innerText.
    private resolveAction(member: string): ActionOverrideHandler | undefined {
        if (!member) return undefined;

        const framework = FrameworkRegistry.detectFramework();
        if (!framework) return undefined;

        const overrides = FrameworkRegistry.get(framework)?.getOverrides();
        return overrides ? overrides[member] : undefined;
    }

    async show(message : ExecuteMessage) {
        if (!message.target) {
            console.error('No target defined on message', message);
            return;
        }

        const nodes = document.querySelectorAll<HTMLElement>(message.target);

        // Prefer the adapter's show so it is the exact inverse of hide. Setting style.display
        // alone could never reveal something hide had put away, because hide works by adding the
        // framework's hidden class - show and hide were operating on two different mechanisms.
        const action = this.resolveAction('show');
        if (action) {
            action(nodes, message as unknown as DomMessage, {} as HandleCtx);
            return;
        }

        nodes.forEach(el => (el.style.display = ''));
    }
    async redirect(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx) {
        this.plang.fetch(message.data as string);
    }

    async navigate(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx) {
         if ((ctx.options as any).popstate) return;
        
        let url = message.data;
        // A goal asking to navigate to a url means a plain GET of that url, whatever brought us here.
        // Carrying the current request's options over was wrong twice: setState only pushes when
        // options.method is GET, so a navigate answering a form POST was dropped and the address never
        // changed - and the options also hold that request's target (#booklistResult) and body, so
        // pressing Back later re-fetched the url into a target that no longer exists and the page came
        // back blank.
        let state = { url: url, options: { method: "GET" } } as HistoryStateShape

        this.plang.navigator.setState(state, false)

    }

    async replaceState(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx) {
        if ((ctx.options as any).popstate) return;

        let url = message.data;
        let state = { url: url, options: { method: "GET" } } as HistoryStateShape

        this.plang.navigator.setState(state, true)
    }

    async uiRemove(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx) {

        instructions.forEach(item => {
            const selector = item.selector;
            const nodes = document.querySelectorAll(selector);
            if (!nodes.length) {
                UtilHelper.elementNotFound(selector, message, ctx);
                return;
            }

            nodes.forEach(n => (n.remove()));
        });
    }

    async dialogCommand(closeAll : any) {
        if (!closeAll) {
            let window = this.plang.windowStack.pop();
            if (window) {
                window.close();
            }
        }
    }


    async uiInstruction(instructions: Instruction[], message: ExecuteMessage, ctx: HandleCtx) {
        instructions.forEach(async item => {
            const selector = item.selector;
            const member = item.member
            const value = item.value;
            const kind = item.kind;

            const nodes = document.querySelectorAll<HTMLElement>(selector);
            if (!nodes.length) {
                UtilHelper.elementNotFound(selector, message, ctx);
                return;
            }

            const attrToProp = { checked: "checked", selected: "selected", value: "value" } as any;

            // A member can name an action - show, hide, replace, append and the rest that
            // SetElement's own description advertises - rather than a dom property. Those live in
            // the framework adapters and were unreachable from here, so the instruction fell
            // through to n[member] = value and silently did nothing: "hide" set el.hide = null,
            // and clearing a variable ("replace" with an empty value) never cleared anything.
            const action = this.resolveAction(member);
            if (action) {
                action(nodes, message as unknown as DomMessage, ctx);
                return;
            }

            switch (kind) {
                case "Property":
                    nodes.forEach((n : any) => {
                        const parts = member.split(".");
                        if (parts.length === 2) {
                            (n[parts[0]] as any)[parts[1]](value);
                        } else {
                            if (attrToProp[member]) {
                                n[attrToProp[member]] = value;
                            } else {
                                n[member] = value;
                            }
                        }
                    });
                    break;

                case "Attribute":
                    nodes.forEach((n : any) => {
                        if (item.value == null) {
                            n.removeAttribute(item.member);
                            if (attrToProp[item.member]) {
                                n[attrToProp[item.member]] = attrToProp[item.member] === "value" ? "" : false;
                            }
                        } else {
                            n.setAttribute(item.member, item.value);
                            if (attrToProp[item.member]) n[attrToProp[item.member]] = item.member === "value" ? item.value : !!item.value;
                        }
                    });
                    break;

                case "Style":
                    nodes.forEach((n : any) => (n.style[member] = value));
                    break;

                case "Event": {
                    const handler =
                        typeof value === "function"
                            ? value
                            : typeof value === "string"
                                ? (window as any)[value] || new Function("event", value)
                                : null;
                    if (!handler) return;
                    nodes.forEach(n => n.addEventListener(member, handler));
                    break;
                }
            }
        });
    }

}
