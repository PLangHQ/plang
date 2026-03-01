# Output Module

Write text to the user. This is the primary way to display information in PLang.

## Actions

### write

Send content to an output channel.

```plang
/ Simple text
- write out 'Hello world'

/ Variable interpolation
- set %name% = 'PLang'
- write out 'Welcome to %name%'

/ Object properties
- set %user% = {name: "John", age: 30}
- write out 'Name: %user.name%, Age: %user.age%'
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Content | object | yes | — | The content to write |
| Actor | string | no | "user" | Which actor to write to |
| Channel | string | no | "default" | Output channel name |

### Asking for Input

The output module also handles user input:

```plang
- ask 'What is your name?', write to %name%
- write out 'Hello, %name%!'
```

## Output Channels

By default, output goes to the console. In a webserver context, it goes to the HTTP response. The channel system routes output to the right destination automatically.
