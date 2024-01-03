
# Selenium

## Introduction
The Selenium module in plang is designed to provide a seamless interface for automating web browsers, allowing users to perform tasks such as navigating web pages, interacting with elements, and extracting data. For advanced users with programming experience, understanding how plang steps translate into C# methods can unlock powerful customizations and optimizations in their automation scripts.

plang integrates with C# by using a language model to interpret natural language instructions and map them to corresponding C# methods in the Selenium module. This process involves two main components: the Builder and the Runtime. The Builder parses the .goal files and generates .pr instruction files, while the Runtime executes the instructions using the Selenium module's methods.

## Plang code examples
For simple documentation and examples, refer to [PLang.Modules.SeleniumModule.md](./PLang.Modules.SeleniumModule.md). The repository for additional examples can be found at [PLangHQ Selenium Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Selenium).

### Navigate to a URL
This is a common operation to start any web automation task. It involves opening a browser and navigating to a specified URL.

```plang
Selenium
- go to https://example.com, show browser
```

C# method signature:
```csharp
Task NavigateToUrl(string url, string browserType = "Chrome", bool headless = false, ...)
```

### Click on an Element
Another frequent operation is to simulate user interaction by clicking on elements within the web page.

```plang
Selenium
- click button #submit
```

C# method signature:
```csharp
Task Click(string cssSelector = null)
```

### Extract Content
Extracting content from a web page is essential for scraping data or verifying page states.

```plang
Selenium
- extract all .product-name, write to %productNames%
```

C# method signature:
```csharp
Task<List<string>> ExtractContent(bool clearHtml = true, string cssSelector = null)
```

For more detailed documentation and examples:
- Refer to [PLang.Modules.SeleniumModule.md](./PLang.Modules.SeleniumModule.md).
- Explore the repository at [PLangHQ Selenium Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Selenium).
- Review the Program.cs source code at [PLang.Modules.SeleniumModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.SeleniumModule/Program.cs).

## Source code
The Program.cs runtime code can be found at [PLang.Modules.SeleniumModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.SeleniumModule/Program.cs).
The Builder.cs for building steps can be found at [PLang.Modules.SeleniumModule Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.SeleniumModule/Builder.cs).

## How plang is mapped to C#

### Builder
When a user runs `plang build`, the .goal file is read, and each step is parsed. For each step, a question is sent to the language model along with a list of all available modules. The language model suggests a module to use, and then the Builder sends all the methods in the Selenium module to the language model along with the step. This is done using Builder.cs or BaseBuilder.cs, depending on availability. The language model returns a JSON that maps the step text to a C# method with the necessary parameters. The Builder creates a hash of the response and saves a JSON instruction file with the .pr extension in the .build/{GoalName}/ directory.

### Runtime
The .pr file is used by the plang runtime to execute the step. The runtime loads the .pr file, uses reflection to load the PLang.Modules.SeleniumModule, and calls the C# method specified in the "Function" property of the .pr file, with any provided parameters.

### plang example to csharp
Here's how a plang code example maps to a .pr file:

```plang
Selenium
- extract all .product-name, write to %productNames%
```

This step would map to the `ExtractContent` method in the Selenium module.

Example Instruction .pr file:
```json
{
  "Action": {
    "FunctionName": "ExtractContent",
    "Parameters": [
      {
        "Type": "bool",
        "Name": "clearHtml",
        "Value": "true"
      },
      {
        "Type": "string",
        "Name": "cssSelector",
        "Value": ".product-name"
      }
    ],
    "ReturnValue": {
      "Type": "List<string>",
      "VariableName": "productNames"
    }
  }
}
```

## Created
This documentation is created 2024-01-02T22:27:12
