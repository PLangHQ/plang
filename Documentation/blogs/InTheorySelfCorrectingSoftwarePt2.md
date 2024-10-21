# In Theory: Self-Correcting Software - Part 2

In [previous article about self correcting software](./InTheorySelfCorrectingSoftware.md) I demonstrated how a plang code can self correct it self. 
It's a simple example, and simple proof of concept. 

Let's take this bit deeper

Plang compiles into json files. It's an Instruction. This json is saved into a `.pr` file. This is important to understand and remember(the `.pr` file).
It tells the plang runtime how to execute your statement.

Let take this example

```plang
- read file.txt to %content%
```
The instruction for this will be
```json
{
    "FunctionName": "ReadTextFile",
    "Parameters" : [{"path": "file.txt"}]
}
```

The plang runtime will load up the [FileModule](https://github.com/PLangHQ/plang/tree/main/Documentation/modules#file) and
if you look into the [Program.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/FileModule/Program.cs) file, you will find a method called `ReadTextFile`

This is the status of plang v.0.1.*

Now let's imagine that we want to refactor and change the name for the method, to `ReadContentOfTextFile`

Any plang program reading a text file that was build before this change will fail to run.

It will give an error, specifically it will give you an error with the key = "MissingMethod"

In the [previous article](./InTheorySelfCorrectingSoftware.md) I showed how you can bind events to goals(function) or steps(line of code)

So lets use this ability

We start by creating the event listener.

```plang
Events
- on any error where key="MissingMethod", call FixCode
```

Let's now fix the code

First we need to get all the available methods that are in the `FileModule`
```plang
- get all available methods in %!error.ModuleType%, write to %methods%
```

So now that we have all the methods in the `FileModule`, this includes `ReadContentOfTextFile`

It looks something like this:
```json
[{
    "Method": "public async Task<string> ReadContentOfTextFile(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false, bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")",
    "Description": "Former ReadTextFile"
}, 
    "Methdod":"public async Task WriteToFile(string path, string content, bool overwrite = false, bool loadVariables = false, bool emptyVariableIfNotFound = false, string encoding = "utf-8")",
    "Description": null
}]

Next we want to read the instruction file for the step that got the error. 

```plang
- read %!step.AbsolutePrPath%, into %instructions%
```

In plang you always has access to the step that is running through the `%!step%` variable. There are more variables that start with !. They are many [reserved variables](https://github.com/PLangHQ/plang/blob/main/PLang/Utils/ReservedKeywords.cs) in the system.

One property of the `%!step%` variable is `AbsolutePrPath`. This point to the file path of the `.pr` file (remember .pr file from above?)

So in this step, we read the content the the `.pr` file and have the instructions that was executed and got the error

So next we ask the llm to fix the code

```plang
- [llm] system: User is trying to execute code but the method was not found, most likely it was renamed
            These are instructions to call the c# code: %instructions%
            These are the methods available to call: %methods%
            Rewrite the %instructions%
        user: %!step.Text%
        scheme: {plangCode:string}
```

We give the Llm all the information, the information we would need if we wanted to fix the error and in return the Llm will give new `%plangCode%` variable with the fixed code

How do I know? Check out the plang assistant on ChatGpt, how he solves it.

We then write down the code, into the .pr file
```
- write %plangCode% to %!step.AbsolutePrPath%
```
(or git or what ever your process is)


and finally we retry to execute the step

```plang
- retry step
```

With the final code being

```plang
FixCode
- get all available methods in %!error.ModuleType%, write to %methods%
- read %!step.AbsolutePrPath%, into %instructions%
- [llm] system: User is trying to execute code but the method was not found, most likely it was renamed
            These are instructions to call the c# code: %instructions%
            These are the methods available to call: %methods%
            Rewrite the %instructions%
        user: %!step.Text%
        scheme: {plangCode:string}
- write %plangCode% to %!step.AbsolutePrPath%
- retry step
```


## In-theory article
This is in-theory article, it will run in Plang when modules are available and language is mature. 
There is nothing that prevents this code from being written and run. Modules that are not available yet, 'retry step', 'get all available methods...'
when those are done, it will work. 

## Plang upgrade path
This opens up for a very interesting upgrade path for plang. It's still early in plang developemtn and I am breaking built plang code all the time, this will give a upgrade path 
that can be close to automated. There is much more engineering needed, but that is just engineering, the problem is solved.

## Modules upgrade paths

It's easy to [extend the plang language using modules](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/README.md), e.g. to read file paths of a Google drive folder, you install the [Google.Drive module](https://github.com/PLangHQ/modules/tree/main/Google.Drive)

```plang
Start
- get list of all my files in "tAff2u44jU2nZgpJkbY5mSwW3" on google drive, %files%
- write out "List of all files: %files%"
- go through %files%, call ProcessFile

ProcessFile
- download file from google drive, fileId=%item.id%, save it to file/%item.name%
```

Now let say that you are using this module and the developer updates that module, change the method names. 
It would not affect you at all all with this setup, it would correct everything automatically for you.

Caution tho. We programmers dont really like something to happen automatically with out confirming that it works the same way. Thats where unit test come in.
Later on that.