# Module Module

Load and unload external action handler libraries at runtime. Extends PLang with custom modules from .NET assemblies.

## Actions

### add

Load an action handler library from an assembly file.

```plang
- add module 'plugins/MyHandlers.dll'
- add module 'plugins/MyHandlers.dll' namespace 'MyApp.Handlers'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to the assembly (.dll) file |
| Namespace | string | no | Limit handler discovery to this namespace |

**Returns:** A record with:

| Property | Description |
|----------|-------------|
| `name` | Assembly name |
| `actions` | Number of actions discovered |

When a module is loaded, PLang scans the assembly for classes decorated with the `[Action]` attribute and registers them with the engine. After loading, the new actions are available to all subsequent steps.

### remove

Unload a previously loaded module by name.

```plang
- remove module 'MyHandlers'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Name | string | yes | Module name to unregister |

Returns 404 if the module is not found.

## Example

```plang
Start
- add module 'extensions/EmailHandlers.dll'
- send email to 'user@example.com' subject 'Hello' body 'World'
/ The 'send email' step is handled by an action discovered in the loaded module
```
