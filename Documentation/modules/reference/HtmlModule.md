# HtmlModule

Hint: `[html]`  
Type: `PLang.Modules.HtmlModule.Program`

Parse html text, extract from css selectors

## Methods

### ClearHtml

```
ClearHtml(String html) : String
```

Clear html from string


### Extract

```
Extract(String html, String cssSelector) : PLang.Runtime.ObjectValue
```

Extracts html content based on css selector


### ExtractForm

```
ExtractForm(PLang.Modules.HtmlModule.Program+ExtractFormParameters parameters) : Object
```

Extract a form element with all its inputs. CSS3 selectors are available

- `parameters` *PLang.Modules.HtmlModule.Program+ExtractFormParameters* — (see Type information in SupportingObjects)

Examples:

- extract form#login from %html%, write to %form% => Parameters.Html=%html%, Parameters.CssSelector="form#login"
- extract form from %html%, write to %form% => Parameters.Html=%html%, Parameters.CssSelector="form"

### ExtractSelect

```
ExtractSelect(PLang.Modules.HtmlModule.Program+ExtractSelectParameters parameters) : Object
```

Extract a select element's options into a structured list. CSS3 selectors are available

- `parameters` *PLang.Modules.HtmlModule.Program+ExtractSelectParameters* — (see Type information in SupportingObjects)

Examples:

- extract select#country from %html%, write to %countries% => Parameters.Html=%html%, Parameters.CssSelector="select#country"
- extract select.user-role from %html%, write to %roles% => Parameters.Html=%html%, Parameters.CssSelector="select.user-role"

### ExtractTable

```
ExtractTable(PLang.Modules.HtmlModule.Program+ExtractTableParameters parameters) : Object
```

Extract an HTML table into a structured table object. Css3 selectors are availabe. FirstRowIsHeader is false by default

- `parameters` *PLang.Modules.HtmlModule.Program+ExtractTableParameters* — (see Type information in SupportingObjects)

Examples:

- extract table#users from %html%, first row is header, write to %users% => Parameters.Html=%html%, Parameters.CssSelector="table#users", Parameters.FirstRowIsHeader=true
- extract second .employeetable from %html%, write to %employees% => Parameters.Html=%html%, Parameters.CssSelector=".employeetable", Parameters.FirstRowIsHeader=false
- extract second .employeetable from %html%, first row is header, write to %employees% => Parameters.Html=%html%, Parameters.CssSelector=".employeetable", Parameters.Index=1, Parameters.FirstRowIsHeader=true
- extract last table from %html%, write to %table% => Parameters.Html=%html%, Parameters.CssSelector="table", Parameters.Index=-1

