# WebserverModule

Hint: `[webserver]`  
Type: `PLang.Modules.WebserverModule.Program`

Start webserver, add route, set certificate, read/write to Header, Cookie, send file to client

## Methods

### AddRoute

```
AddRoute(String path, List<PLang.Modules.WebserverModule.Program+ParamInfo> pathParameters, PLang.Models.GoalToCallInfo goalToCall, PLang.Modules.WebserverModule.Program+RequestProperties requestProperties = null, PLang.Modules.WebserverModule.Program+ResponseProperties responseProperties = null) : object
```

Add route to webserver. When goalToCall is null, use the path parameter in the response to created instance of goalToCall using the path paramter as GoalToCallInfo.Name

- `path` *String*
- `pathParameters` *List<PLang.Modules.WebserverModule.Program+ParamInfo>* — (see Type information in SupportingObjects)
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `requestProperties` *PLang.Modules.WebserverModule.Program+RequestProperties*, default `null` — (see Type information in SupportingObjects)
- `responseProperties` *PLang.Modules.WebserverModule.Program+ResponseProperties*, default `null` — (see Type information in SupportingObjects)

Examples:

- add route /admin, call /admin/Overview => path="/admin", pathParameters=[], goalToCall={"Name":"/admin/Overview"}, requestProperties=null
- add route "/admin/crm/vendor/%id%(number)", [get, post], call /admin/crm/Vendor => path="/admin/crm/vendor/%id%(number)", pathParameters=[{"Name":"id","VariableOrValue":"%id%","Type":"number"}], goalToCall={"Name":"/admin/crm/Vendor"}, requestProperties={"Methods":["GET","POST"]}
- add route /admin/idea, post, max content length 8mb, call /admin/Idea => path="/admin/idea", pathParameters=[], goalToCall={"Name":"/admin/Idea"}, requestProperties={"Methods":["POST"],"MaxContentLengthInBytes":8388608}
- add route /zoo/feed, content type application/json, call /zoo/Feed => path="/zoo/feed", pathParameters=[], goalToCall={"Name":"/zoo/Feed"}, responseProperties={"ContentType":"application/json"}
- add route /shop/%sku%/review, call /shop/Review productSku=%sku% => path="/shop/%sku%/review", pathParameters=[{"Name":"sku","VariableOrValue":"%sku%"}], goalToCall={"Name":"/shop/Review","Parameters":{"productSku":"%sku%"}}

### DeleteCookie

```
DeleteCookie(String name) : object
```


### GetCookie

```
GetCookie(String name) : Object
```


### GetCookieRaw

```
GetCookieRaw(String name) : String
```


### GetNumberOfLiveConnections

```
GetNumberOfLiveConnections(Int32 lastUpdatedInSeconds = 0) : Int64
```


### GetRequestHeader

```
GetRequestHeader(String key) : String
```


### GetRequestMetrics

```
GetRequestMetrics(String since = null, Int32 windowMinutes = 60) : Dictionary<String,Object>
```


### GetUserIp

```
GetUserIp(String headerKey = null) : String
```

Returns the ip address of the connection. headerKey should be null unless specified by user (e.g. X-Forwarded-For when the app runs behind a proxy); then the value of that header is returned


### Redirect

```
Redirect(String url, Boolean permanent = False, Boolean preserveMethod = False) : object
```


### RestartWebserver

```
RestartWebserver(String webserverName = default) : PLang.Modules.WebserverModule.Program+WebserverProperties
```


### SendFileToUser

```
SendFileToUser(String path, String fileName = null, String contentType = null, String actor = user, String channel = default) : object
```


Examples:

- send 'file.pdf' to user => path=file.pdf
- send 'document.docx', name="custom file.docx" => path=document.docx, fileName="custom file.docx"

### SendToWebSocket

```
SendToWebSocket(Object data, Dictionary<String, Object> headers = null, String webSocketName = default) : object
```


### SendToWebSocket

```
SendToWebSocket(PLang.Models.GoalToCallInfo goalToCall, Dictionary<String, Object> parameters = null, String webSocketName = default) : object
```

- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `parameters` *Dictionary<String, Object>*, default `null`
- `webSocketName` *String*, default `default`

### SetCertificate

```
SetCertificate(String permFilePath, String privateKeyFile = null) : object
```


### SetSelfSignedCertificate

```
SetSelfSignedCertificate() : object
```


### ShutdownWebserver

```
ShutdownWebserver(String webserverName) : PLang.Modules.WebserverModule.Program+WebserverProperties
```


### StartWebserver

```
StartWebserver(PLang.Modules.WebserverModule.Program+WebserverProperties webserverProperties) : PLang.Modules.WebserverModule.Program+WebserverProperties
```

- `webserverProperties` *PLang.Modules.WebserverModule.Program+WebserverProperties* — (see Type information in SupportingObjects)

### StartWebSocketConnection

```
StartWebSocketConnection(String url, PLang.Models.GoalToCallInfo goalToCall, String webSocketName = default, String contentRecievedVariableName = %content%) : PLang.Modules.WebserverModule.Program+WebSocketInfo
```

- `url` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `webSocketName` *String*, default `default`
- `contentRecievedVariableName` *String*, default `%content%`

### StreamFile

```
StreamFile(PLang.Services.OutputStream.Messages.StreamMessage streamMessage, Int64 startByte = 0, Nullable<Int64> endByte = null) : object
```

- `streamMessage` *PLang.Services.OutputStream.Messages.StreamMessage* — (see Type information in SupportingObjects)
- `startByte` *Int64*, default `0`
- `endByte` *Nullable<Int64>*, default `null`

### WriteCookie

```
WriteCookie(String name, String value, Int32 expiresInSeconds = 604800) : object
```


### WriteToResponseHeader

```
WriteToResponseHeader(Dictionary<String, Object> headers = null) : object
```


### WriteVariablesToCookie

```
WriteVariablesToCookie(String name, List<PLang.Runtime.ObjectValue> values, Int32 expiresInSeconds = 604800) : object
```

- `name` *String*
- `values` *List<PLang.Runtime.ObjectValue>* — (see Type information in SupportingObjects)
- `expiresInSeconds` *Int32*, default `604800`

