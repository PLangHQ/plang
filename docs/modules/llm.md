# LLM Module

Query large language models with tool calling, streaming, structured output, conversation continuity, and caching. The default implementation is OpenAI-compatible and works with any API that follows the OpenAI chat completions format.

## Actions

### query

Send a query to an LLM.

```plang
- system: analyze sentiment
  user: %comment%
  schema: {sentiment: string, score: int}
  write to %result%
```

```plang
- system: you are a helpful assistant
  user: %question%
  write to %answer%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Messages | list | yes | — | Conversation messages with Role (system/user/assistant) and Text |
| Tools | list | no | — | Goals available as tools for the LLM to call |
| OnToolCall | goal | no | — | Callback fired before/after each tool execution |
| OnValidateResponse | goal | no | — | Callback to validate the response. Return error to retry |
| OnStream | goal | no | — | Callback fired for each streaming chunk |
| Schema | string | no | — | JSON schema the response must conform to |
| Format | string | no | — | Response format: json, md, python, yaml, etc. |
| Model | string | no | gpt-4.1-mini | Model override |
| ContinuePreviousConversation | bool | no | false | Prepend stored conversation history from previous queries |
| Temperature | double | no | 0.0 | Sampling temperature (0.0 = deterministic) |
| MaxTokens | int | no | 4000 | Maximum tokens in the response |
| MaxToolCalls | int | no | 10 | Maximum total individual tool calls before stopping |
| MaxValidationRetries | int | no | 3 | Maximum validation retries before returning error |
| Cache | bool | no | true | Cache the response. Skipped when Tools is non-null |

**Returns:** The LLM's response — parsed JSON object when format is json, extracted text for other formats, or raw text when no format is set.

## Schema and Format

### Schema

When you set `schema`, the module instructs the LLM to respond in JSON matching that schema. The format automatically defaults to `json`.

```plang
- system: extract entities
  user: %text%
  schema: {people: [{name: string, role: string}]}
  write to %entities%
- write out %entities.people[0].name%
```

### Format

Format controls how the response is extracted. `json` is special (schema support + JSON validation). All other formats instruct the LLM to wrap the response in a code block, and the module extracts from it.

```plang
- system: convert this to python
  user: %csharpCode%
  format: python
  write to %pythonCode%
```

```plang
- system: explain this code
  user: %code%
  format: md
  write to %explanation%
```

| Format | Behavior |
|--------|----------|
| `json` | JSON validation + schema support. Extracts from code blocks on parse failure |
| `python`, `md`, `yaml`, etc. | Extracts content from matching code block in response |
| *(not set, no schema)* | Raw response returned as-is |

## Tools

Define goals as tools the LLM can call. Each tool has a name, description, typed parameters, and an optional `parallel` flag.

```plang
- system: you are a helpful assistant
  user: %question%
  tools:
    GetWeather, gets weather for a city, %city%(string), parallel
    SearchWeb, searches the web, %query%(string)
  onToolCall call DisplayToolStatus
  write to %answer%
```

The LLM decides which tools to call based on the descriptions. Tool results are sent back to the LLM automatically. The loop continues until the LLM responds with content (no more tool calls) or `MaxToolCalls` is reached.

### Parallel tool execution

When the LLM requests multiple tools in one response and all matching goals have `parallel` set, they execute concurrently. If any tool in the batch is not parallel, all run sequentially.

### OnToolCall callback

Fires before and after each tool execution with `%name%`, `%arguments%`, `%status%` (starting/completed), and `%result%` (on completed).

```plang
DisplayToolStatus
- if %status% = 'starting'
  - write out 'Calling %name%...'
- if %status% = 'completed'
  - write out '%name% done'
```

## Validation

Use `OnValidateResponse` to validate the LLM's response. If the validation goal returns an error, the error message is sent back to the LLM for a retry, up to `MaxValidationRetries`.

```plang
- system: generate a haiku
  user: %topic%
  onValidateResponse call CheckHaiku
  write to %haiku%

CheckHaiku
- if %response% does not contain 3 lines
  - throw error 'Haiku must have exactly 3 lines'
```

## Streaming

Use `OnStream` to receive response chunks as they arrive. The callback receives `%content%` (the chunk), `%fullContent%` (accumulated), and `%isDone%` (bool).

```plang
- system: tell me a story
  user: %prompt%
  onStream call ShowChunk
  write to %story%

ShowChunk
- write out %content%
```

## Conversation Continuity

Set `continuePreviousConversation` to prepend the full message history from the previous query in the same goal scope.

```plang
Start
- system: you are a helpful assistant
  user: What is PLang?
  write to %answer1%

- system: you are a helpful assistant
  user: Tell me more about that
  continuePreviousConversation
  write to %answer2%
```

The stored conversation includes the assistant's response. Format instructions are re-applied fresh on each call (they don't compound).

## Images

Send images in messages using file paths, URLs, or base64 strings.

```plang
- system: describe this image
  user: what do you see?
  images: https://example.com/photo.jpg
  write to %description%
```

Multiple images per message are supported. File paths are automatically read and base64-encoded.

## Caching

Responses are cached by default (persistent, hash-based). The cache key includes messages, model, temperature, schema, and format. Caching is automatically skipped when tools are used.

```plang
/ This will be cached
- system: translate to French
  user: Hello world
  write to %french%

/ Force a fresh call
- system: translate to French
  user: Hello world
  cache false
  write to %french2%
```

## Response Properties

The returned value carries metadata alongside the value itself. Read metadata properties with the `!` accessor — `%result!TotalTokens%` reads the `TotalTokens` metadata, while `%result.sentiment%` reads a field on the value. The `!` accessor works on any variable returned from an action that attaches metadata.

| Property | Type | Description |
|----------|------|-------------|
| `RawResponse` | string | Raw text response from the LLM |
| `Model` | string | Model that was used |
| `Cached` | bool | Whether this came from cache |
| `PromptTokens` | int | Total prompt tokens across all API calls |
| `CompletionTokens` | int | Total completion tokens |
| `TotalTokens` | int | PromptTokens + CompletionTokens |
| `Cost` | double? | Estimated cost (null if the implementation has no pricing data) |
| `ToolCallCount` | int | Total tool calls executed |
| `ValidationRetries` | int | Number of validation retries |
| `Format` | string? | Effective format used |
| `Schema` | string? | Schema used |

```plang
- system: analyze this
  user: %text%
  schema: {sentiment: string}
  write to %result%
- write out 'Sentiment: %result.sentiment%'
- write out 'Tokens: %result!TotalTokens%, Cost: %result!Cost%'
```

## Configuration

The default OpenAI implementation reads settings from:

| Setting | Default | Description |
|---------|---------|-------------|
| `llm.endpoint` | `https://api.openai.com/v1/chat/completions` | API endpoint |
| `llm.apiKey` | env `OPENAI_API_KEY` | API authentication |
| `llm.model` | `gpt-4.1-mini` | Default model (overridden by Model parameter) |

To use a different OpenAI-compatible API (e.g., Azure OpenAI, local models):

```plang
- set setting 'llm.endpoint' to 'https://my-server.com/v1/chat/completions'
- set setting 'llm.apiKey' to %myKey%
```

The implementation is swappable via the standard `code.setDefault` action (see [code module](code.md)).

## Examples

### Simple Query

```plang
Start
- system: you are a helpful assistant
  user: What is the capital of Iceland?
  write to %answer%
- write out %answer%
```

### Structured Output

```plang
Start
- system: extract product info
  user: %productDescription%
  schema: {name: string, price: number, category: string}
  write to %product%
- write out 'Product: %product.name% ($%product.price%)'
```

### Multi-Turn Conversation

```plang
Start
- system: you are a language tutor
  user: Teach me a word in Icelandic
  write to %lesson1%
- write out %lesson1%

- system: you are a language tutor
  user: Use it in a sentence
  continuePreviousConversation
  write to %lesson2%
- write out %lesson2%
```

### Tool Calling

```plang
Start
- system: you are a weather assistant
  user: What's the weather in Reykjavik and London?
  tools:
    GetWeather, gets current weather for a city, %city%(string), parallel
  onToolCall call ShowToolProgress
  write to %answer%
- write out %answer%

GetWeather
- get 'https://api.weather.com/%city%', write to %weather%
- return %weather%

ShowToolProgress
- if %status% = 'starting'
  - write out 'Looking up %name%(%arguments%)...'
```
