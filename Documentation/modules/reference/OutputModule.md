# OutputModule

Hint: `[output]`  
Type: `PLang.Modules.OutputModule.Program`

Send a value or text to the user, and ask the user a question with either text or a template file. A step that starts with `write out`, `write to output` or `ask user` belongs here. The content is already made by an earlier step: putting a template or a variable into an element on the page (`render x to #main`) is the template/ui module, not this one. Output stream can be to the user(default), system, to different channels such audit|metric|debug|...., and it can have different serialization, text, json, csv, binary, etc.

## Methods

### Ask

```
Ask(PLang.Services.OutputStream.Messages.AskMessage askMessage) : Object
```

Send to a question to the output stream and waits for answer. It always returns and answer will be written into variable

- `askMessage` *PLang.Services.OutputStream.Messages.AskMessage* — (see Type information in SupportingObjects)

Examples:

- ask user template.html, open modal, validate ValidateData, call back data: %id%, write to %result% => Content="template.html", Actor="user", Channel="default", Actions:["showModal"], CallbackData:{id:"%id"}

### AskTo

```
AskTo(String question, Object to, Dictionary<String, Object> callbackData = null) : Object
```

Ask a question to an iroh peer (%peer% from `connect` or of an incoming command), e.g. `ask user "Allow git push?" to %robbi%, write to %answer%`. The question goes to the peer's `on ask` goal and this goal stops; when the peer answers (`answer %callback% with %answer%`) the goal goes on from this step with the answer in the variable. Variables the goal needs after the answer are listed in callbackData. Not for asking the user of a web request or the console: that is Ask


Examples:

- ask user "Allow Claude to run git push?" to %robbi%, write to %answer% => question="Allow Claude to run git push?", to="%robbi%", ReturnValues = %answer%
- ask "Which branch?" to %peer%, keep %taskId%, write to %branch% => question="Which branch?", to="%peer%", callbackData={"taskId":"%taskId%"}, ReturnValues = %branch%

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

Write out json content. Only choose this method when the step itself writes the json out, e.g. `- write out '{name:John}'`. A step that writes a %variable% is the Write method, whatever that variable happens to hold: `write out %result%` is Write even where an earlier step set %result% to a json object, because the step says to write the variable out and says nothing about json. Do your best to make sure that TextMessage.Content is valid json. Any %variable% should have double quotes around it. statusCode(like http status code) should be defined by user. type=error should have statusCode between 400-599, depending on text. actor=user|system, channel=default|trace|debug|info(default for log)|warning|error|audit|metric|security|. User can also define his custom channel

- `textMessage` *PLang.Services.OutputStream.Messages.TextMessage* — (see Type information in SupportingObjects)
- `jsonOptions` *PLang.Modules.OutputModule.Program+JsonOptions*, default `null` — (see Type information in SupportingObjects)

### WriteTo

```
WriteTo(Object content, Object to) : object
```

Write content to a %variable% that holds where it goes: a process started with `start process` gets it as one line on its standard input. Choose this when the step writes out TO a %variable%, e.g. `write out %json% to %bot%`; `write out %x%` without `to %variable%` is Write. Text is written as it is, any other value as one line of json.


Examples:

- write out %json% to %bot% => content="%json%", to="%bot%"
- write out "hello" to %process% => content="hello", to="%process%"

### WriteWithStreamInfo

```
WriteWithStreamInfo(Object content, PLang.Modules.OutputModule.Program+OutputStreamInfo outputStreamInfo) : object
```

- `content` *Object*
- `outputStreamInfo` *PLang.Modules.OutputModule.Program+OutputStreamInfo* — (see Type information in SupportingObjects)

