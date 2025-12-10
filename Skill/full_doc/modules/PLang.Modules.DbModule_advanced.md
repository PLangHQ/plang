# Db

## Introduction
The `Db` module in the plang programming language serves as an interface for database operations, allowing users to perform a variety of tasks such as creating tables, inserting records, and managing transactions. This module leverages the power of C# to execute these operations securely and efficiently.

Advanced users familiar with programming will appreciate the seamless integration of plang steps with C# methods. The mapping process is facilitated by a Language Learning Model (LLM) which interprets natural language instructions and translates them into corresponding C# method calls. This documentation will guide you through the mapping rules and provide examples of common database operations in plang.

## Plang code examples
For simple documentation and examples, please refer to [PLang.Modules.DbModule.md](./PLang.Modules.DbModule.md). The repository containing a comprehensive set of examples can be found at [PLangHQ/plang - Db Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Db).

### Selecting Data
One of the most common database operations is selecting data from a table. The following plang code example demonstrates how to select all records from a table and store them in a variable.

```plang
- select * from items, write to %items%
```

This step maps to the `Select` method in the `Db` module with the following default C# signature:
```csharp
Task<dynamic?> Select(string sql, List<object> Parameters = null, bool selectOneRow_Top1OrLimit1 = false)
```

### Inserting Data
Another frequent operation is inserting data into a table. Below is a plang code example that inserts a new record and retrieves the ID of the inserted row.

```plang
- insert into tasks (description, due_date) values ('New task', '2024-07-01 21:47:43'), write to %taskId%
```

This step corresponds to the `InsertAndSelectIdOfInsertedRow` method in the `Db` module with the default C# signature:
```csharp
Task<object> InsertAndSelectIdOfInsertedRow(string sql, List<object> Parameters = null)
```

For more detailed documentation and additional examples, please refer to [PLang.Modules.DbModule.md](./PLang.Modules.DbModule.md) and the [PLangHQ/plang - Db Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Db) repository. To understand the implementation details, examine the Program.cs source code at [PLang.Modules.DbModule - Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/DbModule/Program.cs).

## Source code
The source code for the `Db` module is organized into several files:
- [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/DbModule/Program.cs) contains the runtime code for the module.
- [Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/DbModule/Builder.cs) is responsible for building the steps during the plang build process.
- [ModuleSettings.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/DbModule/ModuleSettings.cs) includes settings specific to the `Db` module.

## How plang is mapped to C#
Modules in plang are utilized through a two-step process involving the Builder and Runtime.

### Builder
During the build process, the following steps occur:
1. The Builder reads the .goal file and parses each step (lines starting with `-`).
2. For each step, the StepBuilder sends a query to the LLM along with a list of all available modules.
3. The LLM suggests a module to use, such as `PLang.Modules.DbModule`.
4. The Builder sends all the methods in the `Db` module to the LLM along with the step.
5. Depending on the availability of `Builder.cs`, either this file or `BaseBuilder.cs` is used to process the LLM's response.
6. The Builder creates a hash of the response and saves a JSON instruction file with the `.pr` extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
During runtime, the `.pr` file is executed as follows:
1. The plang runtime loads the `.pr` file.
2. Reflection is used to load the `PLang.Modules.DbModule`.
3. The "Function" property in the `.pr` file specifies the C# method to call.
4. If required, parameters are provided as specified in the `.pr` file.

### plang example to csharp
Here is how a plang code example is mapped to a method in the `Db` module and represented in a `.pr` file:

#### plang code example
```plang
- create table tasks (description TEXT, due_date DATETIME), write to %result%
```

#### Mapping to C# method
This maps to the `CreateTable` method in the `Db` module.

#### Example Instruction .pr file
```json
{
  "Action": {
    "FunctionName": "CreateTable",
    "Parameters": [
      {
        "Type": "string",
        "Name": "sql",
        "Value": "CREATE TABLE tasks (description TEXT, due_date DATETIME)"
      }
    ],
    "ReturnValue": {
      "Type": "int",
      "VariableName": "result"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:49:44.