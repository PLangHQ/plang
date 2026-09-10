# CachingModule

Hint: `[caching]`  
Type: `PLang.Modules.CachingModule.Program`

Handles caching of objects, set, get and remove. object can be can be cached with sliding or fixed period

## Methods

### Get

```
Get(String key) : Object
```


### RemoveCache

```
RemoveCache(String key) : object
```


### SetForAbsoluteExpiration

```
SetForAbsoluteExpiration(String key, Object value, Int32 timeInSeconds = 600) : object
```


### SetForSlidingExpiration

```
SetForSlidingExpiration(String key, Object value, Int32 timeInSeconds = 600) : object
```


