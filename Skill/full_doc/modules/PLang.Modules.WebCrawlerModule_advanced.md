# WebCrawler

## Introduction
The WebCrawler module in the plang programming language serves as a powerful tool for automating interactions with web pages. It allows users to programmatically navigate websites, extract data, and perform actions like clicking buttons or entering text. This module is particularly useful for tasks such as automated testing, data scraping, or any scenario where web interaction is required without manual input.

### How plang Integrates with C# Methods
In plang, each step defined in the `.goal` file corresponds to a method in a C# module. The mapping from plang to C# is facilitated by a language model that interprets natural language instructions and translates them into structured commands that invoke specific C# methods from the WebCrawler module. This seamless integration allows users to leverage the robust features of C# while working within the intuitive, natural-language-based environment of plang.

## Plang Code Examples
For more comprehensive documentation and examples, refer to the [WebCrawler Module Documentation](./PLang.Modules.WebCrawlerModule.md) and explore the [repository for examples](https://github.com/PLangHQ/plang/tree/main/Tests/WebCrawler).

### Example: Navigating to a URL and Extracting Content
This common use case involves navigating to a specific URL and extracting content from the page. Below is a plang example followed by the corresponding C# method signature.

#### plang Code
```plang
WebCrawler
- Navigate to URL 'https://example.com'
- Extract content from '#content', clear html, write to %extractedContent%
```

#### C# Method Signature
```csharp
void NavigateToUrl(string url);
List<string> ExtractContent(bool clearHtml, string cssSelector);
```

### Example: Input and Click
Another frequent operation is to input data into form fields and simulate button clicks, often used in automated form submissions or login procedures.

#### plang Code
```plang
WebCrawler
- Navigate to URL 'https://example.com/login'
- Input 'username' into '#username'
- Input 'password123' into '#password'
- Click '#submit-button'
```

#### C# Method Signature
```csharp
void NavigateToUrl(string url);
void Input(string value, string cssSelector);
void Click(string cssSelector);
```

For additional methods and detailed examples, please refer to the [WebCrawler Module Documentation](./PLang.Modules.WebCrawlerModule.md) and the [repository for examples](https://github.com/PLangHQ/plang/tree/main/Tests/WebCrawler). For a deeper dive into the source code, check out [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.WebCrawlerModule/Program.cs) and [Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.WebCrawlerModule/Builder.cs).

## Source Code
The runtime code for the WebCrawler module is available at [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.WebCrawlerModule/Program.cs). The Builder.cs, which is responsible for building steps, can be found at [Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.WebCrawlerModule/Builder.cs).

## How plang is mapped to C#
### Builder
1. During the build process, the `.goal` file is parsed to identify each step.
2. Each step is sent to the LLM along with a list of available modules for module suggestion.
3. Once a module is selected, the methods from the WebCrawler module are sent to the LLM to match the step with an appropriate method.
4. The LLM returns a JSON mapping the step to a C# method with necessary parameters.
5. Builder.cs or BaseBuilder.cs processes this JSON to create a hash and save an instruction file with the `.pr` extension.

### Runtime
1. The `.pr` file is loaded by the plang runtime.
2. Reflection is used to load the WebCrawler module.
3. The "Function" property in the `.pr` file specifies which C# method to call.
4. If parameters are required, they are passed to the method as specified in the `.pr` file.

### Example Instruction `.pr` file
```json
{
  "Action": {
    "FunctionName": "NavigateToUrl",
    "Parameters": [
      {
        "Type": "string",
        "Name": "url",
        "Value": "https://example.com"
      }
    ]
  }
}
```

## Created
This documentation was created on 2024-07-26T10:53:34.