# In Theory: Instant compile time with plang

I will go through how the builder works, takes your step, converts to executable code. I will show how plang will be close to instant compile time within 1 year(August 2024). No matter how big the project is or how many files are in it.

### How the builder works.

The builder takes your goals and goes through each step, it figures out with help of LLM how to map it to methods(functions) in C#. These instructions are then stored in a .pr file. It is a simple json. You can read it. Basic programmer will be able to understand it.

Now that we have instruction for our steps, we never have to compile that statement ever again. Think about it, you never have to compile that code again. Not unless it changes.

So the only thing that is sent to llm to compile is the step you just created, nothing else. This means even now with gpt-4o-mini, we could send the step and get answer withing 500ms. That is a pretty fast build time.

Now imagine when it's free, when you have the LLM running on your computer. What will be created?

### How the process works

Best with example:

```plang
- read file.txt, into %content%
```

Plang buidler takes this, and ask the LLM to give a module that you think will work. It will return the file module, and it will generate this json

```json

```

Then when plang runs the code, it loads this json, and using reflection/dynamic it loads the class described int the .pr files, the function and parameters and executes that method.

This of course has huge impact on security, the execution layer is now text file that can be signed. 



