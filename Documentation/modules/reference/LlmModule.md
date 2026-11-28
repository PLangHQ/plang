# LlmModule

Hint: `[llmmodule]`  
Type: `PLang.Modules.LlmModule.Program`

Ask LLM a question and recieve and answer. Also decide: when a step says decide, or picks one option out of a list of options (kind, status, yes or no) for a text, it is Decide, answered by the decider with a confidence

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

### Decide

```
Decide(Object state, String question, Dictionary<String, String> options = null) : String
```

Decides which one of a closed set of options fits a text. Answers the option, never free text. Use it when the answer is a choice (kind, status, yes or no), use AskLlm when the answer is text to write or extract. %x% is the option, %x!Confidence% how sure it is, %x!Probabilities% each option's share.

- `state` *Object* — The text or variable the decision is about, e.g. %email%
- `question` *String* — What is being decided, short, e.g. which kind of email is this
- `options` *Dictionary<String, String>*, default `null`

Examples:

- decide which kind of email %email% is, options: invoice, order, spam, write to %kind% => state="%email%", question="which kind of email is this", options={"invoice": null, "order": null, "spam": null}
- decide if %message% asks for a reply, options: yes means a question waits for an answer, no means nothing waits, write to %needsReply% => state="%message%", question="does the message ask for a reply", options={"yes": "a question waits for an answer", "no": "nothing waits for an answer"}
- decide the status of %company% from %mail%, options: open, waiting, closed, write to %status% => state="%mail%", question="what should the status of the company be", options={"open": null, "waiting": null, "closed": null}

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
RunAgent(String messages, List<PLang.Models.AgentTool> tools = null, String model = null, String reasoning = null, Int32 maxRounds = 30, PLang.Models.GoalToCallInfo onToolCall = null, PLang.Models.GoalToCallInfo onToolResult = null, PLang.Models.GoalToCallInfo onProgress = null, PLang.Models.GoalToCallInfo onRoundEnd = null, Int32 timeoutInSeconds = 600) : PLang.Modules.LlmModule.Program+AgentRun
```

Runs an agent: sends messages and tools to the llm, runs each tool the llm asks for by calling the tool's goal with the arguments as parameters, appends the results to messages and repeats until the llm answers with text. messages is the conversation and is updated in place. tools is a list of {name, description, parameters (json schema), call (goal path)}. Events: onProgress runs once per round that has tool calls, before them, with %text% (what the llm wrote alongside the calls, may be empty), %round% and %toolCallCount%; onToolCall runs before a tool with %toolCall%; onToolResult runs after it with %toolCall% and %toolResult% and may return a replacement result; onRoundEnd runs after the round's tools with %round%. reasoning: none|low|medium|high. Returns {Answer, Rounds, ToolCalls, ToolErrors, InputTokens, OutputTokens, StoppedAtMaxRounds}

- `messages` *String*
- `tools` *List<PLang.Models.AgentTool>*, default `null` — (see Type information in SupportingObjects)
- `model` *String*, default `null`
- `reasoning` *String*, default `null`
- `maxRounds` *Int32*, default `30`
- `onToolCall` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onToolResult` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onProgress` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `onRoundEnd` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `timeoutInSeconds` *Int32*, default `600`

### UseSharedIdentity

```
UseSharedIdentity(Boolean useSharedIdentity = True) : object
```


