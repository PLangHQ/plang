/**
 * True once a newer version of the app has taken over this tab. The scripts and styles in the tab
 * are the old ones, so the next link the visitor follows is a full page load, which brings in the
 * new ones, instead of a swap inside the old page. The visitor is leaving the page anyway.
 */
export declare function updateIsPending(): boolean;
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
export declare function registerNavigationSigner(tier: string | null | undefined, report: (type: string, err: unknown) => void): Promise<void>;
