# ConvertModule

Hint: `[convert]`  
Type: `PLang.Modules.ConvertModule.Program`

Convert object from one to another. html => md, md => html, string => keyvalue list

## Methods

### ConvertHtmlToPdf

```
ConvertHtmlToPdf(PLang.Modules.ConvertModule.Program+ConvertHtmlToPdfInstruction options) : object
```

converts html to pdf, returns byte array of file if no path is defined

- `options` *PLang.Modules.ConvertModule.Program+ConvertHtmlToPdfInstruction* — (see Type information in SupportingObjects)

### ConvertMdToHtml

```
ConvertMdToHtml(String content, Boolean useAdvancedExtension = True, List<String> markdownPipelineExtensions = null) : String
```


### ConvertToKeyValueList

```
ConvertToKeyValueList(Object variable, String newLineSeperator = 
, String columnSeperator = 	, Boolean trimColumns = True, List<String> headers = null) : Object
```


### ConvertToMd

```
ConvertToMd(Object content, String unknownTags = Bypass, Boolean githubFlavored = True, Boolean removeComments = True, Boolean smartHrefHandling = True, Boolean cleanupUnnecessarySpaces = True, Boolean suppressDivNewlines = True) : String
```

unknownTags=(Bypass|Drop|PassThrough|Raise


