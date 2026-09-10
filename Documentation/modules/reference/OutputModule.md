# OutputModule

Hint: `[output]`  
Type: `PLang.Modules.OutputModule.Program`

Writes to the output stream. Ask a question with either text or template file. output stream can be to the user(default), system, to different channels such audit|metric|debug|...., and it can have different serialization, text, json, csv, binary, etc.

## Methods

### Ask

```
Ask(PLang.Services.OutputStream.Messages.AskMessage askMessage) : Object
```

Send to a question to the output stream and waits for answer. It always returns and answer will be written into variable

- `askMessage` *PLang.Services.OutputStream.Messages.AskMessage* — (see Type information in SupportingObjects)

Examples:

- ask user template.html, open modal, validate ValidateData, call back data: %id%, write to %result% => Content="template.html", Actor="user", Channel="default", Actions:["showModal"], CallbackData:{id:"%id"}

### SetOutputStream

```
SetOutputStream(String channel, PLang.Models.GoalToCallInfo goalToCall, Dictionary<String, Object> parameters = null) : object
```

- `channel` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `parameters` *Dictionary<String, Object>*, default `null`

### Write

```
Write(PLang.Services.OutputStream.Messages.TextMessage textMessage) : object
```

Write appends by default a text message to the target. User can define different actions, but when it is not defined set as 'append'. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. actor=user|system.

- `textMessage` *PLang.Services.OutputStream.Messages.TextMessage* — (see Type information in SupportingObjects)

### WriteJson

```
WriteJson(PLang.Services.OutputStream.Messages.TextMessage textMessage, PLang.Modules.OutputModule.Program+JsonOptions jsonOptions = null) : object
```

Write out json content. Only choose this method when it's clear user is defining a json output, e.g. `- write out '{name:John}'. Do your best to make sure that TextMessage.Content is valid json. Any %variable% should have double quotes around it. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. actor=user|system, channel=default|trace|debug|info(default for log)|warning|error|audit|metric|security|. User can also define his custom channel

- `textMessage` *PLang.Services.OutputStream.Messages.TextMessage* — (see Type information in SupportingObjects)
- `jsonOptions` *PLang.Modules.OutputModule.Program+JsonOptions*, default `null` — (see Type information in SupportingObjects)

### WriteWithStreamInfo

```
WriteWithStreamInfo(Object content, PLang.Modules.OutputModule.Program+OutputStreamInfo outputStreamInfo) : object
```

- `content` *Object*
- `outputStreamInfo` *PLang.Modules.OutputModule.Program+OutputStreamInfo* — (see Type information in SupportingObjects)

