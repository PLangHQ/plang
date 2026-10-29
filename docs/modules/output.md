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

#### Carrying state across the suspend

The pending ask's snapshot holds the run's variables (`snapshots.md`), so a variable set before the ask is in scope when the answer comes back. A sensitive value is the exception: it is left out of a snapshot, and the resumed run asks for it again.

```plang
/ %userId% is still in scope when the answer arrives
- set %userId% = 42
- ask 'What is your name?', write to %name%
- write out 'User %userId% is %name%'
```

On a channel that cannot wait for the answer (a message or HTTP channel), the pending ask carries a signed snapshot of the run. When the actor answers, the run is resumed from it, the answer is bound under `%!ask.answer%`, and the same ask step answers with it instead of asking again. The answer flows through the step's `write to %x%` target. PLang signs and checks the snapshot for you — an unsigned or changed one never resumes.

You don't write the resume yourself for an ask — the channel does it. A goal that holds a callback from elsewhere goes on with `- start %callback%` (`callback.start`); see [callback](callback.md).

## Output Channels

By default, output goes to the console. In a webserver context, it goes to the HTTP response. For ask, the channel is also where the question lands and where the answer is read from. The channel system routes output to the right destination automatically.
