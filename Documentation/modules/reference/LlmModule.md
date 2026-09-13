# LlmModule

Hint: `[llmmodule]`  
Type: `PLang.Modules.LlmModule.Program`

Ask LLM a question and recieve and answer

## Methods

### AppendToAssistant

```
AppendToAssistant(String assistant) : object
```


### AppendToSystem

```
AppendToSystem(String system) : object
```


### AppendToUser

```
AppendToUser(String user) : object
```


### AskLlm

```
AskLlm(List<PLang.Models.LlmMessage> promptMessages, String scheme = null, String model = gpt-4.1-mini, Double temperature = 0, Double topP = 0, Double frequencyPenalty = 0, Double presencePenalty = 0, Int32 maxLength = 4000, Boolean cacheResponse = True, String llmResponseType = null, Boolean continuePrevConversation = False, PLang.Modules.LlmModule.Program+Tools tools = null) : Object
```

When user intent is to write the result into a %variable% it MUST have ReturnValues, e.g. `... write to %result% => ReturnValues should contain the %result%

- `promptMessages` *List<PLang.Models.LlmMessage>* — (see Type information in SupportingObjects)
- `scheme` *String*, default `null`
- `model` *String*, default `gpt-4.1-mini`
- `temperature` *Double*, default `0`
- `topP` *Double*, default `0`
- `frequencyPenalty` *Double*, default `0`
- `presencePenalty` *Double*, default `0`
- `maxLength` *Int32*, default `4000`
- `cacheResponse` *Boolean*, default `True`
- `llmResponseType` *String*, default `null`
- `continuePrevConversation` *Boolean*, default `False`
- `tools` *PLang.Modules.LlmModule.Program+Tools*, default `null` — (see Type information in SupportingObjects)

### GetLlmIdentity

```
GetLlmIdentity() : String
```


### GetPreviousMessages

```
GetPreviousMessages() : PLang.Models.LlmMessage
```

Retrieves all previous messages


### RunAgent

```
RunAgent(String messages, List<PLang.Models.AgentTool> tools = null, String model = null, String reasoning = null, Int32 maxRounds = 30, PLang.Models.GoalToCallInfo onToolCall = null, PLang.Models.GoalToCallInfo onToolResult = null, PLang.Models.GoalToCallInfo onProgress = null, Int32 timeoutInSeconds = 600) : PLang.Modules.LlmModule.Program+AgentRun
```

Runs an agent: sends messages and tools to the llm, runs each tool the llm asks for by calling the tool's goal with the arguments as parameters, appends the results to messages and repeats until the llm answers with text. messages is the conversation and is updated in place. tools is a list of {name, description, parameters (json schema), call (goal path)}. Events: onToolCall runs before a tool with %toolCall%, onToolResult runs after it with %toolCall% and %toolResult% and may return a replacement result, onProgress runs with %text% when the llm writes text alongside tool calls. reasoning: none|low|medium|high. Returns {Answer, Rounds, ToolCalls, ToolErrors, InputTokens, OutputTokens, StoppedAtMaxRounds}

- `messages` *String*
- `tools` *List<PLang.Models.AgentTool>*, default `null` — (see Type information in SupportingObjects)
- `model` *String*, default `null`
- `reasoning` *String*, default `null`
- `maxRounds` *Int32*, default `30`
- `onToolCall` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onToolResult` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onProgress` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `timeoutInSeconds` *Int32*, default `600`

### UseSharedIdentity

```
UseSharedIdentity(Boolean useSharedIdentity = True) : object
```


