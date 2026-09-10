# UiModule

Hint: `[ui]`  
Type: `PLang.Modules.UiModule.Program`

Takes any user command and tries to convert it to html. Add, remove, insert content to css selector. Set the (default) layout for the UI. Execute javascript.

## Methods

### CloseWindow

```
CloseWindow(PLang.Modules.UiModule.Program+DialogCommand dialogCommand) : object
```

- `dialogCommand` *PLang.Modules.UiModule.Program+DialogCommand* — (see Type information in SupportingObjects)

### ExecuteJavascript

```
ExecuteJavascript(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object
```

- `executeMessage` *PLang.Services.OutputStream.Messages.ExecuteMessage* — (see Type information in SupportingObjects)

### Flush

```
Flush() : object
```


### Navigate

```
Navigate(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object
```

Navigate the user to location. Set function="navigate", data="%url%"

- `executeMessage` *PLang.Services.OutputStream.Messages.ExecuteMessage* — (see Type information in SupportingObjects)

### RemoveElement

```
RemoveElement(List<PLang.Modules.UiModule.Program+UiRemove> domRemoves, String actor = user, String channel = default) : object
```

Remove/delete an element by a css selector

- `domRemoves` *List<PLang.Modules.UiModule.Program+UiRemove>* — (see Type information in SupportingObjects)
- `actor` *String*, default `user`
- `channel` *String*, default `default`

### RenderImageToHtml

```
RenderImageToHtml(String path) : Object
```


### RenderTemplate

```
RenderTemplate(PLang.Modules.UiModule.Program+RenderTemplateOptions options) : Object
```

Examples:
```plang
- render product.html => isTemplateFile=true, renderToOutputstream = true
- render frontpage.html, write to %html% => isTemplateFile=true, renderToOutputstream = false
- render "Is this correct file content.html" => isTemplateFile = false, renderToOutputstream = true
- render product.html to #main => renderToOutputstream = true, ReRender=true, Target="#main"
- replace #main with template.html => Target=#main, actions=["replace"], ReRender=true, FileName=template.html, renderToOutputStream= true
- set html of #product to product.html => Target=#product, actions=["replace"], ReRender=true, FileName=product.html, renderToOutputStream= true
- append to #list to item.html, scroll to view => Target=#list, actions=["replace", "scrollIntoView"], ReRender=true, FileName=item.html, renderToOutputStream= true

Target can be null when not defined by user.
Actions: list of action to preform, the default is 'replace'(innerHTML).
ReRender: default is true. normal behaviour is to re-render the content, like user browsing a website
When user doesn't write the return value into any variable, set it as renderToOutputstream=true, or when user defines it.
IsTemplateFile: set as true when RenderMessage.Content looks like a fileName, e.g. %fileName%, %template%, etc. If Content is clearly a text, set as false
```

- `options` *PLang.Modules.UiModule.Program+RenderTemplateOptions* — (see Type information in SupportingObjects)

### ReplaceState

```
ReplaceState(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object
```

Change the url in the address bar without loading it and without adding a history entry, e.g. "navigate to %url%, replace state" or "replace state with %url%". Set function="replaceState", data="%url%"

- `executeMessage` *PLang.Services.OutputStream.Messages.ExecuteMessage* — (see Type information in SupportingObjects)

### SetElement

```
SetElement(List<PLang.Modules.UiModule.Program+UiInstruction> uiInstructions, String actor = user, String channel = default) : object
```

- `uiInstructions` *List<PLang.Modules.UiModule.Program+UiInstruction>* — (see Type information in SupportingObjects)
- `actor` *String*, default `user`
- `channel` *String*, default `default`

### SetFrameworks

```
SetFrameworks(PLang.Modules.UiModule.Program+UiFramework framework) : object
```

- `framework` *PLang.Modules.UiModule.Program+UiFramework* — (see Type information in SupportingObjects)

### SetLayout

```
SetLayout(PLang.Modules.UiModule.Program+LayoutOptions options) : List<LayoutOptions>
```

set the layout for the gui

- `options` *PLang.Modules.UiModule.Program+LayoutOptions* — (see Type information in SupportingObjects)

### SetTargetArea

```
SetTargetArea(String target) : object
```

Target defines where in the UI to write the content, e.g. a cssSelector when ui is html


### ShowElement

```
ShowElement(PLang.Services.OutputStream.Messages.ExecuteMessage executeMessage) : object
```

Set ExecuteMessage.Actions="show"

- `executeMessage` *PLang.Services.OutputStream.Messages.ExecuteMessage* — (see Type information in SupportingObjects)

### ShowNotification

```
ShowNotification(PLang.Services.OutputStream.Messages.TextMessage textMessage) : object
```

Set TextMessage.Actions="notify"

- `textMessage` *PLang.Services.OutputStream.Messages.TextMessage* — (see Type information in SupportingObjects)

