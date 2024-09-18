# In Theory: Instant Compile Time with Plang

Let’s take a look at how the Plang builder works and why it can deliver near-instant compile times, no matter the size of your project. This system compiles only what’s new or changed, making build times essentially disappear.

### How It Works

The Plang builder takes your goals file, processes each step, and uses an LLM to map it to methods in a module. These instructions are saved in a `.pr` file, a simple JSON that anyone can read. Once a step is processed and mapped, you never need to compile it again unless you change it.

That’s the core idea—compile once, never again, unless you modify something. So when you run the builder, the only thing sent to the LLM is the step you’ve just written. With GPT-4o-mini, this happens in around 500ms, making build times almost instant. [Read more about the builder here](https://github.com/PLangHQ/plang/blob/main/Documentation/Builder.md).

### Example in Action

Here’s a simple example:

```plang
- read file.txt, into %content%
```

The builder looks at this step and asks the LLM which module to use. It picks the file module and generates this JSON, which gets saved in a `.pr` file:

```json
"Action": {
    "FunctionName": "ReadTextFile",
    "Parameters": [
      {
        "Type": "String",
        "Name": "path",
        "Value": "file.txt"
      },
      {
        "Type": "String",
        "Name": "returnValueIfFileNotExisting",
        "Value": null
      },
      {
        "Type": "Boolean",
        "Name": "throwErrorOnNotFound",
        "Value": true
      }
    ],
    "ReturnValues": null
  }
```

Next time you run the code, Plang loads this JSON file, dynamically loads the class, passes in the parameters, and runs the method. It doesn’t need to compile that step again because nothing has changed. This is faster and more secure, too. The code execution is based on text files that can be signed and reviewed—no more hidden behavior in compiled binaries.

### Commit the `.build` Folder

Here’s something different: you should commit the `.build` folder. That way, when another developer pulls your project, they won’t need to rebuild the entire codebase. All the compiled steps are already there, saving time for everyone.

### The Future of Instant Compiling

If you never change the step `- read file.txt, into %content%`, it won’t ever be rebuilt. Plang only rebuilds what’s new or modified. That means even in large projects, only the lines you work on are affected. 

Today, this already gives you decent build times, but looking ahead, it gets even better. Plang could eventually monitor your changes in real-time. As soon as you write a new step, it could start compiling the previous one immediately. With faster LLMs—and even better, when it runs locally—you’ll reach a point where you never wait for builds.

No more staring at progress bars while waiting for code to compile. [Here’s the future we’re aiming for](https://xkcd.com/303/).

![Compiling](https://imgs.xkcd.com/comics/compiling.png)
