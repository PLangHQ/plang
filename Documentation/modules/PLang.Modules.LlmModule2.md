
# LlmModule
## Introduction
The LlmModule is a powerful component of the plang programming language designed to interface with Language Learning Models (LLMs). It provides a simple yet effective way to perform natural language processing tasks such as sentiment analysis, text categorization, content generation, question answering, and language translation.

## For Beginners
If you're new to programming or unfamiliar with technical jargon, think of the LlmModule as a friendly assistant that can understand and generate human-like text. It can answer questions, analyze the mood of a sentence, categorize information, translate languages, and even write stories or articles based on prompts you provide.

## Best Practices for Llm
When using the LlmModule in plang, it's important to structure your code clearly and use variables effectively. Here's a best practice example:

```plang
LLM
- set %prompt% as 'Tell me a joke about cats'
- [llm] system: generate a joke based on the prompt
        user: %prompt%
        write to %joke%
- if %joke% is not empty then call !JokeReceived, else !NoJokeFound
```

In this example, we first set a prompt for the LLM. We then ask the LLM to generate content based on that prompt and store the result in a variable. Finally, we use an if statement to check if a joke was generated and call the appropriate function based on the result.

## Supported AI
You can [view the list of supported AI models](https://github.com/PLangHQ/plang/blob/main/Documentation/SupportedAI.md) that you can define in your code when using the Llm module.

# LlmModule Documentation

The `LlmModule` is designed to interact with a Language Learning Model (LLM) to ask questions and receive answers. This module can be used to analyze text, generate content, and perform various NLP tasks.

## Methods

### AskLlm
Asks the LLM a question and receives an answer. This method can be used to analyze sentiment, categorize text, and more.

#### Parameters
- `scheme`: The format or structure of the question.
- `system`: The specific system or context for the question.
- `assistant`: The assistant's name or identifier.
- `user`: The user's input or query.
- `model`: The model of the LLM, default is `gpt-4-turbo`.
- `temperature`: The creativity level, default is `0`.
- `topP`: The probability threshold for token selection, default is `0`.
- `frequencyPenalty`: The penalty for frequency, default is `0.0`.
- `presencePenalty`: The penalty for presence, default is `0.0`.
- `maxLength`: The maximum length of the response, default is `4000` characters.
- `cacheResponse`: Whether to cache the response, default is `true`.
- `llmResponseType`: The type of response expected from the LLM.

#### Returns
- A `Task` that completes with the LLM's response.

## Examples

### Example 1: Sentiment Analysis
```plang
LLM
- set %comment% as 'This is awesome'
- [llm] system: give me sentiment from the user comment
        user:  %comment%
        scheme: {sentiment:negative|neutral|positive}
        write to %result%
- write out 'The comment is: %result.sentiment%'
```

### Example 2: Text Categorization
```plang
LLM
- set %text% as 'AI is taking over the world'
- [llm] system: give me 2 categories from the user text
        user: %text% 
        write to %cat1% and %cat2%
- write out 'The categories are: %cat1% and %cat2%'
```

### Example 3: Content Generation
```plang
LLM
- set %prompt% as 'Write a short story about a space adventure'
- [llm] system: generate a story based on the prompt
        user: %prompt%
        model: 'gpt-4'
        temperature: 0.7
        maxLength: '2000'
        write to %story%
- write out 'Here is your story: %story%'
```

### Example 4: Question Answering
```plang
LLM
- set %question% as 'What is the distance from the Earth to the Moon?'
- [llm] system: answer the user question
        user: %question%
        cacheResponse: false
        write to %answer%
- write out 'The answer is: %answer%'
```

### Example 5: Language Translation
```plang
LLM
- set %phrase% as 'Hello, how are you?'
- [llm] system: translate the phrase to Spanish
        user: %phrase%
        write to %translation%
- write out 'The translation is: %translation%'
```

These examples demonstrate the versatility of the `LlmModule` in handling various tasks such as sentiment analysis, text categorization, content generation, question answering, and language translation. The parameters can be adjusted to fit the specific needs of the task at hand.


For a full list of examples, visit [LlmModule Examples on GitHub](https://github.com/PLangHQ/plang/tree/main/Tests/Llm).

## Step Options
Each step in your plang code can be enhanced with additional options for robustness and functionality. Click the links below for more details on how to use each option:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For those who are ready to dive deeper and understand how the LlmModule maps to underlying C# functionality, please refer to the [advanced documentation](./PLang.Modules.LlmModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:56:42.
