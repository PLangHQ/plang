# WebSocketModule

Hint: `[websocket]`  
Type: `PLang.Modules.WebSocketModule.Program`

## Methods

### Connect

```
Connect(String url, String name = null, Dictionary<String, Object> headers = null, PLang.Models.GoalToCallInfo onMessage = null, PLang.Models.GoalToCallInfo onConnected = null, PLang.Models.GoalToCallInfo onClose = null, PLang.Models.GoalToCallInfo onError = null, Int32 bufferSize = 8192) : Object
```

- `url` *String*
- `name` *String*, default `null`
- `headers` *Dictionary<String, Object>*, default `null`
- `onMessage` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onConnected` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onClose` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onError` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `bufferSize` *Int32*, default `8192`

### Send

```
Send(Object message, String name = null) : object
```


