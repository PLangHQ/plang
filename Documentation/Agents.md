# Agents in plang

An agent is a loop: send the conversation and a list of tools to the llm, run the tool it asks
for, add the result to the conversation, repeat until it answers with text. In plang the loop
is one step, the tools are goals, and the rendering is yours.

```plang
Answer
- [llm] run agent, messages: %messages%, tools: %tools%
    model: "gpt-5.4-mini", reasoning: medium, max rounds: 30
    on tool call, call ToolStarted
    on tool result, call ToolFinished
    on progress, call Progress
    write to %run%
- write out %run.Answer%
```

`%messages%` is the conversation. It goes in with at least a system and a user message and
comes back with everything the llm and the tools added, so it is the thing to store between
turns. `%run%` holds the answer and the numbers of the run:

| `%run.…%` | |
|---|---|
| `Answer` | the llm's final text |
| `Rounds` | how many times the llm was called |
| `ToolCalls`, `ToolErrors` | tools run, and how many of those returned an error |
| `InputTokens`, `OutputTokens` | summed over the rounds |
| `StoppedAtMaxRounds` | true when the loop hit `max rounds` before the llm answered |

## Messages

Start a conversation with a list of role and content:

```plang
- set %messages% = [ { "role": "system", "content": %systemPrompt% }, { "role": "user", "content": %prompt% } ]
```

To continue one, load what you stored and add the next user message:

```plang
- select messages from conversations where id=%id%, return 1, write to %stored%
- set %messages% = %stored%
- add { "role": "user", "content": %prompt% } to list %messages%
```

Between turns the list also holds the llm's own items (its text, its tool calls, its reasoning
markers) and the tool outputs. Keep them as they are and pass the whole list back: reasoning
models use those items to keep their thread. Their shape is the provider's (today the OpenAI
Responses items: `message`, `function_call`, `function_call_output`, `reasoning`), which is
what a page that replays a conversation walks over.

## Tools

A tool is a goal plus a description of its arguments:

```plang
- set %tools% = [
    { "name": "count_schools",
      "description": "Counts the schools in the analytics database",
      "parameters": { "type": "object", "properties": { "dataSourceName": { "type": "string" } }, "required": ["dataSourceName"], "additionalProperties": false },
      "call": "/agent/tools/CountSchools" }
  ]
```

`parameters` is a JSON schema; the llm fills it. When the llm calls the tool, the arguments
become variables in the goal and whatever the goal returns is the tool result:

```plang
CountSchools
- select count(*) as n from schools, return 1 row, data source "analytics", write to %n%
- return %n%
```

A string is sent as is, anything else as JSON. If the goal throws, the llm gets `Error:` and
the first line of the message, the run counts a tool error, and the loop continues; the llm
decides what to do with it. A tool the llm names that is not in the list gets an error the
same way.

Keep the tool list in a json file when it grows: `read json file "tools.json", write to %tools%`.

## Events

Three goals let the app draw the run as it happens, without knowing anything about the
provider.

```plang
ToolStarted
- [ui] render "tool.html", append to #messages

ToolFinished
- [ui] render "tool.html", replace self of "#tool-%toolCall.id%"

Progress
- [ui] render "status.html", append to #messages
```

| Event | Variables | Runs |
|---|---|---|
| `on tool call` | `%toolCall%` with `id`, `name`, `arguments` (an object) | before the tool goal |
| `on tool result` | `%toolCall%`, `%toolResult%` | after the tool goal. If the goal returns a value, that replaces the result the llm sees |
| `on progress` | `%text%` | when the llm writes text in the same round as tool calls; the "let me check…" line |

`on tool result` is where knowledge rides along with data: a query on a table can return the
table's documentation page in front of the rows, so the llm reads it whether it asked for it or
not.

```plang
ToolFinished
- if %toolCall.arguments.sql% is empty then
    - end goal
- read file "/doc/%toolCall.arguments.table%.md", write to %doc%, if not found return empty string
- if %doc% is empty then
    - end goal
- return "Documentation:\n%doc%\n\nResult:\n%toolResult%"
```

## Options

| | |
|---|---|
| `model` | the provider's model name, default `gpt-5.4-mini` |
| `reasoning` | `none`, `low`, `medium`, `high`. Tools with reasoning need the OpenAI Responses api, which is what the openai service uses |
| `max rounds` | the loop stops here and `StoppedAtMaxRounds` is true; 30 by default. A hard question with many small tools uses more rounds than you think |
| `timeout in seconds` | per llm call, 600 by default |

## Which llm service

The agent runs through `ILlmService.Chat`, next to the `Query` the builder uses. The `openai`
service implements it (`--llmservice=openai`, key in the settings under `OpenAiKey`). The
plang service (`llm.plang.is`) does not yet and returns a 501 with that message. A service of
your own implements `Chat` once: send the messages and tools in its wire format, hand back the
items untouched plus the text, the tool calls and the usage.

## Things that took a day to learn

- **Advice in the system prompt is not read.** A page the llm "should read first" is skipped;
  a page that arrives inside a tool result is not. Put knowledge where the llm cannot avoid
  it: in the prompt, or in `on tool result`.
- **A tool error must be one line.** A full error dump with a call stack pulls the llm into
  debugging your runtime instead of its task. The loop trims to the first line; keep your
  tool goals' error messages short too.
- **Give it the common methods up front.** A tool that describes other tools
  (`describe_module`) is fine, but the two or three tools every conversation uses belong in
  the prompt with their parameters, or every conversation starts with the same lookups.
- **The llm asks back when the prompt lets it.** If a kind of question has a documented
  default, say in the prompt that the default is used and stated, not asked about.
- **Count.** Rounds, tool calls, errors and tokens per conversation, stored next to the
  conversation, are what tell you whether a change to the prompt or the tools helped.

## The C# side

`PLang.Modules.LlmModule.Program.RunAgent` is the loop. It reads the list behind `messages`
and appends to it in place, converts each tool call's arguments to a dictionary and calls the
tool goal through `CallGoalModule` with them as parameters, and calls the event goals the
same way. `ILlmService.Chat(LlmChatRequest)` returns `LlmChatResult(Items, Text, ToolCalls,
Usage)`. The models are in `PLang/Models/LlmChat.cs`.
