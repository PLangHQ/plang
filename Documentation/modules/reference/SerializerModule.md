# SerializerModule

Hint: `[serializer]`  
Type: `PLang.Modules.SerializerModule.Program`

## Methods

### AddSerializer

```
AddSerializer(String path) : Nullable<Int32>
```


### ConvertToType

```
ConvertToType(PLang.Models.ObjectValue<List<String>> variables, String type) : Object
```

- `variables` *PLang.Models.ObjectValue<List<String>>* — (see Type information in SupportingObjects)
- `type` *String*

### Deserialize

```
Deserialize(IO.Stream stream, String serializer = json) : Object
```

- `stream` *IO.Stream* — (see Type information in SupportingObjects)
- `serializer` *String*, default `json`

### Deserialize

```
Deserialize(Byte[] data, String serializer = json) : Object
```


### Serialize

```
Serialize(Object data, String serializer = json, IO.Stream stream = null) : Byte[]
```

serializer(message_pack|json). User can also define his own

- `data` *Object*
- `serializer` *String*, default `json`
- `stream` *IO.Stream*, default `null` — (see Type information in SupportingObjects)

