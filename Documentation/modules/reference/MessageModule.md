# MessageModule

Hint: `[message]`  
Type: `PLang.Modules.MessageModule.Program`

Send and recieve private messages over the network (nostr) to a public key. Get account(public key), set current account for messaging. Get, add or remove the relays messages go through. Not for a database table that happens to be called messages: a step that inserts, selects or updates rows is DbModule, whatever the table is named

## Methods

### AddRelay

```
AddRelay(String relayUrl) : String
```

Add a relay that messages are sent through and listened on, e.g. relay.primal.net or wss://relay.primal.net. Returns the list of relays


### GetPrivateKey

```
GetPrivateKey() : String
```


### GetPublicKey

```
GetPublicKey() : String
```


### GetRelays

```
GetRelays() : String
```


### Listen

```
Listen(PLang.Models.GoalToCallInfo goalName, String contentVariableName = content, String senderVariableName = sender, String eventVariableName = __NosrtEventKey__, Nullable<DateTimeOffset> listenFromDateTime = null, String[] onlyMessageFromSenders = null) : object
```

goalName should be prefixed by ! and be whole word with possible slash(/). The sender of a message is its public key in hex

- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `contentVariableName` *String*, default `content`
- `senderVariableName` *String*, default `sender`
- `eventVariableName` *String*, default `__NosrtEventKey__`
- `listenFromDateTime` *Nullable<DateTimeOffset>*, default `null`
- `onlyMessageFromSenders` *String[]*, default `null`

### RemoveRelay

```
RemoveRelay(String relayUrl) : String
```

Remove a relay from the relays messages are sent through and listened on. Returns the list of relays


### SendEmail

```
SendEmail(PLang.Modules.MessageModule.Program+EmailMessage emailMessage) : Object
```

- `emailMessage` *PLang.Modules.MessageModule.Program+EmailMessage* — (see Type information in SupportingObjects)

### SendPrivateMessage

```
SendPrivateMessage(String content, String receiverPublicKey) : object
```

Send a private message to a public key (npub... or hex)


### SendPrivateMessageToMyself

```
SendPrivateMessageToMyself(String content) : object
```


### SetCurrentAccount

```
SetCurrentAccount(String publicKeyOrName) : object
```


