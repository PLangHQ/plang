# FilterModule

Hint: `[filter]`  
Type: `PLang.Modules.FilterModule.Program`

Allow user to find text, filter out items, select, query from a %variable% and get specific item from that variable.
```plang
- filter %list% where id=%id%, write to %newList%
```

## Methods

### ExtractByCssSelector

```
ExtractByCssSelector(String html, String cssSelector, String retrieveOneItem = null) : Object
```

Retrieve element(s) from html by a css selector, retrieveOneItem: first|last|number (retrieveOneItem can also be a number representing the index.)


### ExtractElementsFromHtml

```
ExtractElementsFromHtml(String html, List<String> elementNames = null) : PLang.Modules.FilterModule.Program+HtmlNode
```

Extracts all html element matches elementName, e.g. if user want to extract link, elementNames=["a"]


### ExtractFromXPath

```
ExtractFromXPath(String html, String xpath) : Object
```


### ExtractMarkdownWrapping

```
ExtractMarkdownWrapping(String input, String[] format = null) : Object
```

Parses an input that is wrapped with markdown code format and return text inside those code blocks


### FilterOnProperty

```
FilterOnProperty(Object variableToExtractFrom, String propertyToFilterOn, String operatorOnPropertyToFilterOn = =, String retrieveOneItem = null, String operatorToFilterOnValueComparer = insensitive, Boolean throwErrorOnEmptyResult = False, Object defaultValue = null) : Object
```

Use this function when the intent is to filter a list based solely on the property name or pattern of the property name, without needing to match a specific value within that property. 
This function is suitable when the user specifies conditions like "property starts with", "property ends with", or "property contains" without mentioning a value to filter by.

operatorOnPropertyToFilterOn can be: =|!=|startswith|endswith|contains
retrieveOneItem: null|first|last|number (retrieveOneItem can also be a number representing the index.)
operatorToFilterOnValueComparer: insensitive|case-sensitive
can return a list of elements or one element, depending on if retrieveOneItem is set.
throwErrorOnEmptyResult: set to true when user defines retrieveOneItem and on error for key:NotFound og status code: 404 or when user defines so
defaultValue: when defaultValue is defined, the throwErrorOnEmptyResult=false
<example>
- filter %types% where property: %type.Name%, write to %item% => variableToExtractFrom="%types%", propertyToFilterOn="%type.Name%", operatorOnPropertyToFilterOn="="
- filter %list% where property is 'Name' => variableToExtractFrom="%list%", propertyToFilterOn="Name"
- filter %list% where property contains 'Addr', return the first => variableToExtractFrom="%list%", propertyToFilterOn="Name", operatorOnPropertyToFilterOn="contains", retrieveOneItem="first", throwErrorOnEmptyResult=true
</example>


### FilterOnPropertyAndValue

```
FilterOnPropertyAndValue(PLang.Runtime.ObjectValue variableToExtractFrom, String propertyToFilterOn, Object valueToFilterBy, String operatorToFilterOnValue = =, String operatorOnPropertyToFilter = =, String propertyToExtract = null, String retrieveOneItem = null, String operatorToFilterOnValueComparer = insensitive, Boolean throwErrorOnEmptyResult = False, Object defaultValue = null) : Object
```

Use this function when the intent is to filter a list based on both the property name and a specific value within that property. 
This function is appropriate when the user specifies conditions that involve both a property and a value, such as "property is 'Name' and value is 'John'", or when operators on both the property and value are needed.

propertyToFilterOn: required, property of a list to filter on
valueToFilterBy: required, find a specific Value that is stored in the propertyToFilterOn
operatorOnPropertyToFilter can be following(sperated by |): <|>|equals|startswith|endswith|contains.
retrieveOneItem: first|last|retrieveOneItem can also be a number representing the index.
operatorOnPropertyToFilter: equals|startswith|endswith|contains
propertyToExtract: by default it returns the element that matches the property, can be defined as 'parent' or when propertyToExtract is specified it will find that property and return the object from that property.
operatorToFilterOnValue: =|!= 
operatorToFilterOnValueComparer: insensitive|case-sensitive
throwErrorOnEmptyResult: set to true when user defines retrieveOneItem and on error for key:NotFound og status code: 404 or when user defines so
defaultValue: when defaultValue is defined, the throwErrorOnEmptyResult=false
<example>
- filter %files% where "name" contains ".txt", write to %filteredFiles% => variableToExtractFrom=%files%, propertyToFilterOn="name", valueToFilterBy=".txt", operatorToFilterOnValue="contains", operatorOnPropertyToFilter="="
- filter %json% where property starts with "%item%/" and has "John" as value, get parent object, write to %libraries% => variableToExtractFrom="%json%", 
	propertyToFilterOn="%item%/", valueToFilterBy="John", operatorToFilterOnValue="contains", operatorOnPropertyToFilter="startswith", propertyToExtract="parent"
	operatorToFilterOnValueComparer="insensitive"
- filter %list% where property is "Quantity" and is larger then "10", give me first, write to %library% => variableToExtractFrom="%list%", 
	propertyToFilterOn="Quantity", valueToFilterBy="10", operatorToFilterOnValue=">", operatorOnPropertyToFilter="="
	operatorToFilterOnValueComparer="case-sensitive"
	retrieveOneItem="first"
	throwErrorOnEmptyResult=true
</example>

- `variableToExtractFrom` *PLang.Runtime.ObjectValue* — (see Type information in SupportingObjects)
- `propertyToFilterOn` *String*
- `valueToFilterBy` *Object*
- `operatorToFilterOnValue` *String*, default `=`
- `operatorOnPropertyToFilter` *String*, default `=`
- `propertyToExtract` *String*, default `null`
- `retrieveOneItem` *String*, default `null`
- `operatorToFilterOnValueComparer` *String*, default `insensitive`
- `throwErrorOnEmptyResult` *Boolean*, default `False`
- `defaultValue` *Object*, default `null`

### FindTextInContent

```
FindTextInContent(String content, String textToFind, String matching = contains, String retrieveOneItem = null) : Object
```

matching: contains|startwith|endwith|equals. retrieveOneItem: first|last|number (retrieveOneItem can also be a number representing the index.)


### GetItem

```
GetItem(Object variableToExtractFrom, String retrieveOneItem) : Object
```

Gets an item from list, giving the first, last or by index according to user definition. retrieveOneItem: first|last|number (retrieveOneItem can also be a number representing the index.)


### Join

```
Join(List<Object> list = null, String seperator = , , String[] exclude = null) : String
```

Joins a list of items into one string


