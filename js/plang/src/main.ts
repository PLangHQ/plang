import { initI18n } from "./i18n/index.js";
import { Plang } from "./Plang.js";

// First statement, before anything can fail. The inline snippet in the layout waits for this
// flag and only speaks up when the module never executed at all. Without it the snippet has to
// guess how long booting may take, which reported healthy pages on slow connections.
(window as any).__plangModuleRan = true;

try {
    await initI18n();
    window.plang = new Plang();
    await window.plang.init();
} catch (e) {
    // One contract for every boot failure: pages listen for plang:failed and never run timers
    // of their own. Rethrown so the reporter's unhandledrejection listener still records it.
    window.dispatchEvent(new CustomEvent("plang:failed", { detail: { reason: "init", error: e } }));
    throw e;
}
