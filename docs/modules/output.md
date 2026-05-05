# Output Module

Write text to the user, and ask the user for input. Together with `variable`, this is how a goal communicates with whoever's running it.

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

### ask

Ask the actor a question. PLang treats every ask as a *suspend point* — the goal pauses, the question goes out on the channel, and execution resumes when the answer comes back.

```plang
/ Basic ask — suspend, wait for answer, write to a variable
- ask 'What is your name?', write to %name%
- write out 'Hello, %name%!'
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Question | string | yes | — | The question shown to the user |
| Variables | list | no | empty | Names of variables that survive the suspend (see below) |

#### Carrying state across the suspend — `vars:`

Use `vars:` to name the variables whose values must be available when the answer comes back. Without `vars:`, the resumed run sees a fresh App and only the answer itself crosses the boundary.

```plang
/ Capture %userId% so it's still in scope when the answer arrives
- set %userId% = 42
- ask 'What is your name?', vars: %userId%, write to %name%
- write out 'User %userId% is %name%'
```

The named variables are captured into an `AskCallback` envelope at issue time. When the actor answers, the callback is verified (signed + tamper-checked), the captured variables are bound back into scope, and the answer is delivered to the `write to %x%` target on the same step. PLang signs and verifies the envelope for you — tampered or unsigned envelopes never resume.

#### How resume works under the hood

The PLang runtime issues an `AskCallback` carrying the position, actor, and the named variables. The host channel ships it as `application/plang+data`, holds it until the answer arrives, and replays it through `- run %callback%` (`callback.run`). The runtime verifies the signature, restores the variables, and re-dispatches the same ask step — this time short-circuiting through a sentinel so the question is *not* asked twice. The answer flows through the original step's `write to %x%` target.

You don't write `callback.run` yourself for an ask — the channel handles it. You'll see `callback.run` in goals that consume callback envelopes from elsewhere; see [callback](callback.md).

## Output Channels

By default, output goes to the console. In a webserver context, it goes to the HTTP response. For ask, the channel is also where the question lands and where the answer is read from. The channel system routes output to the right destination automatically.
