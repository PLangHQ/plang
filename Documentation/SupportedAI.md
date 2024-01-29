# Supported AI Models in Plang

Plang integrates with a variety of OpenAI models, providing developers with the flexibility to choose the model that best fits their needs. Below is a list of supported models along with their respective input and output prices per 1000 tokens.

### OpenAI Model Pricing

| Model                      | Input Price | Output Price |
|----------------------------|-------------|--------------|
| gpt-4                      | $0.06       | $0.12        |
| gpt-4-32k                  | $0.12       | $0.24        |
| gpt-3.5-turbo-1106         | $0.0020     | $0.0040      |
| gpt-3.5-turbo-instruct     | $0.0030     | $0.0040      |
| gpt-4-1106-preview         | $0.02       | $0.06        |
| gpt-4-1106-vision-preview  | $0.02       | $0.06        |
| gpt-4-vision-preview       | $0.02       | $0.06        |

## Utilizing AI Models in Plang

To use an AI model within Plang, you need to create a goal file. Here's an example of how to use a Large Language Model (LLM), with the default model set to gpt-4:

```plang
Start
- [llm] system: what is the sentiment of user input
        user: This rocks
        scheme: {sentiment:positive|negative|neutral}
        write to %sentiment%
- write out %sentiment%
```

### Supported Parameters

When coding your LLM in Plang, you can customize its behavior using the following parameters:

- `scheme`: A string that describes the JSON structure for structured responses from the LLM.
- `model`: The default is gpt-4.
- `temperature`: Default is 0.
- `topP`: Default is 0.
- `frequencyPenalty`: Default is 0.0.
- `presencePenalty`: Default is 0.0.
- `maxLength`: Default is 4000.
- `cacheResponse`: Default is true.
- `llmResponseType`: The default is null, but you can specify `.md`, `css`, `javascript`, `html`.

### Example Usage with Parameters

Here are some examples of how to use these parameters within your Plang goal file:

```plang
Start
- [llm] system: generate a summary for the following article
        user: [Article content goes here]
        model: gpt-4-32k
        temperature: 0.7
        topP: 1
        maxLength: 300
        llmResponseType: .md
        write to %summary%
- write out %summary%
```

## Understanding LLM Structure in Plang

In Plang, the interaction with an LLM is structured through a conversation-like syntax involving three main components:

- `system`: This represents the instructions or commands given to the LLM.
- `user`: This is the input from the user that the LLM will process.
- `assistant`: The LLM itself, which processes the input and generates a response based on the system's instructions.

The `%variable%` syntax is used to store and reference the output of the LLM within the goal file. This allows for the output to be used in subsequent steps or to be manipulated further.

By understanding these components and how they interact, developers can effectively utilize the power of LLMs within their Plang applications.