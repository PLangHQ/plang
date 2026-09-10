# MessageModule

Hint: `[message]`  
Type: `PLang.Modules.MessageModule.Program`

Send and recieve private messages. Get account(public key), set current account for messaging

## Methods

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

goalName should be prefixed by ! and be whole word with possible slash(/)

- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `contentVariableName` *String*, default `content`
- `senderVariableName` *String*, default `sender`
- `eventVariableName` *String*, default `__NosrtEventKey__`
- `listenFromDateTime` *Nullable<DateTimeOffset>*, default `null`
- `onlyMessageFromSenders` *String[]*, default `null`

### SendEmail

```
SendEmail(PLang.Modules.MessageModule.Program+EmailMessage emailMessage) : Object
```

- `emailMessage` *PLang.Modules.MessageModule.Program+EmailMessage* — (see Type information in SupportingObjects)

### SendPrivateMessage

```
SendPrivateMessage(String content, String receiverPublicKey) : object
```


### SendPrivateMessageToMyself

```
SendPrivateMessageToMyself(String content) : object
```


### SetCurrentAccount

```
SetCurrentAccount(String publicKeyOrName) : object
```


