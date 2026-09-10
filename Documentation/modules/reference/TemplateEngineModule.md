# TemplateEngineModule

Hint: `[templateengine]`  
Type: `PLang.Modules.TemplateEngineModule.Program`

Render template html, files, elements using template engine. plang examples: 
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


