export class OverrideNavigator {
    constructor(plang) {
        this.plang = plang;
        this.onClick = (e) => {
            const target = e.target;
            const a = target?.closest("a");
            if (!a)
                return;
            if (this.shouldReturn(e, a))
                return;
            e.preventDefault();
            const options = this.getOptions(a);
            options.method = 'GET';
            this.navigate(a.href, options);
        };
        this.onSubmit = (e) => {
            if (e.defaultPrevented)
                return;
            const f = e.target;
            if (!(f instanceof HTMLFormElement) || f.target)
                return;
            e.preventDefault();
            let url = location.href;
            if (typeof f.action == 'string') {
                url = f.action;
            }
            const method = (f.method || "GET").toUpperCase();
            const data = new FormData(f);
            const options = this.getOptions(f);
            if (e.submitter) {
                const submitter = e.submitter;
                options.submitter = submitter;
                if (submitter.name && submitter.value) {
                    data.set(submitter.name, submitter.value);
                }
            }
            options.body = data;
            options.method = method;
            if (method === "GET") {
                const qs = new URLSearchParams(data).toString();
                const dest = url.split("?")[0] + (qs ? `?${qs}` : "");
                void this.navigate(dest, options);
            }
            else {
                void this.navigate(url, options);
            }
        };
        document.addEventListener("click", this.onClick, true);
        document.addEventListener("submit", this.onSubmit);
    }
    navigate(url, options) {
        const load = this.plang.fetch(url, options);
        return load.then(() => {
        })
            .catch((error) => {
            console.log('error', error);
            this.plang.reportCaught("navigate", error);
        });
    }
    getOptions(element) {
        let beforeActions = '';
        let afterActions = '';
        const headers = [];
        for (const attr of Array.from(element.attributes)) {
            if (!attr.name.startsWith("p-"))
                continue;
            if (attr.name.toLowerCase() == 'p-before-actions') {
                beforeActions = attr.value;
            }
            else if (attr.name.toLowerCase() == 'p-after-actions') {
                afterActions = attr.value;
            }
            else {
                headers.push({ key: attr.name, value: attr.value });
            }
        }
        if (this.errorTarget && !headers.some(h => h.key === 'p-error-target')) {
            var errorElement = document.getElementById(this.errorTarget);
            if (errorElement)
                errorElement.style.display = 'none';
            headers.push({ key: 'p-error-target', value: this.errorTarget });
        }
        return { headers: headers, sourceElement: element, beforeActions: beforeActions, afterActions: afterActions };
    }
    shouldReturn(e, a) {
        // ignore already-handled or modified clicks
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey)
            return true;
        // ignore special anchors
        const href = a.getAttribute("href") ?? "";
        if (!href || href === "#" || href.startsWith('#') || href.startsWith("javascript:"))
            return true;
        if (a.target)
            return true;
        if (a.hasAttribute("download"))
            return true;
        const rel = a.getAttribute("rel") ?? "";
        if (rel == 'external')
            return true;
        // only hijack internal links (same-origin or your domain)
        try {
            const url = new URL(a.href, location.href);
            const isSameOrigin = url.origin === location.origin;
            if (!isSameOrigin)
                return true;
        }
        catch {
            // Not a parseable URL, so it is not ours to intercept. Control flow, not an error.
            return true;
        }
        return false;
    }
    removeErrorTarget() {
        this.errorTarget = undefined;
    }
    setErrorTarget(elementId) {
        this.errorTarget = elementId;
    }
    removeDefaultTarget() {
        this.defaultTarget = undefined;
    }
    setDefaultTarget(elementId) {
        this.defaultTarget = elementId;
    }
}
//# sourceMappingURL=OverrideNavigator.js.map