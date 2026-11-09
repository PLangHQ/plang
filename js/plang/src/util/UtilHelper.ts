import type { HandleCtx } from "../net/ResponseDispatcher";
import type { BaseMessage } from "../types/messages";


export class UtilHelper {


    public static elementNotFound(cssSelector: string, message: BaseMessage, ctx: HandleCtx) {

        if (message.debug && message.debug.goal.absolutePath) {
            console.warn(`Could not find element "${cssSelector}" - vscode://file:///${message.debug.goal.absolutePath.replaceAll(' ', '%20')}`);
        } else {
            console.warn(`Could not find element "${cssSelector}"`);
        }
    }

    public static hasContentModification(actions?: string[]) {
        return (actions && (actions.includes('replace') || actions.includes('replaceSelf') ||
            actions.includes('prepend') || actions.includes('append') ||
            actions.includes('prependOrReplace') || actions.includes('appendOrReplace')));
    }

    public static async doDownloadResponse(res: Response) {
        const blob = await res.blob();
        const name = this.getResponseFilename(res) ?? "";
        const a = document.createElement("a");
        const objectUrl = URL.createObjectURL(blob);
        a.href = objectUrl;
        a.download = name;
        a.target = "_blank";
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(objectUrl);
    }

    public static isDownloadResponse(res: Response) {
        const cd = res.headers.get("content-disposition") ?? "";
        if (/attachment/i.test(cd)) return true;
        const ct = (res.headers.get("content-type") ?? "").toLowerCase();
        if (!ct) return false;
        if (ct === "application/octet-stream") return true;
        if (!ct.includes("text") && !ct.includes("json") && !ct.includes("javascript") && !ct.includes("html")) return true;
        return false;
    }

    private static getResponseFilename(res: Response) {
        const cd = res.headers.get("content-disposition");
        if (!cd) return null;

        const star = cd.match(/filename\*\s*=\s*([^']+)''([^;]+)/i);
        if (star) {
            try {
                return decodeURIComponent(star[2]);
            } catch {
                // Malformed filename* header, fall through to the plain form. Control flow.
            }
        }

        const plain = cd.match(/filename\s*=\s*(?:"([^"]+)"|([^;]+))/i);
        return (plain ? (plain[1] ?? plain[2])?.trim() : null) ?? null;
    }



    public static extractHtmlAndScripts(htmlStr: string) {
        const template = document.createElement("template");
        template.innerHTML = htmlStr;

        const container = document.createElement("div");
        container.appendChild(template.content.cloneNode(true));

        const id = container.querySelector("[id]") as HTMLElement | null;
        const scripts: HTMLScriptElement[] = [];
        const styles: HTMLStyleElement[] = [];

        // Eitt onytt eigindanafn ma ekki fella alla teikninguna. HTML sem baradist inn med
        // flaskubaki i nafninu, \"templates, kastadi InvalidCharacterError her og RenderHandler
        // skiladi tha engu: sidan stod eftir half teiknud. Eigindid er nu sleppt og hitt teiknad.
        const copyAttrs = (from: Element, to: Element) => {
            Array.from(from.attributes).forEach((a) => {
                if (to.hasAttribute(a.name)) return;
                try { to.setAttribute(a.name, a.value); } catch { /* onytt nafn, sleppt */ }
            });
        };

        container.querySelectorAll("style").forEach((style) => {
            const newStyle = document.createElement("style");
            copyAttrs(style, newStyle);
            newStyle.textContent = style.textContent;
            styles.push(newStyle);
            style.remove();
        });

        container.querySelectorAll("script").forEach((script) => {
            const newScript = document.createElement("script");
            if (script.src) newScript.src = script.src;
            if (script.type) newScript.type = script.type;
            copyAttrs(script, newScript);
            newScript.textContent = script.textContent;
            scripts.push(newScript);
            script.remove();
        });

        return { html: container, scripts, styles, elementId: id };
    }


    public static async hashObject(obj : any) {
        const json = JSON.stringify(obj);
        const encoder = new TextEncoder();
        const data = encoder.encode(json);
        const hashBuffer = await crypto.subtle.digest('SHA-256', data);
        const hashArray = Array.from(new Uint8Array(hashBuffer));
        const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
        return hashHex;
    }
}