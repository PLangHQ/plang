# ListDictionaryModule

Hint: `[listdictionary]`  
Type: `PLang.Modules.ListDictionaryModule.Program`

get first|last|random|position| item from list or dictionary.
Add, update, delete and retrieve list or dictionary. Group by key, merge two lists

## Methods

### AddItemsToDictionary

```
AddItemsToDictionary(String key, Dictionary<String, Object> value = null, Dictionary<String, Object> dictionaryInstance = null, Boolean updateIfExists = True) : String
```

Method always returns instance of dictionaryInstance, it creates a new instance if it is null. ReturnValue should always be used with AddItemsToDictionary


### AddItemsToList

```
AddItemsToList(List<Object> value = null) : List<Object>
```

Method always returns instance of a list, ReturnValues with the list name should always be used


### AddToDictionary

```
AddToDictionary(String key, Object value, Dictionary<String, Object> dictionaryInstance = null, Boolean updateIfExists = True) : Dictionary<String,Object>
```

Method always returns instance of dictionaryInstance, it creates a new instance if it is null. ReturnValue should always be used with AddToDictionary


### AddToList

```
AddToList(Object value, List<Object> listInstance = null, Boolean uniqueValue = False, Boolean caseSensitive = False) : Object
```

Method always returns instance of listInstance, it creates a new instance if it is null. ReturnValue MUST always be defined


### ClearDictionary

```
ClearDictionary(Dictionary<String, Object> dictionary = null) : object
```


### ClearList

```
ClearList(List<Object> listInstance = null) : object
```


### DeleteFromList

```
DeleteFromList(Object item, List<Object> listInstance = null) : Boolean
```


### DeleteKeyFromDictionary

```
DeleteKeyFromDictionary(String key, Dictionary<String, Object> dictionary = null) : Boolean
```


### GetFromDictionary

```
GetFromDictionary(String key, Dictionary<String, Object> dictionaryInstance = null) : Object
```

Gets an object from dictionary. ReturnValue should always be used with GetFromDictionary


### GetFromList

```
GetFromList(Int32 position, List<Object> listInstance = null) : Object
```

Gets an item from a list by position


### GetItem

```
GetItem(String operator = first, List<Object> listInstance = null, List<String> sortColumns = null, List<String> sortOperator = null) : Object
```


### GroupBy

```
GroupBy(Object obj, String key) : Object
```

Group a %variable% by key


### MergeLists

```
MergeLists(Object list1, Object list2, String key) : Object
```

Merges two objects or lists according to primary key


