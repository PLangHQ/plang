# Understanding the Plang Builder Process

The Plang Builder is a sophisticated tool designed to interpret and execute `.goal` files written in the Plang programming language. This document provides a detailed walkthrough of how the Plang Builder processes a simple `ReadFile.goal` file to demonstrate its functionality.

## Example: ReadFile.goal

Consider the following Plang code snippet:

```plang
ReadFile
- read file.txt to %content%
```

In this example, the goal is to read the contents of `file.txt` into the variable `%content%`. Here's how the Plang Builder processes this goal:

### Step 1: Parsing the Goal File

The builder begins by reading the entire `.goal` file. It identifies each goal defined within the file, as a single `.goal` file can contain multiple goals. It then collects all the steps associated with each goal.

### Step 2: Processing Steps

Each step is processed using the [`StepBuilder`](https://github.com/PLangHQ/plang/blob/main/PLang/Building/StepBuilder.cs) component. For the step `- read file.txt to %content%`, the builder sends this step to the Language Learning Model (LLM) along with a list of all available modules (refer to [Modules Documentation](./modules/README.md)).

The LLM is queried to determine the best module that aligns with the user's intent. In this case, the LLM suggests using the `FileModule` (source code available [here](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/FileModule/Program.cs)).

### Step 3: Constructing Instructions

With the module identified, the `InstructionBuilder` constructs the necessary instructions to execute the user's intent. The builder queries the LLM again, this time providing a list of all methods available in the `FileModule`. The LLM responds with the appropriate function to call and its parameters. For this example, the response might look like:

```json
{
  "Action": {
    "FunctionName": "ReadTextFile",
    "Parameters": [
      {
        "Type": "String",
        "Name": "path",
        "Value": "file.txt"
      }
    ],
    "ReturnValue": [
      {
        "Type": "String",
        "VariableName": "content"
      }
    ]
  }
}
```

### Step 4: Validation and Execution

The builder validates that the function exists within the `FileModule` and that the parameters match. If any discrepancies are found, it makes another request to the LLM with error information for correction.

Once validated, the builder compiles the instructions into executable code. For this step, the runtime would execute the following code:

```plang
var content = FileModule.ReadTextFile("file.txt")
```

### Further Exploration

This overview simplifies the actual complexity involved in the Plang Builder's process. For a deeper understanding, you are encouraged to explore the [Building directory](https://github.com/PLangHQ/plang/tree/main/PLang/Building) in the Plang Builder's source code repository.

By following this guide, developers can gain insights into how the Plang Builder interprets and executes goals, facilitating the creation of robust and efficient Plang applications.