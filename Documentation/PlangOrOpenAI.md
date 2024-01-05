# Plang Guide

Welcome to the Plang guide! Plang is a powerful programming language that runs on Windows, Linux, and MacOS. This guide will help you understand how to use Plang effectively.

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
2. Next, download the OpenAI module into your project from [here](https://github.com/PLangHQ/modules/OpenAIService).
3. Create a folder named `modules` in your project.
4. In your `Start.goal` file, type in the following at the top:

```plang
@llm=OpenAIService

Start
- write out 'hello world'
```

If you are using Events in Plang, then instead of putting in `Start.goal`, it needs to go into your `Events.goal` file:

```plang
@llm=OpenAIService

Events
- before app start, call !DoStuff
```

The strict format of "@llm=OpenAIService" allows the builder to pick it up and use it instead of the built-in Plang service.

## Which is better?

The Plang service uses GPT4 from OpenAI, so there is no difference in the results. We hope to provide you with a faster and much cheaper service in the future. By using our service, you are supporting the project, its development, and hopefully enabling us to create a cheaper and more efficient language learning model (LLM) as the build process is relatively simple.