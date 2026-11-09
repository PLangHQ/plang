import { t as defaultT } from "../i18n/index.js";
import type { T } from "../i18n/index.js";
import { KeyStore } from "./KeyStore.js";

// ECDSASigner.ts

type KeyPair = { privateKey: CryptoKey; publicKey: CryptoKey };

export interface SignedMessage {
    type: "ecdsa-sha-256";
    nonce: string;
    created: string;
    expires: string;
    name: string;
    data: { type: "sha-256"; format: "raw"; hash: string };
    contracts: string[];
    headers: Record<string, string>;
    parent: null;
    identity: null;
    signature: string | null;
    jwkIdentity: JsonWebKey | null;
}

export class ECDSASigner {
    // Storage is behind a ladder now. A browser that refuses IndexedDB used to make signing
    // throw, which killed the poll and left the page on a spinner forever. See KeyStore.
    private store = new KeyStore();
    private keyPromise: Promise<KeyPair> | null = null;

    constructor(private t: T = defaultT) {}

    /** Which rung of the storage ladder is in use. Resolved on first touch. */
    async storeTier() {
        return this.store.tier();
    }

    async deleteDb(): Promise<boolean> {
        await this.store.clear();
        this.keyPromise = null;
        return true;
    }

    async getKey(): Promise<KeyPair> {
        // Cached: getKey is called for every signed request, and each call used to open a
        // transaction and re-import the keys.
        if (!this.keyPromise) this.keyPromise = this.loadOrCreate();
        return this.keyPromise;
    }

    private async loadOrCreate(): Promise<KeyPair> {
        const stored = await this.store.get();
        if (stored) {
            const privateKey = await crypto.subtle.importKey(
                "jwk", stored.privateKey, { name: "ECDSA", namedCurve: "P-256" }, true, ["sign"]
            );
            const publicKey = await crypto.subtle.importKey(
                "jwk", stored.publicKey, { name: "ECDSA", namedCurve: "P-256" }, true, ["verify"]
            );
            return { privateKey, publicKey };
        }

        const keyPair = (await crypto.subtle.generateKey(
            { name: "ECDSA", namedCurve: "P-256" }, true, ["sign", "verify"]
        )) as CryptoKeyPair;

        const privateKey = await crypto.subtle.exportKey("jwk", keyPair.privateKey);
        const publicKey = await crypto.subtle.exportKey("jwk", keyPair.publicKey);
        await this.store.put({ privateKey, publicKey });

        return { privateKey: keyPair.privateKey, publicKey: keyPair.publicKey };
    }

    async exportPublicKey(): Promise<JsonWebKey> {
        const { publicKey } = await this.getKey();
        return crypto.subtle.exportKey("jwk", publicKey);
    }

    // Add this method to your ECDSASigner class
    async MyIdentity(): Promise<any> {
        const jwk = await this.exportPublicKey();
        const keyString = `{"crv":"P-256","kty":"EC","x":"${jwk.x}","y":"${jwk.y}"}`;

        const hash = await crypto.subtle.digest(
            "SHA-256",
            new TextEncoder().encode(keyString)
        );

        const identity = Array.from(new Uint8Array(hash))
            .map(b => b.toString(16).padStart(2, "0"))
            .join("");
        return identity;
    }

    async hashBuffer(buf: ArrayBuffer): Promise<string> {
        const hash = await crypto.subtle.digest("SHA-256", buf);
        return Array.from(new Uint8Array(hash))
            .map((b) => b.toString(16).padStart(2, "0"))
            .join("");
    }

    async signRequest(request: Request, name = "default"): Promise<SignedMessage> {
        const { privateKey } = await this.getKey();
        const payload = await request.clone().arrayBuffer();
        const dataHash = await this.hashBuffer(payload);

        const headersObj: Record<string, string> = {};
        request.headers.forEach((v, k) => {
            headersObj[k] = v;
        });

        const now = new Date();
        const msg: SignedMessage = {
            type: "ecdsa-sha-256",
            nonce: crypto.randomUUID(),
            created: now.toISOString(),
            expires: new Date(now.getTime() + 5 * 60_000).toISOString(),
            name,
            data: { type: "sha-256", format: "raw", hash: dataHash },
            contracts: ["C0"],
            headers: headersObj,
            parent: null,
            identity: null,
            signature: null,
            jwkIdentity: null,
        };

        const sig = await crypto.subtle.sign(
            { name: "ECDSA", hash: "SHA-256" },
            privateKey,
            new TextEncoder().encode(JSON.stringify(msg))
        );

        msg.signature = btoa(String.fromCharCode(...new Uint8Array(sig)));
        msg.jwkIdentity = await this.exportPublicKey();
        return msg;
    }
}
