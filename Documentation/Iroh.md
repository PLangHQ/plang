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

Allow
- if %Identity% != %Settings.TrustedIdentity% then
    - throw error "Not allowed", 403
```

Listening keeps the app running. `on incoming` is required. A command that is not signed, or whose signature is wrong, is declined before your goal is called. The goal gets `%Identity%` (the signer), `%goalName%`, `%parameters%` and `%EndpointId%`. When it throws an error the command is declined, otherwise the requested goal runs with `%Identity%` as a parameter and what it returns goes back to the sender.

## Connect and tell

A connection is kept by the runtime under the peer's key (its endpoint id), not by the goal that made it. Any goal can use it: a scheduled run, a request, an event. Tell the peer by its key, and connect when the tell says there is no connection:

```plang
Update
- tell %kioskKey% to Update, version: "1", timeout 30 seconds,
    on error NotConnected call Connect, retry 3 times, wait 10 seconds between retries,
    write to %result%

Connect
- connect to iroh peer %kioskKey%
```

- `tell` waits for the answer (60 seconds by default, `timeout 30 seconds` changes it). `%result%` is what the remote goal returns. A declined command is an error in the step, so `on error` works.
- `tell` to a key with no open connection fails with the error key `NotConnected`. The first tell makes the connection this way, and so does the first tell after a connection was lost.
- There is one connection per key. A `connect` to a key that is already connected returns the open connection, so connecting again does not dial again.
- An open connection keeps the app running, as a webserver does. No `keep alive` is needed. An app that connects once and has nothing else to do ends only when the connection closes, or with `disconnect from %kioskKey%`.
- The wait between retries is `wait 10 seconds between retries`, or `retry 3 times over 30 seconds`, which spreads the tries over that time (10 seconds between them).

### Push: the peer sends to you

The usual pattern: the app subscribes once, and the peer tells it when it has something. The app must take commands on its own connection (`when a command comes in`), and connect and subscribe again when the connection closes, since no later step would notice.

```plang
Start
- call goal Subscribe

Subscribe
- tell %agentEndpoint% to Subscribe, since: %lastSeq%, timeout 10 seconds,
    on error NotConnected call Connect, retry 100 times, wait 10 seconds between retries

Connect
- connect to iroh peer %agentEndpoint%, when a command comes in run Allow, when the connection closes run Subscribe

Allow
- if %Identity% != %Settings.AgentIdentity% then
    - throw error "Not allowed", 403

Ingest
- write out "got %at%"
```

The peer keeps the subscriber's identity and tells it by that:

```plang
Subscribe
- cache %Identity% for 30 days, key "subscriber"

Push
- get cache "subscriber", write to %subscriber%
- tell peer id %subscriber% to Ingest, at: %Now%, timeout 10 seconds, on error call goal Dropped
```

- A peer that connected to `start listening` is kept under the `%Identity%` that signed its command, once `on incoming` allowed it. `tell %x%` and `tell peer id %x%` both work when `%x%` holds a key or an identity.
- `when the connection closes` runs when the peer or the network ends the connection, not after `disconnect`.
- The connect step can also be written `connect to %agentEndpoint%, on incoming call Allow, on closed call Subscribe`.

### Pull: you ask the peer

When the app asks on a schedule, the connection made by one run is used by the next:

```plang
Start
- every 5 minutes call goal Pull
- start scheduler

Pull
- set %next% = 1
- go through 20 times, call Fetch

Fetch
- tell %agentEndpoint% to Snapshot, seq: %next%, timeout 30 seconds,
    on error NotConnected call Connect, retry 3 times,
    write to %env%
- if %env.next% = "" then
    - call goal NoEnvelope
- set %next% = %env.next%

NoEnvelope
- end goal and 2 levels up

Connect
- connect to iroh peer %agentEndpoint%, on error call goal Unreachable

Unreachable
- end goal and 3 levels up
```

- The loop stops with `end goal`, no flag. `end goal` ends the goal it is in and `N levels up` also ends the N goals that called it. NoEnvelope (0), Fetch (1) and Pull (2) end, so the loop stops. Ending only Fetch would go on with the next item.
- Connect has its own `on error`. Without it, a peer that is not there stops the app. Unreachable ends this pull and the next pull tries again.

### A peer that goes away

A peer that disappears without closing (a crash, a lost network) is noticed after the idle timeout, about 30 seconds.
- A `tell` that is waiting when that happens fails with `NotConnected`, so `on error NotConnected call Connect, retry ...` connects again. A `tell` that only runs past its `timeout` on an open connection fails with a general error.
- `when the connection closes` runs then, so in the push pattern the app subscribes again.

A step can also tell the connection object that `connect ..., write to %agent%` returns: `tell %agent% to X`.

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
- A peer that disappears without closing is only noticed after the QUIC idle timeout (see [A peer that goes away](#a-peer-that-goes-away)).
- A command without an identity is declined and its connection closed. A new connection from the same identity replaces the old one.
- The signing service refuses a signature older than its expiry and a nonce it has seen, so a replayed command is declined. How long nonces are kept across a restart is not verified.
- A file that is cut off halfway is not resumed, send it again. A 40 MB file took 2 seconds between two processes on one machine, direct, so the speed over a relay is not measured.
- The free n0 relays are for development. Make the relay configurable before relying on it in production.
