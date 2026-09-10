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


### UseSharedIdentity

```
UseSharedIdentity(Boolean useSharedIdentity = True) : object
```


