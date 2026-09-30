# Settings Module

Persistent key-value settings stored in the system data source. Settings survive across runs — use them for configuration that shouldn't live in files.

## Actions

### get

Retrieve a setting by key.

```plang
- get setting 'apiKey', write to %key%
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Key | `get setting '<key>'` | string | yes | Setting key |

If the setting doesn't exist, PLang will prompt the user to enter a value (returns an `AskError` that triggers the ask flow).

### set

Store a setting.

```plang
- set setting 'apiKey' to 'sk-abc123'
- set setting 'maxRetries' to 3
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Key | `set setting '<key>'` | string | yes | Setting key |
| Value | `to <value>` (`… to 'sk-abc123'`, `… to 3`) | object | no | Value to store |

### remove

Delete a setting.

```plang
- remove setting 'apiKey'
```

**Parameters:**

| Property | How you say it | Type | Required | Description |
|----------|----------------|------|----------|-------------|
| Key | `remove setting '<key>'` | string | yes | Setting key to remove |

## Examples

### API Key Management

```plang
Start
- get setting 'apiKey', write to %key%
- write out 'Using API key: %key%'
```

On first run, PLang prompts the user to enter the API key. On subsequent runs, it's retrieved from storage automatically.

### Configuration with Defaults

```plang
Start
- get setting 'maxRetries', write to %retries%, on error ignore
- if %retries% is null then call SetDefaults
- write out 'Max retries: %retries%'

SetDefaults
- set setting 'maxRetries' to 3
- set %retries% = 3
```
