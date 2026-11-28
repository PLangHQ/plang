# IrohModule

Hint: `[iroh]`  
Type: `PLang.Modules.IrohModule.Program`

Talk to other plang apps over iroh, peer to peer by key and not by ip. Create and print the endpoint key (`create iroh key`), listen for signed commands (`start listening`), connect to a peer by its key (`connect to`) and tell it to run a goal (`tell %peer% to Update`). Listen and connect can bind a fixed UDP port (`on udp port 7000`)

## Methods

### Answer

```
Answer(Object callback, Object answer) : object
```

Send the answer to a question a peer asked (%callback% in the goal of `on ask call`), e.g. `answer %callback% with %answer%`. The peer goes on with its goal from its ask step, with the answer


Examples:

- answer %callback% with %answer% => callback=%callback%, answer=%answer%

### Close

```
Close(String keyName = default) : object
```

Stop listening and close the endpoint of a named key


Examples:

- stop iroh, key 'kiosk' => keyName="kiosk"

### Connect

```
Connect(String target, PLang.Models.GoalToCallInfo onClosed = null, String keyName = default, Boolean useRelay = True, Int32 port = 0, PLang.Models.GoalToCallInfo onIncoming = null, PLang.Models.GoalToCallInfo onOutput = null, PLang.Models.GoalToCallInfo onAsk = null) : PLang.Modules.IrohModule.Program+IrohPeer
```

Connect to a peer by its endpoint id (or a full ticket). The connection is kept under that key and stays open, keeping the app alive, until the peer, the network or `disconnect from <key>` closes it; one connection per key, a connect to a key that is connected returns the open connection. Any run tells the peer by its key: `tell %agentEndpoint% to Snapshot`. onClosed is called when the peer or the network closes it (not when this app disconnects), use it to connect again. With onIncoming the peer can also tell this app on the same connection: every command must be signed, onIncoming is called first and declines by throwing an error, exactly as in `start listening`, then the requested goal runs

- `target` *String*
- `onClosed` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `keyName` *String*, default `default`
- `useRelay` *Boolean*, default `True`
- `port` *Int32*, default `0` — UDP port the endpoint binds, on IPv4 and IPv6, for a firewall rule or a NAT port forward. 0 (the default) takes a free port. The first listen or connect of a key binds it; a later one uses that endpoint as it is
- `onIncoming` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onOutput` *PLang.Models.GoalToCallInfo*, default `null` — The goal called with %message% and %peer% when the peer writes out to this app, `on output call X`. Needs onIncoming (see Type information in SupportingObjects)
- `onAsk` *PLang.Models.GoalToCallInfo*, default `null` — The goal called with %question%, %callback% and %peer% when the peer asks this app, `on ask call X`; it answers with `answer %callback% with %answer%`. Needs onIncoming (see Type information in SupportingObjects)

Examples:

- connect to %kioskKey%, on closed call Redial, write to %agent% => target=%kioskKey%, onClosed=Redial
- connect to iroh peer %id%, key 'admin', write to %agent% => target=%id%, keyName="admin"
- connect to %agentEndpoint%, on udp port 47001, write to %agent% => target=%agentEndpoint%, port=47001
- connect to %agentEndpoint%, on incoming call Allow, on closed call Reconnect, write to %agent% => target=%agentEndpoint%, onIncoming=Allow, onClosed=Reconnect

### CreateKey

```
CreateKey(String name = default, Boolean replace = False) : PLang.Modules.IrohModule.Program+IrohKey
```

Create the iroh endpoint key with a name, saved in settings, and return its endpoint id to print or share. When the key exists it is returned unchanged, unless replace is true. The secret is never returned


Examples:

- create iroh key, write to %key% => name="default"
- create iroh key 'kiosk', replace, write to %key% => name="kiosk", replace=true

### Disconnect

```
Disconnect(PLang.Modules.IrohModule.Program+IrohPeer peer) : object
```

Close the connection to a peer (%peer% from connect or of an incoming command)

- `peer` *PLang.Modules.IrohModule.Program+IrohPeer* — (see Type information in SupportingObjects)

Examples:

- disconnect from %agent% => peer=%agent%

### DisconnectById

```
DisconnectById(String peerId) : object
```

Close the connection to a peer by its key: the endpoint id it was connected to, or the %Identity% of a peer that connected to this app. Nothing happens when it is not connected


Examples:

- disconnect from %agentEndpoint% => peerId=%agentEndpoint%
- close connection %server.endpoint% => peerId=%server.endpoint%

### GetKey

```
GetKey(String name = default) : PLang.Modules.IrohModule.Program+IrohKey
```

Get the endpoint id of a named iroh key. The key is created when it does not exist


Examples:

- get iroh key, write to %key% => name="default"

### Listen

```
Listen(PLang.Models.GoalToCallInfo onIncoming = null, String keyName = default, Boolean useRelay = True, Int32 port = 0, PLang.Models.GoalToCallInfo onClosed = null, PLang.Models.GoalToCallInfo onOutput = null, PLang.Models.GoalToCallInfo onAsk = null) : PLang.Modules.IrohModule.Program+IrohListener
```

Start listening for iroh peers. Every incoming command must be signed, an unsigned or badly signed one is declined and its connection closed. The goal in onIncoming is called first with %Identity% (the signer), %goalName%, %parameters%, %EndpointId% and %peer% (the connection the command came on) (and %fileName% and %fileSize% when a file comes with the command, the file is not received until this goal has not failed), and it decides if the identity is allowed by throwing an error to decline. When it does not fail the requested goal runs, also with %peer% and %Identity%, and what it returns goes back to the caller. An allowed peer's connection is kept under its %Identity% (one connection per identity), so any run can tell it on that connection while it is open: `tell %watcherIdentity% to Ingest`. onClosed is called with %peer% when the peer or the network closes a connection. onIncoming is required

- `onIncoming` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `keyName` *String*, default `default`
- `useRelay` *Boolean*, default `True`
- `port` *Int32*, default `0` — UDP port the endpoint binds, on IPv4 and IPv6, for a firewall rule or a NAT port forward. 0 (the default) takes a free port. The first listen or connect of a key binds it; a later one uses that endpoint as it is
- `onClosed` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onOutput` *PLang.Models.GoalToCallInfo*, default `null` — The goal called with %message% and %peer% when a peer writes out to this app (`write out %x% to %peer%` in the peer), `on output call X` (see Type information in SupportingObjects)
- `onAsk` *PLang.Models.GoalToCallInfo*, default `null` — The goal called with %question%, %callback% and %peer% when a peer asks this app (`ask user "..." to %peer%` in the peer), `on ask call X`. It answers with `answer %callback% with %answer%` (see Type information in SupportingObjects)

Examples:

- start listening, on incoming call Allow, write to %listener% => onIncoming=Allow
- listen on iroh 'kiosk', on incoming call Allow => keyName="kiosk", onIncoming=Allow
- start listening on udp port 7000, on incoming call Allow => onIncoming=Allow, port=7000
- start listening on udp port 47000, on incoming call Allow, on closed call PeerGone, write to %listener% => onIncoming=Allow, onClosed=PeerGone, port=47000

### Tell

```
Tell(PLang.Modules.IrohModule.Program+IrohPeer peer, String goalName, Dictionary<String, Object> parameters = null, String file = null, Int32 timeoutInSeconds = 60, Int32 expiresInSeconds = 120) : Object
```

Tell a connected peer to run a goal in its app. The peer is one from connect, or %peer% of an incoming command (tells it back on the same connection). The command is signed with your identity, the peer decides if it will run it. Returns what the remote goal returns

- `peer` *PLang.Modules.IrohModule.Program+IrohPeer* — (see Type information in SupportingObjects)
- `goalName` *String*
- `parameters` *Dictionary<String, Object>*, default `null`
- `file` *String*, default `null` — Path of a file to send with the command. Its size and sha256 are in the signed command, the peer checks them and gives the goal the saved path as %file%
- `timeoutInSeconds` *Int32*, default `60`
- `expiresInSeconds` *Int32*, default `120`

Examples:

- tell %agent% to Update, version: "1", write to %result% => goalName="Update", parameters={"version":"1"}
- tell %agent% to RestartKiosk, timeout 30 seconds, write to %result% => goalName="RestartKiosk", timeoutInSeconds=30
- tell %agent% to Update, file: %package%, write to %result% => goalName="Update", file=%package%
- tell %peer% to Ingest, seq: %seq%, write to %result% => goalName="Ingest", parameters={"seq":"%seq%"}

### TellById

```
TellById(String peerId, String goalName, Dictionary<String, Object> parameters = null, String file = null, Int32 timeoutInSeconds = 60, Int32 expiresInSeconds = 120) : Object
```

Tell a peer by its key to run a goal in its app, on its open connection: the endpoint id it was connected to (`connect to <key>`), or the %Identity% of a peer that connected to this app. When it is not connected the error key is NotConnected, so connect with on error and retry. Same as tell otherwise

- `peerId` *String*
- `goalName` *String*
- `parameters` *Dictionary<String, Object>*, default `null`
- `file` *String*, default `null` — Path of a file to send with the command. Its size and sha256 are in the signed command, the peer checks them and gives the goal the saved path as %file%
- `timeoutInSeconds` *Int32*, default `60`
- `expiresInSeconds` *Int32*, default `120`

Examples:

- tell %agentEndpoint% to Snapshot, write to %snapshot%, on error NotConnected call Connect, retry 3 times over 1 minute => peerId=%agentEndpoint%, goalName="Snapshot"
- tell %watcherIdentity% to Ingest, seq: %seq%, write to %result% => peerId=%watcherIdentity%, goalName="Ingest", parameters={"seq":"%seq%"}
- tell peer %Identity% to Ingest, write to %result% => peerId=%Identity%, goalName="Ingest"

