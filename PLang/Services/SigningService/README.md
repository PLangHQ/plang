# SignedMessage: Cryptographic Signature Envelope (for plang)

## **Purpose**

`SignedMessage` is a canonical JSON envelope for digital signatures in the plang ecosystem.
It ensures platform-independent, secure, and verifiable transport of signed data across languages (including plang, C#, and JavaScript).

---

## **General Rules**

* **All properties are required**, even if their value is `null`.
* JSON (de)serialization **must always include every property**, even if it is `null`.
* **Property names** are always **camelCase**.
* **Property order** is fixed and must be preserved during (de)serialization for signature consistency.
* The **signature** is computed over the JSON object with all properties (in order), **excluding** the `signature` property itself.
* The signature is always base64-encoded.

---

## **Base Class: `SignedMessage`**

| Property  | Type                   | Description / Usage                                              |
| --------- | ---------------------- | ---------------------------------------------------------------- |
| type      | string                 | Signature algorithm. E.g. `"Ed25519"` or `"ecdsa-sha-256"`.      |
| nonce     | string                 | Random GUID string to prevent replay attacks.                    |
| created   | ISO 8601 datetime      | Creation timestamp in UTC (e.g. `"2025-07-01T12:00:00Z"`).       |
| expires   | ISO 8601 datetime/null | Expiry timestamp in UTC, or `null` if not set.                   |
| name      | string/null            | Application-specific identifier, or `null`.                      |
| data      | any/null               | Main signed payload, or `null`.                                  |
| contracts | array of string        | List of contract/authority tags. Must be present, e.g. `["C0"]`. |
| headers   | object/null            | Metadata headers as key-value pairs, or `null`.                  |
| parent    | SignedMessage/null     | Reference to a parent message (for chaining), or `null`.         |
| identity  | string/null            | Key fingerprint or identifier, or `null`.                        |
| signature | string/null            | Base64 signature (computed after signing).                       |

---

## **Subclass: `SignedMessageJwkIdentity`**

* Inherits all fields from `SignedMessage`.
* Adds:

| Property    | Type   | Description                                                                         |
| ----------- | ------ | ----------------------------------------------------------------------------------- |
| jwkIdentity | object | JSON Web Key (JWK) public key for signature verification. Required, even if `null`. |

---

## **Serialization Example**

```json
{
  "type": "Ed25519",
  "nonce": "c298bb59-0ef0-4fd8-9f62-1e1e364c0b85",
  "created": "2025-07-01T12:00:00Z",
  "expires": "2025-07-01T12:05:00Z",
  "name": null,
  "data": { "payload": "test" },
  "contracts": ["C0"],
  "headers": null,
  "parent": null,
  "identity": null,
  "signature": "BASE64_SIGNATURE"
}
```

**If using JWK:**

```json
{
  "type": "ecdsa-sha-256",
  "nonce": "b701b2eb-2e62-4ea3-b331-f0a22233b5de",
  "created": "2025-07-01T12:00:00Z",
  "expires": "2025-07-01T12:05:00Z",
  "name": null,
  "data": { "payload": "test" },
  "contracts": ["C0"],
  "headers": null,
  "parent": null,
  "identity": null,
  "signature": "BASE64_SIGNATURE",
  "jwkIdentity": {
    "crv": "P-256",
    "ext": true,
    "key_ops": ["verify"],
    "kty": "EC",
    "x": "....",
    "y": "...."
  }
}
```

---

## **Signature Workflow**

1. **Build the message object** with all fields, setting `signature` to `null`.
2. **Serialize the object to canonical JSON** (all properties present, in defined order, camelCase, explicit `null`s).
3. **Sign the JSON bytes** (excluding the `signature` field).
4. **Encode the signature as base64** and set it in the `signature` property.
5. **Transmit the full object** (now including the base64 signature).

---

## **Verification Workflow**

1. **Parse the JSON object.**
2. **Remove the `signature` property**.
3. **Serialize the object** (same rules as above: order, casing, explicit nulls).
4. **Verify the signature** using the public key (`jwkIdentity` or other method, depending on `type`).

---

## **Cross-Language Interop Notes**

* **No property may be omitted** (even if `null`).
* **No extra properties** should be present.
* **No array or object property may be missing or reordered**.
* **Datetime values must be in ISO 8601 UTC** format.

---

## **Best Practices**

* Document and enforce the fixed property order and naming in all consuming languages.
* If extending, always append new properties at the end and set default values to `null` unless required.
* If using `SignedMessageJwkIdentity`, always include the `jwkIdentity` property (never omit).

---

## **Summary**

This format ensures any system (including plang) can serialize, sign, transmit, verify, and parse messages in a secure, predictable, and platform-agnostic way.
Every property is present, every time, for both signing and verification.

