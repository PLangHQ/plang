export type StoredKeys = {
    privateKey: JsonWebKey;
    publicKey: JsonWebKey;
};
export type Tier = "indexeddb" | "localstorage" | "sessionstorage" | "memory";
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
export declare class KeyStore {
    private tierPromise;
    private memory;
    private reported;
    tier(): Promise<Tier>;
    private resolveTier;
    private announce;
    /** Probe by actually opening it. A present global can still throw on use. */
    private indexedDbWorks;
    /** Same reasoning: write and read back, do not trust the global existing. */
    private webStorageWorks;
    get(): Promise<StoredKeys | null>;
    put(keys: StoredKeys): Promise<void>;
    clear(): Promise<void>;
    private openDb;
    private idbGet;
    private idbPut;
    private idbClear;
}
