# Library Module

Load external handler libraries at runtime. Extends PLang with custom action handlers from .NET assemblies.

## Actions

### load

Load a handler library from an assembly file.

```plang
- load library 'plugins/MyHandlers.dll'
- load library 'plugins/MyHandlers.dll' namespace 'MyApp.Handlers'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Path | string | yes | Path to the assembly (.dll) file |
| Namespace | string | no | Limit handler discovery to this namespace |

**Returns:** A library record with:

| Property | Description |
|----------|-------------|
| `name` | Assembly name |
| `actions` | Number of actions discovered |

## How It Works

When a library is loaded, PLang scans the assembly for classes decorated with the `[Action]` attribute and registers them with the engine. After loading, the new actions are available to all subsequent steps.

If you specify a namespace, only handlers in that namespace are discovered. This is useful when an assembly contains multiple modules and you only need some of them.

## Example

```plang
Start
- load library 'extensions/EmailHandlers.dll'
- send email to 'user@example.com' subject 'Hello' body 'World'
```

The `send email` step would be handled by an action discovered in the loaded library.
