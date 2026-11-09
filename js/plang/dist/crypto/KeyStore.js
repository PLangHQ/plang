import { errorReporter } from "../net/ErrorReporter.js";
const DB_NAME = "ecdsa_keys";
const STORE_NAME = "keys";
const KEY_ID = "signing";
const WEB_KEY = "plang.ecdsa_keys";
/**
 * Where the signing keypair is kept, with a fallback ladder.
 *
 * Why a ladder exists at all: a browser that refuses site data cannot store the keypair, so
 * signing throws, so the poll never connects, so the page renders a spinner and nothing else.
 * Teachers on managed machines hit exactly that and reported the invite link as broken.
 *
 * Why falling back is acceptable: the private key is already generated as **extractable** and
 * written as a plain JWK. It is ordinary same-origin JSON today, readable by any script on
 * the page. localStorage and sessionStorage have precisely the same exposure, so moving
 * between them costs no real security. This would NOT be true of a non-extractable CryptoKey,
 * which can only live in IndexedDB; if the key is ever made non-extractable, this whole file
 * has to go.
 *
 * Cookies are deliberately not a rung. A cookie is sent to the server on every request, which
 * would put the private key on the wire. That is strictly worse than any option here.
 *
 * The last rung is memory, which cannot fail but does not persist: the visitor gets a new
 * identity on every page load. That keeps the site usable rather than dead, and it is a real
 * cost, so the tier in use is reported.
 */
export class KeyStore {
    constructor() {
        this.tierPromise = null;
        this.memory = null;
        this.reported = false;
    }
    async tier() {
        if (!this.tierPromise)
            this.tierPromise = this.resolveTier();
        return this.tierPromise;
    }
    async resolveTier() {
        if (await this.indexedDbWorks())
            return this.announce("indexeddb");
        if (this.webStorageWorks("localStorage"))
            return this.announce("localstorage");
        if (this.webStorageWorks("sessionStorage"))
            return this.announce("sessionstorage");
        return this.announce("memory");
    }
    announce(tier) {
        // Only the degraded rungs are worth a row. Reporting the healthy one on every page
        // load would bury everything else.
        if (tier !== "indexeddb" && !this.reported) {
            this.reported = true;
            errorReporter.report("identityStore", `signing key fell back to ${tier}`);
        }
        return tier;
    }
    /** Probe by actually opening it. A present global can still throw on use. */
    indexedDbWorks() {
        return new Promise(resolve => {
            let done = false;
            const finish = (ok) => { if (!done) {
                done = true;
                resolve(ok);
            } };
            try {
                const req = indexedDB.open(DB_NAME, 1);
                req.onupgradeneeded = () => req.result.createObjectStore(STORE_NAME);
                req.onsuccess = () => { req.result.close(); finish(true); };
                req.onerror = () => finish(false);
                req.onblocked = () => finish(false);
                // Some locked down browsers neither resolve nor error, they simply never call
                // back, which would hang the whole page on an await. Time it out.
                setTimeout(() => finish(false), 3000);
            }
            catch {
                finish(false);
            }
        });
    }
    /** Same reasoning: write and read back, do not trust the global existing. */
    webStorageWorks(which) {
        try {
            const s = window[which];
            const probe = "plang.probe";
            s.setItem(probe, "1");
            const ok = s.getItem(probe) === "1";
            s.removeItem(probe);
            return ok;
        }
        catch {
            return false;
        }
    }
    async get() {
        const tier = await this.tier();
        if (tier === "memory")
            return this.memory;
        if (tier === "indexeddb")
            return this.idbGet();
        const raw = window[tier === "localstorage" ? "localStorage" : "sessionStorage"]
            .getItem(WEB_KEY);
        if (!raw)
            return null;
        try {
            return JSON.parse(raw);
        }
        catch {
            // Corrupt entry is the same situation as no entry: generate a fresh pair rather
            // than leaving the visitor permanently unable to sign.
            return null;
        }
    }
    async put(keys) {
        const tier = await this.tier();
        if (tier === "memory") {
            this.memory = keys;
            return;
        }
        if (tier === "indexeddb")
            return this.idbPut(keys);
        window[tier === "localstorage" ? "localStorage" : "sessionStorage"]
            .setItem(WEB_KEY, JSON.stringify(keys));
    }
    async clear() {
        const tier = await this.tier();
        this.memory = null;
        if (tier === "indexeddb")
            return this.idbClear();
        if (tier !== "memory") {
            window[tier === "localstorage" ? "localStorage" : "sessionStorage"]
                .removeItem(WEB_KEY);
        }
    }
    openDb() {
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, 1);
            req.onupgradeneeded = () => req.result.createObjectStore(STORE_NAME);
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    }
    async idbGet() {
        const db = await this.openDb();
        return new Promise((resolve, reject) => {
            const req = db.transaction(STORE_NAME, "readonly").objectStore(STORE_NAME).get(KEY_ID);
            req.onsuccess = () => resolve(req.result ?? null);
            req.onerror = () => reject(req.error);
        });
    }
    async idbPut(keys) {
        const db = await this.openDb();
        return new Promise((resolve, reject) => {
            const req = db.transaction(STORE_NAME, "readwrite").objectStore(STORE_NAME)
                .put(keys, KEY_ID);
            req.onsuccess = () => resolve();
            req.onerror = () => reject(req.error);
        });
    }
    async idbClear() {
        const db = await this.openDb();
        db.close();
        return new Promise((resolve, reject) => {
            const req = indexedDB.deleteDatabase(DB_NAME);
            req.onsuccess = () => resolve();
            req.onerror = () => reject(req.error);
            req.onblocked = () => resolve();
        });
    }
}
//# sourceMappingURL=KeyStore.js.map