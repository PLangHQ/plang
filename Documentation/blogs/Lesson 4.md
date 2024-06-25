# How does it really work?

Let's go back to this example
```plang
ReadFile
- read file.txt, into %content%
- write out %content%
```

What the builder does, is read the goal file and break up all the steps. It does a bit analyzing using llm on the goal and then for each step it asks the llm what [module](../modules/README.md) would fit the step.

We suggest some 30 modules to the LLM, one of them is called FileModule. "Hey", says the llm, "I think it the [`FileModule`](../modules/PLang.Modules.FileModule.md)".

Plang builder then recieves that it `FileModule`. Ok, "here are all the methods inside the `FileModule`, can you select the one that fits with the intent of the user."

One of those methods is `ReadTextFile(string path) : string`, so if we call the `ReadTextFile` function for this step, I will get the text of the file read into the `%content%` variable.

And it repeats for the next step, `write out %content%` maps to out [`OutputModule.Write(object content)`](../modules/PLang.Modules.OutputModule.md) method

So that is how it works. Not to complex.

Next is [Lesson 5 : Error and Events](./Lesson%205.md)