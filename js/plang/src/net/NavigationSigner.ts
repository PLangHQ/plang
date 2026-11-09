const SCRIPT = "/plang-sw.js";
const UPDATE_EVERY_MS = 5 * 60_000;

/**
 * Registers the worker that signs page navigations (src/sw/plang-sw.ts).
 *
 * It can only read the key from IndexedDB, so a browser whose key lives anywhere else keeps the
 * plain navigation and any earlier registration is removed.
 *
 * The worker is also what tells an open tab that the app was updated: the browser checks the
 * worker script for a change, and a changed script takes over this page at once. Reloading then
 * brings in the new scripts and styles that a tab left open across an update would otherwise keep
 * running against the new server. The reload waits until the tab is in the background, and is
 * skipped when the visitor has typed something into the page that is still there.
 */
export async function registerNavigationSigner(tier: string | null | undefined, report: (type: string, err: unknown) => void) {
    if (!("serviceWorker" in navigator)) return;

    try {
        if (tier !== "indexeddb") {
            for (const registration of await navigator.serviceWorker.getRegistrations()) {
                if (new URL(registration.active?.scriptURL ?? location.href).pathname === SCRIPT) {
                    await registration.unregister();
                }
            }
            return;
        }

        trackEdits();

        let hadController = !!navigator.serviceWorker.controller;
        navigator.serviceWorker.addEventListener("controllerchange", () => {
            if (!hadController) { hadController = true; return; }
            reloadWhenUnnoticed();
        });

        const registration = await navigator.serviceWorker.register(SCRIPT, { scope: "/" });

        let lastCheck = Date.now();
        document.addEventListener("visibilitychange", () => {
            if (document.visibilityState !== "visible" || Date.now() - lastCheck < UPDATE_EVERY_MS) return;
            lastCheck = Date.now();
            registration.update().catch(() => { /* offline, the next visit checks again */ });
        });
    } catch (e) {
        report("navigationSigner", e);
    }
}

// Only fields the visitor changed count. A page fills in many fields itself, a date range or a
// selected option, and those differ from their default value without anyone having typed.
const editedByUser = new Set<Element>();

function trackEdits() {
    const remember = (e: Event) => {
        if (e.isTrusted && e.target instanceof Element) editedByUser.add(e.target);
    };
    document.addEventListener("input", remember, true);
    document.addEventListener("change", remember, true);
}

function hasUnsavedInput(): boolean {
    for (const el of Array.from(editedByUser)) {
        if (!el.isConnected) { editedByUser.delete(el); continue; }
        if (el instanceof HTMLInputElement) {
            if (el.type === "checkbox" || el.type === "radio") { if (el.checked !== el.defaultChecked) return true; continue; }
            if (el.value !== el.defaultValue) return true;
        } else if (el instanceof HTMLTextAreaElement) {
            if (el.value !== el.defaultValue) return true;
        } else if (el instanceof HTMLSelectElement) {
            if (Array.from(el.options).some(o => o.selected !== o.defaultSelected)) return true;
        }
    }
    return false;
}

function reloadWhenUnnoticed() {
    const reload = () => { if (!hasUnsavedInput()) location.reload(); };
    if (document.visibilityState === "hidden") { reload(); return; }
    const onChange = () => {
        if (document.visibilityState !== "hidden") return;
        document.removeEventListener("visibilitychange", onChange);
        reload();
    };
    document.addEventListener("visibilitychange", onChange);
}
