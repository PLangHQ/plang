# XmlModule

Hint: `[xml]`  
Type: `PLang.Modules.XmlModule.Program`

## Methods

### AddElement

```
AddElement(Xml.XmlDocument xmlDoc, String nameOfElement, String insertElementInsideElement, Dictionary<String, String> attributeOnElement = null, Dictionary<String, Object> subElements = null) : Xml.XmlDocument
```

Adds an element to xml document. Can include sub elements. subElements diction object is formatted: {"Attributes":{ "Key": string, "Value": string }, "Value":string }

- `xmlDoc` *Xml.XmlDocument* — (see Type information in SupportingObjects)
- `nameOfElement` *String*
- `insertElementInsideElement` *String*
- `attributeOnElement` *Dictionary<String, String>*, default `null`
- `subElements` *Dictionary<String, Object>*, default `null`

