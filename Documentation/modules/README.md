# Modules

Every step you write becomes one call of one function in one module. For example:

```plang
- read file "work/hello.txt", write to %text%
```

This step becomes `FileModule.ReadTextFile`.

## Where to look

- **[Module reference](./reference/README.md)** lists every module, every function the builder can turn a step
  into, and each function's parameters, types and defaults. It is generated from the code by
  `dotnet run --project Tools/ModuleDocGen`, so it matches the runtime. It also gives the hint that forces a module,
  e.g. `[file]`.
- **[Writing steps that build](../WritingStepsThatBuild.md)** gives one checked wording for each common task, and
  the function each wording becomes.
- **[Check the build](../CheckTheBuild.md)** shows how to see what a step became, and what to do when it became the
  wrong thing.

## Modules that are not built in

The [modules repository](https://github.com/PLangHQ/modules) has modules that do not come with plang. It also
shows how to write your own: a `.dll` in the app's `.modules` folder adds its functions to the builder.
