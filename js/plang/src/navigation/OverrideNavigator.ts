import { updateIsPending } from "../net/NavigationSigner.js";
import type { HandleCtx } from "../net/ResponseDispatcher";
import type { Plang } from "../Plang";
import type { BaseMessage } from "../types/messages";

export type HeaderKV = { key: string; value: string };
export type HeadersLike = HeaderKV[] | null | undefined;
export type NavigatorOptions = {
    method?: "GET" | "POST";
    body?: unknown;
    replace?: boolean;
    headers?: HeadersLike;
    sourceElement?: Element | string | null;
    beforeActions?: string | undefined | null;
    afterActions?: string | undefined | null;
    signal?: AbortSignal | null;
    popstate?: boolean;
    scroll?: [number, number] | null;
    onSuccess?: (message: BaseMessage, ctx: HandleCtx) => Promise<void> | null;
    onFailure?: (message: BaseMessage, ctx: HandleCtx) => Promise<void> | null;
    submitter?: HTMLButtonElement | undefined;
}
export interface Navigable {
    navigate: (
        url: string,
        opts?: NavigatorOptions
    ) => Promise<unknown>;
}

export class OverrideNavigator {
    
    private errorTarget : string | undefined;
    private defaultTarget : string | undefined;

    constructor(private plang: Plang) {
        document.addEventListener("click", this.onClick, true);
        document.addEventListener("submit", this.onSubmit);
    }

    private onClick = (e: MouseEvent): void => {
        const target = e.target as Element | null;
        const a = target?.closest("a");
        if (!a) return;
        if (this.shouldReturn(e, a)) return;

        e.preventDefault();

        const options = this.getOptions(a);
        options.method = 'GET';
        this.navigate(a.href, options);
    };

    private onSubmit = (e: Event): void => {
        if (e.defaultPrevented) return;

        const f = e.target as EventTarget | null;
        if (!(f instanceof HTMLFormElement) || f.target) return;

        e.preventDefault();

        let url = location.href;
        if (typeof f.action == 'string') {
            url = f.action;
        }

        const method = (f.method || "GET").toUpperCase() as "GET" | "POST";
        const data = new FormData(f);
        const options = this.getOptions(f);
        if ((e as any).submitter) {
            const submitter = (e as any).submitter as HTMLButtonElement;
            options.submitter = submitter;

            if (submitter.name && submitter.value) {
                data.set(submitter.name, submitter.value);
            }
        }

        
        options.body = data;
        options.method = method;

        if (method === "GET") {
            const qs = new URLSearchParams(data as any).toString();
            const dest = url.split("?")[0] + (qs ? `?${qs}` : "");
            void this.navigate(dest, options);
        } else {
            void this.navigate(url, options);
        }
    };

    navigate(
        url: string,
        options: NavigatorOptions
    ): Promise<unknown> {
        const load = this.plang.fetch(url, options);

        return load.then(() => {
            })
            .catch((error) => {
                console.log('error', error);
                this.plang.reportCaught("navigate", error);
            });
    }



    private getOptions(element: HTMLElement): NavigatorOptions {
        let beforeActions = '';
        let afterActions = '';
        const headers: HeaderKV[] = [];
        for (const attr of Array.from(element.attributes)) {
            if (!attr.name.startsWith("p-")) continue;

            if (attr.name.toLowerCase() == 'p-before-actions') {
                beforeActions = attr.value;
            } else if (attr.name.toLowerCase() == 'p-after-actions') {
                afterActions = attr.value;
            } else {
                headers.push({ key: attr.name, value: attr.value });
            }
        }

        if (this.errorTarget && !headers.some(h => h.key === 'p-error-target')) {
            var errorElement = document.getElementById(this.errorTarget);
            if (errorElement) errorElement.style.display = 'none';
            headers.push({ key: 'p-error-target', value: this.errorTarget });
        }


        return { headers: headers, sourceElement: element, beforeActions: beforeActions, afterActions: afterActions } as NavigatorOptions;
    }

    private shouldReturn(e: MouseEvent, a: HTMLAnchorElement): boolean {
        // ignore already-handled or modified clicks
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return true;

        // ignore special anchors
        const href = a.getAttribute("href") ?? "";
        if (!href || href === "#" || href.startsWith('#') || href.startsWith("javascript:")) return true;
        if (a.target) return true;
        if (updateIsPending()) return true;
        if (a.hasAttribute("download")) return true;
        const rel = a.getAttribute("rel") ?? "";
        if (rel == 'external') return true;
        // only hijack internal links (same-origin or your domain)
        try {
            const url = new URL(a.href, location.href);
            const isSameOrigin = url.origin === location.origin;
            if (!isSameOrigin) return true;
        } catch {
            // Not a parseable URL, so it is not ours to intercept. Control flow, not an error.
            return true;
        }
        return false;
    }


    public removeErrorTarget() {
       this.errorTarget = undefined;
    }
    public setErrorTarget(elementId: string) {
        this.errorTarget = elementId;
    }

    public removeDefaultTarget() {
       this.defaultTarget = undefined;
    }
    public setDefaultTarget(elementId: string) {
        this.defaultTarget = elementId;
    }

}
