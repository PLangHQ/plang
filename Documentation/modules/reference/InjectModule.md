# InjectModule

Hint: `[inject]`  
Type: `PLang.Modules.InjectModule.Program`

Dependancy injection

## Methods

### Inject

```
Inject(String type, String pathToDll, Boolean isDefaultOrGlobalForWholeApp = False, String environmentVariable = PLANG_ENV, String environmentVariableValue = null) : object
```

type can be: db, settings, caching, logger, llm, askuser, encryption, archiver. Injection can be for runtime, builder or both.


