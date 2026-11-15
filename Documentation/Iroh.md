# Iroh: tell another plang app what to do

The `iroh` module connects plang apps peer to peer by key, not by ip address. It is built on [iroh](https://iroh.computer) (QUIC, hole punching, relay fallback). Every command is signed with the sender's plang identity, and the receiving app decides who may run what.

## Make a key

An endpoint key is the address of this app. It is created once, saved in the app's settings, and is not the same as the identity key that signs commands.

```plang
PrintKey
- create iroh key, write to %key%
- write out "Send commands to %key.EndpointId%"
```

`create iroh key` returns the existing key unchanged, add `replace` to make a new one. `create iroh key 'kiosk'` makes a second, named key. The secret is never returned.

## Listen

```plang
Start
- start listening, on incoming call Allow, write to %listener%
- keep alive

Allow
- if %Identity% != %Settings.TrustedIdentity% then
    - throw error "Not allowed", 403
```

`on incoming` is required. A command that is not signed, or whose signature is wrong, is declined before your goal is called. The goal gets `%Identity%` (the signer), `%goalName%`, `%parameters%` and `%EndpointId%`. When it throws an error the command is declined, otherwise the requested goal runs with `%Identity%` as a parameter and what it returns goes back to the sender.

## Connect and tell

```plang
Update
- connect to %kioskKey%, on closed call Redial, write to %agent%
- tell %agent% to Update, version: "1", write to %result%

Redial
- connect to %kioskKey%, on closed call Redial, write to %agent%
```

`tell` waits for the answer (60 seconds by default, `timeout 30 seconds` changes it). `%result%` is what the remote goal returns. A declined command is an error in the step, so `on error` works.

## Wire format

One bidirectional QUIC stream per `tell`, ALPN `plang/tell/1`. The request is JSON `{ "Signature": <signed message>, "Payload": <string> }` where the payload is `{ goal, parameters, created, to }` and the signature covers the payload text. `to` is the receiver's endpoint id and is checked when the sender connected by id. The answer is `{ Ok, Result, Error, StatusCode }`. The stream is closed after the answer.

## Limits to know

- The C interface gives no peer endpoint id on an accepted connection, so the receiver authenticates the sender by the plang signature alone.
- `on closed` fires when the connection is closed. A peer that disappears without closing is only noticed after the QUIC idle timeout, so a `tell` that fails is the reliable moment to connect again.
- The signing service refuses a signature older than its expiry and a nonce it has seen, so a replayed command is declined. How long nonces are kept across a restart is not verified.
- The free n0 relays are for development. Make the relay configurable before relying on it in production.
