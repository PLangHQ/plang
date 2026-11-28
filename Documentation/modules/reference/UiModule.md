# UiModule

Hint: `[ui]`  
Type: `PLang.Modules.UiModule.Program`

Puts content into the page the user is looking at. A render step belongs here when it says where on the page the result goes, naming a cssSelector such as #main or an action such as replace, append, showModal or navigate, e.g. `render 'page.html' to #main`. A render step that captures the result in a variable instead, `write to %html%`, is the template engine module and shows nothing. Also takes any user command and tries to convert it to html, adds, removes and inserts content at a css selector, sets the (default) layout for the UI, and executes javascript.

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


### RenderInMainLayout

```
RenderInMainLayout(String content, String actor = user) : String
```


### RenderTemplate

```
RenderTemplate(PLang.Modules.UiModule.Program+RenderTemplateOptions options) : Object
```

Everything goes inside the single options parameter, and what is rendered goes inside its RenderMessage. Examples:
```plang
- render product.html
  => options={"RenderMessage":{"Content":"product.html"},"IsTemplateFile":true,"RenderToOutputstream":true}
- render frontpage.html, write to %html%
  => options={"RenderMessage":{"Content":"frontpage.html"},"IsTemplateFile":true,"RenderToOutputstream":false}
- render "<p>plain text</p>"
  => options={"RenderMessage":{"Content":"<p>plain text</p>"},"IsTemplateFile":false,"RenderToOutputstream":true}
- append to #list to item.html, scroll to view
  => options={"RenderMessage":{"Content":"item.html","Target":"#list","Actions":["replace","scrollIntoView"]},"IsTemplateFile":true,"RenderToOutputstream":true,"DontRenderMainLayout":true}
- render welcome.html, cssSelector: "#main", action: "replace"
  => options={"RenderMessage":{"Content":"welcome.html","Target":"#main","Actions":["replace"]},"IsTemplateFile":true,"RenderToOutputstream":true,"DontRenderMainLayout":false}
```
Target is null when the step names no selector, and lives inside RenderMessage with Content and Actions.
ReRender defaults to true, the normal behaviour of re-rendering the content.
RenderToOutputstream is true when the step writes the result into no variable.
DontRenderMainLayout turns on the selector the step names, not on whether it names one. #main is the page's main render area and takes false; any other selector takes true.
IsTemplateFile is true when RenderMessage.Content is a file name, which includes a path written out in the step, and false when Content is the text to render.

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

Show an element that is already on the page, by css selector. Set ExecuteMessage.Actions="show". A step that renders a template file into the page is RenderTemplate, even when it names an action such as show or showModal: the action is part of rendering, not a step of its own

- `executeMessage` *PLang.Services.OutputStream.Messages.ExecuteMessage* — (see Type information in SupportingObjects)

### ShowNotification

```
ShowNotification(PLang.Services.OutputStream.Messages.TextMessage textMessage) : object
```

Set TextMessage.Actions="notify"

- `textMessage` *PLang.Services.OutputStream.Messages.TextMessage* — (see Type information in SupportingObjects)

