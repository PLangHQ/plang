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

## Send a file with the command

```plang
SendUpdate
- tell %agent% to Update, file: %package%, timeout 120 seconds, write to %result%

Update
- write out "Got %file%"
- return "scheduled"
```

The size and sha256 of the file are in the signed command. The receiver calls `on incoming` first (it gets `%fileName%` and `%fileSize%` too) and takes no file bytes until that goal has passed. Then the file is saved under `/.tmp/iroh/<id>/<name>` in the receiving app, its sha256 is checked, and the goal runs with `%file%` set to that path. A file that does not match is deleted and the command is declined. After a successful command the file belongs to the goal, so move it or delete it. Folders older than a day are removed when the app starts listening. The largest file taken is 512 MB, change it with the setting `IrohMaxFileBytes`.

## Wire format

One bidirectional QUIC stream per `tell`, ALPN `plang/tell/1`. The request is a 4 byte big endian header length, the header, then the file bytes when there is a file. The header is JSON `{ "Signature": <signed message>, "Payload": <string> }` where the payload is `{ goal, parameters, created, to, file: { name, size, sha256 } }` and the signature covers the payload text. `to` is the receiver's endpoint id and is checked when the sender connected by id. The file goes in 64 KB chunks, each with a 30 second timeout. The answer is `{ Ok, Result, Error, StatusCode }`. The stream is closed after the answer.

## Limits to know

- The C interface gives no peer endpoint id on an accepted connection, so the receiver authenticates the sender by the plang signature alone.
- `on closed` fires when the connection is closed. A peer that disappears without closing is only noticed after the QUIC idle timeout, so a `tell` that fails is the reliable moment to connect again.
- The signing service refuses a signature older than its expiry and a nonce it has seen, so a replayed command is declined. How long nonces are kept across a restart is not verified.
- A file that is cut off halfway is not resumed, send it again. A 40 MB file took 2 seconds between two processes on one machine, direct, so the speed over a relay is not measured.
- The free n0 relays are for development. Make the relay configurable before relying on it in production.
