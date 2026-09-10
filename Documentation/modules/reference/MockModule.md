# MockModule

Hint: `[mock]`  
Type: `PLang.Modules.MockModule.Program`

Mock other modules. Code should start with `mock XXX` where XXX would be the module, each mock needs to call a goal that will perform the mocking
Example
`mock http post http://example.org, call goal ProcessExample` => ModuleType="Namespace.HttpModule", MethodName="post", GoalToCall={Name:ProcessExample}

## Methods

### MockMethod

```
MockMethod(PLang.Modules.MockModule.Program+MockData mockData) : object
```

- `mockData` *PLang.Modules.MockModule.Program+MockData* — (see Type information in SupportingObjects)

