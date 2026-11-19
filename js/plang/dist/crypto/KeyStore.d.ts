/**
 * The private key is a non-extractable CryptoKey where it is kept as one (IndexedDB, memory), and a
 * JWK where only text can be kept (localStorage, sessionStorage). The public key is always a JWK.
 */
export type StoredKeys = {
    privateKey: CryptoKey | JsonWebKey;
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
 * What each rung costs: in IndexedDB (and in memory) the private key is a non-extractable
 * CryptoKey. A script on the page can sign with it while it runs, but cannot read the key and
 * take it away. localStorage and sessionStorage hold only text, so on those rungs the private
 * key is a JWK that any script on the page can read. That is the price of a browser that
 * refuses IndexedDB but keeps web storage, and it is still better than a new identity on every
 * page load. A key is never moved down the ladder: a JWK is only written on the web storage
 * rungs, for a key generated there.
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
