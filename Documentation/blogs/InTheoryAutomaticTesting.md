# In Theory: Automatic Unit Testing with Plang

> This post dives into Plang, an intent-based programming language that interprets natural language. For more, visit [plang.is](https://plang.is) or [get started here](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%201.md).

In Plang, you can bind events to goals and steps, which opens up a fascinating possibility for automatic testing. Let's walk through an example, starting with something simple: adding two numbers.

### A Simple Example: Adding Two Numbers

Here’s a basic code snippet in Plang:

```plang
AddTwoNumbers
- %num1% + %num2%, write to %sum%
- write out %sum%
```

To build automatic unit tests, let's first think about the usual process of testing logic manually. Without automation, you’d run something like this:

```bash
plang num1=1 num2=2
```

This would output:

```bash
3
```

### Automating the Process

Because Plang lets us bind events to steps, we can automate this process. For example:

```plang
Events
- before each step, call PrepareUnitTest
- after each step, call MakeUnitTest
```

Here’s how it works:
- The `PrepareUnitTest` goal stores the variables in the current step.
- The `MakeUnitTest` goal captures the output, like the value of `%sum%`.

Once that’s in place, we can even ask the [LLM to generate unit tests](https://chatgpt.com/share/66e7f108-4690-8003-ac91-7d4f99a50da7) for us. This results in a JSON file with the relevant test values.

### A Developer’s Interface

At this point, a developer would likely need a simple interface—a GUI—to tweak those values, add new tests, or remove unnecessary ones. Each module would define its own rules for unit testing, especially for handling I/O operations like HTTP requests, which would require special considerations.

With this setup, automatic testing becomes a streamlined part of the development process, enhancing efficiency and reliability in Plang projects.

## In Theory

This post is titled `In Theory` because, at this point, automatic unit testing with Plang is just that—a theory. It hasn’t been implemented or tested yet, but there’s no reason it couldn’t be. The concept is sound; it’s just a matter of engineering time.