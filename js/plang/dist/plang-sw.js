"use strict";
// Signs page navigations with the visitor's identity.
//
// The browser holds the identity (see KeyStore.ts) and a classic page load, a link from a mail or a
// Back on a phone, is a plain GET that carries none. The server can then not tell who is asking,
// which is why an app had to send the page as a shell and fill it in once the poll had connected.
// This worker answers each navigation itself: it reads the signing key from IndexedDB, signs the
// request the way Plang.fetch does, and returns the server's answer. The page arrives for the right
// person on the first request, at the address that was asked for.
//
// Anything unusual, no key yet, no IndexedDB, a failed signature, leaves the navigation to the
// browser, so a visitor without an identity gets exactly what they got before this worker existed.
//
// Same constants as crypto/KeyStore.ts. This file is a classic script with no imports so it can be
// served as one file from the root of the site, which is what gives it the whole site as its scope.
const sw = self;
const DB_NAME = "ecdsa_keys";
const STORE_NAME = "keys";
const KEY_ID = "signing";
const SIGNATURE_LIFETIME_MS = 5 * 60_000;
let signingKey = null;
function readStoredKeys() {
    return new Promise((resolve, reject) => {
        const open = indexedDB.open(DB_NAME, 1);
        open.onupgradeneeded = () => open.result.createObjectStore(STORE_NAME);
        open.onerror = () => reject(open.error);
        open.onsuccess = () => {
            const db = open.result;
            const get = db.transaction(STORE_NAME, "readonly").objectStore(STORE_NAME).get(KEY_ID);
            get.onsuccess = () => { db.close(); resolve(get.result ?? null); };
            get.onerror = () => { db.close(); reject(get.error); };
        };
    });
}
function loadKey() {
    if (!signingKey) {
        signingKey = (async () => {
            const stored = await readStoredKeys();
            if (!stored)
                return null;
            const privateKey = await crypto.subtle.importKey("jwk", stored.privateKey, { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
            return { privateKey, publicKey: stored.publicKey };
        })();
        // A missing key is looked at again on the next navigation: the page creates it on its
        // first visit, after this worker may already be running.
        signingKey.then(key => { if (!key)
            signingKey = null; }, () => { signingKey = null; });
    }
    return signingKey;
}
async function sha256Hex(buf) {
    const hash = await crypto.subtle.digest("SHA-256", buf);
    return Array.from(new Uint8Array(hash)).map(b => b.toString(16).padStart(2, "0")).join("");
}
// The same message ECDSASigner.signRequest builds. A navigation has no body.
async function signHeaders(headers) {
    const key = await loadKey();
    if (!key)
        return null;
    const now = new Date();
    const msg = {
        type: "ecdsa-sha-256",
        nonce: crypto.randomUUID(),
        created: now.toISOString(),
        expires: new Date(now.getTime() + SIGNATURE_LIFETIME_MS).toISOString(),
        name: "default",
        data: { type: "sha-256", format: "raw", hash: await sha256Hex(new ArrayBuffer(0)) },
        contracts: ["C0"],
        headers,
        parent: null,
        identity: null,
        signature: null,
        jwkIdentity: null,
    };
    const signature = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key.privateKey, new TextEncoder().encode(JSON.stringify(msg)));
    msg.signature = btoa(String.fromCharCode(...new Uint8Array(signature)));
    msg.jwkIdentity = key.publicKey;
    return msg;
}
async function answerNavigation(request) {
    try {
        const accept = request.headers.get("accept") ?? "text/html";
        const signed = await signHeaders({ accept });
        if (!signed)
            return fetch(request);
        // manual: a redirect is the server's answer and the browser is the one to follow it, so
        // the address bar and the history see it as they always did.
        return await fetch(request.url, {
            method: "GET",
            headers: { accept, "X-Signature": JSON.stringify(signed) },
            redirect: "manual",
            credentials: request.credentials,
            cache: request.cache === "only-if-cached" ? "default" : request.cache,
            referrer: request.referrer,
            referrerPolicy: request.referrerPolicy,
        });
    }
    catch {
        return fetch(request);
    }
}
sw.addEventListener("install", () => { void sw.skipWaiting(); });
sw.addEventListener("activate", (event) => { event.waitUntil(sw.clients.claim()); });
sw.addEventListener("fetch", (event) => {
    const request = event.request;
    if (request.mode !== "navigate" || request.method !== "GET")
        return;
    event.respondWith(answerNavigation(request));
});
