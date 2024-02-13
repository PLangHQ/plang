# Plang Guide

We go over the default built in PLang LLM service or if you like to use OpenAI directly.

## Using Plang Service

Plang service allows you to build your code. By using the Plang service, you are supporting the project. The cost of using the Plang service is exactly twice the cost of using the OpenAI key.

Here's how you can use the Plang service:

1. Start building your code using Plang.
2. On your first build, if you don't have any voucher on the Plang service, you will be provided with a payment link.
3. Click on the payment link, choose the amount you want to buy for (we recommend starting small, like $5), fill in your credit card information, and submit.
4. Build again with Plang, and you should be good to go.

## Using OpenAI

If you have an OpenAI API key, you can use GPT4 to build your code. Here's how you can do it:

1. First, you need to get an API key from [OpenAI](https://openai.com/).
2. Next, download the OpenAI service into your project from [here](https://github.com/PLangHQ/services/tree/main/OpenAiService).
3. Create a folder named `.services` in your project.
4. In your `Start.goal` or `Events.goal` file, type in the following at the top:

When you download the OpenAI module, there are 3 directories for each OS. 
- Windows: OpenAIService/win-x64
- Linux: OpenAIService/linux-x64
- Linux ARM: OpenAIService/linux-arm64

This example uses Windows version

```plang
@llm=OpenAIService/win-x64

Start
- write out 'hello world'
```

## When to use `Start.goal` or `Events.goal`
If you are using Events in Plang, then instead of putting in `Start.goal`, it needs to go into your `Events.goal` file:

```plang
@llm=OpenAIService/win-x64

Events
- before app start, call !DoStuff
```

This is because `Events.goal` file is built before the `Start.goal` file.

The strict format of "@llm=OpenAIService/win-x64" allows the builder to parse it and load it before the LLM services starts doing request.

## Which is better?

The Plang service uses GPT4 from OpenAI, so there is no difference in the results. We hope to provide you with a faster and much cheaper service in the future. 

By using our service, you are supporting the project, its development, and hopefully enabling us to create a cheaper and more efficient language learning model (LLM).