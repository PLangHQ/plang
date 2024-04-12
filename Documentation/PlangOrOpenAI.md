# Plang Development Guide

Welcome to the Plang Development Guide! This document provides detailed instructions on how to build code using the Plang service or OpenAI's GPT4-Turbo. Each method offers unique benefits, and this guide will help you choose and implement the best option for your project needs.

## Using Plang Service

The Plang service not only facilitates code building but also supports the ongoing development of the Plang project. Opting for this service costs twice as much as using an OpenAI API key but directly contributes to the project's growth.

### Steps to Use Plang Service:

1. **Initiate Build**: Start building your code using the Plang service.
2. **Payment Process**: If it's your first build and you don't have a voucher, you will receive a payment link. Click on this link, choose a starting amount (a small amount like $5 is recommended for starters), enter your credit card details, and submit the form.
3. **Continue Building**: Once the payment is complete, you can continue building with Plang.

## Using OpenAI

For those who prefer using OpenAI or already have an OpenAI API key, integrating GPT4-Turbo into your Plang projects is straightforward.

### How to Use OpenAI with Plang:

First, ensure you have an API key from OpenAI. You can obtain one from [OpenAI's website](https://openai.com/).

Next, use the `--llmservice=openai` parameter in your Plang commands as shown below:

```bash
plang --llmservice=openai
plang build --llmservice=openai
plang exec --llmservice=openai
```

## Setting Environment Variables

You can set the `PLangLllmService` environment variable to either 'plang' or 'openai' based on your preference. This variable configures which service Plang should use by default. Setting this variable eliminates the need to specify the `--llmservice` parameter in your commands.

You might need to restart any your terminal or Visual Code after you change environment settings.

## Integration with Visual Studio Code

Visual Studio Code users can enhance their development experience by configuring the editor to use either Plang or OpenAI as the default LLM service. This setting can be adjusted by searching for `Select Plang LLM service` in the Visual Studio Code settings.

## Local LLM Development

While there is currently no local LLM available, developers interested in experimenting with or developing a local version can set it up by adding the `PLangLllmServiceUrl` environment variable. The value should start with 'http' and point to your local server. For instance, if your local LLM is running on port 5000, set it to `http://localhost:5000/path/to/llm/`.

## Comparison and Future Prospects

Both the Plang service and OpenAI use GPT4-Turbo, so there is no difference in the quality of results. The choice between the two may depend on your preference for supporting the Plang project or utilizing existing OpenAI services. We aim to provide a faster and more cost-effective service in the future, enhancing the efficiency of the build process which currently does not require a large-scale LLM.

By choosing the Plang service, you are directly supporting the development of the project and potentially enabling the creation of a more affordable LLM solution in the future.