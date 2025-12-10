# Plang Builder Documentation

Welcome to the Plang Builder documentation! This guide will help you understand how Plang processes and builds your statements, using the example of reading a file. We'll walk through the process step-by-step, explaining how the Builder interprets and executes your Plang code.

## Example: ReadFile.goal

Let's start with a simple example of a Plang goal file, `ReadFile.goal`:

```plang
ReadFile
- read file.txt to %content%
```

### Explanation

In this example, we aim to read the contents of `file.txt` into the variable `%content%`. Here's how the Plang Builder processes this:

1. **Reading the Goal File**: The Builder begins by reading the entire goal file. It identifies each goal within the file, as a `.goal` file can contain multiple goals. It also gathers all the steps associated with each goal.

2. **Processing Each Step**: The Builder processes each step using the [StepBuilder](https://github.com/PLangHQ/plang/blob/main/PLang/Building/StepBuilder.cs). For our example, the step is `- read file.txt to %content%`. The Builder sends this step to the Language Learning Model (LLM) along with a list of all available modules, which can be found in the [modules README](./modules/README.md).

3. **Determining the Appropriate Module**: The LLM is queried to determine which module best fits the user's intent. It responds with a JSON object indicating the module to use. For our example, the LLM returns:

   ```json
   {
       "Text": "- read file.txt to %content%",
       "ModuleType": "PLang.Modules.FileModule"
   }
   ```

   The module identified is `FileModule`, which is defined in the [FileModule source code](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/FileModule/Program.cs).

4. **Building Instructions**: The Builder then constructs the necessary instructions to execute the user's intent using the [InstructionBuilder](https://github.com/PLangHQ/plang/blob/main/PLang/Building/InstructionBuilder.cs). It sends the LLM a list of all available methods in the `FileModule`, including `ReadTextFile`. The LLM returns a JSON object specifying the function to call and its parameters:

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

5. **Validation and Execution**: The Builder validates that the function exists in the `FileModule` and that the parameters match. If validation fails, it requests further information from the LLM, including error details. Once validated, the runtime executes the instruction, effectively running the code `var content = FileModule.ReadTextFile("file.txt")`.

This process is a simplified overview. For a deeper understanding, we encourage you to explore the [Building section of the Plang source code](https://github.com/PLangHQ/plang/tree/main/PLang/Building).

By following these steps, you can effectively use Plang to automate tasks and build complex workflows. Happy coding!