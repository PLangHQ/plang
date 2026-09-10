# InstallModule

Hint: `[install]`  
Type: `PLang.Modules.InstallModule.Program`

Install external utility from url

## Methods

### InstallFromUrl

```
InstallFromUrl(PLang.Modules.HttpModule.Program+HttpRequest request) : object
```

Install external app(python, bash, go, etc.) from a url, such as github. It uses the README to install and creates examples at build time.

- `request` *PLang.Modules.HttpModule.Program+HttpRequest* — (see Type information in SupportingObjects)

