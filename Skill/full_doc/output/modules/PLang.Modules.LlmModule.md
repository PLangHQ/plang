
# Llm

## Introduction
Llm (Language Learning Model) is a powerful tool designed to facilitate interactions between users and AI systems. It allows developers to create applications that can understand and respond to natural language queries, making it easier to integrate AI capabilities into various software solutions.

## For beginners
Llm is a programming language that helps you communicate with AI in a simple way. Think of it as a way to ask questions and get answers from a computer, just like you would with a human. You write instructions in Llm, and the AI processes those instructions to provide you with the information or actions you need. Even if you are new to programming, Llm is designed to be user-friendly and intuitive.

# Llm Module Documentation Examples

## 1. Ask LLM a Question
```plang
Start
- set %userQuestion% as 'What are the benefits of using AI in healthcare?'
- [llm] system: provide a detailed answer to the user question
        user: %userQuestion%
        scheme: {benefits:string[]}
        write to %answer%
- write out 'The benefits are: %answer%'
```

## 2. Append to System, Assistant and user

If you like to include some system command to all llm for that context, you can use the append actions

```plang
Start
- append to system 'Your name is Lucy, working for Achme. You should answer as Shakespear'
- [llm] system: "Analyze request from user"
        user: "Tell me a story about your company"
        write to %result%
- write out %result%
```

This is usefull for example when you want set basic information such about your company or the style of writing for all llm request in that context. You could set this in an event on app start or when specific goals run.


## 5. Use Shared Identity

By default the plang language uses always the same Identity to communicate with Plang service or OpenAI (API key). By setting share identity to false, it uses the Identity in your app.

```plang
- set %sharedIdentity% as false
- [llm] system: "Say hello back"
        user: "Hello"
        write to %response%
- write out %response%
```

## 7. Get Balance

Gets your current balance at the Plang LLM service. This does not work for OpenAI

```plang
Start
- get balance at llm servive, write to %balance%
- write out 'Current balance: %balance%'
```

## Examples
- You can find the source code of the Llm module at [this link](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/Llm/Program.cs).
- For a full list of examples, visit [this link](https://github.com/PLangHQ/plang/tree/main/Tests/Llm).

## Step options
These options are available for each step. Click the links for more details on how to use them:
- [CacheHandler](./modules/handlers/CachingHandler.md)
- [ErrorHandler](./modules/handlers/ErrorHandler.md)
- [RetryHandler](./modules/handlers/RetryHandler.md)
- [Run and forget](./moduels/RunAndForget.md)

## Advanced
For more advanced information, refer to the [advanced documentation](./PLang.Modules.LlmModule_advanced.md) if you want to understand how the underlying mapping works with C#.

## Created
This documentation is created 2024-08-27T14:56:19
