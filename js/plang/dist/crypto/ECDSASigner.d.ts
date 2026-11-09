import type { T } from "../i18n/index.js";
type KeyPair = {
    privateKey: CryptoKey;
    publicKey: CryptoKey;
};
export interface SignedMessage {
    type: "ecdsa-sha-256";
    nonce: string;
    created: string;
    expires: string;
    name: string;
    data: {
        type: "sha-256";
        format: "raw";
        hash: string;
    };
    contracts: string[];
    headers: Record<string, string>;
    parent: null;
    identity: null;
    signature: string | null;
    jwkIdentity: JsonWebKey | null;
}
export declare class ECDSASigner {
    private t;
    private store;
    private keyPromise;
    constructor(t?: T);
    /** Which rung of the storage ladder is in use. Resolved on first touch. */
    storeTier(): Promise<import("./KeyStore.js").Tier>;
    deleteDb(): Promise<boolean>;
    getKey(): Promise<KeyPair>;
    private loadOrCreate;
    exportPublicKey(): Promise<JsonWebKey>;
    MyIdentity(): Promise<any>;
    hashBuffer(buf: ArrayBuffer): Promise<string>;
    signRequest(request: Request, name?: string): Promise<SignedMessage>;
}
export {};
