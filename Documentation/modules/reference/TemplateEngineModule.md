# TemplateEngineModule

Hint: `[templateengine]`  
Type: `PLang.Modules.TemplateEngineModule.Program`

Run a template file or template text through the template engine and hand the result back to the goal. A render step belongs here when it captures the result in a variable, e.g. `render products.html, write to %html%` or `render file.html into %content%`: nothing is shown to the user, the html becomes a value. A render step that puts the result into the page instead, naming a cssSelector or an action such as replace, append or showModal, is the ui module. A step that starts with `write out` is the output module, which sends content that is already made. plang examples:
```
- render file.html
- render %content% to #main / will render the variable into the element #main
- render products.html, %products%, write to %result%
```

## Methods

### RenderContent

```
RenderContent(String content, String fullPath = null, Dictionary<String, Object> variables = null) : String
```


### RenderFile

```
RenderFile(String path, Dictionary<String, Object> variables = null, Boolean writeToOutputStream = False) : String
```

Render a file path either into a write into value or straight to the output stream when no return variable is defined. Set writeToOutputStream=true when no variable is defined to write into


