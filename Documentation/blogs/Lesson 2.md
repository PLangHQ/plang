# Lesson 2: Basic Concepts in Plang

We will start by learning how to structure your code. This is a natural language programming language, but you have to follow some rules.

First are the files & folders. There are a few important ones:

- `Start.goal` - This is the default entry point into a Plang app.
- `Setup.goal` - This is where you set up the system, create tables, and insert config data. This only runs once in the lifetime of your application.
- `Events` folder - You can bind events to goals and steps.
- `.build` folder - Where your code is compiled to.
- `.db` folder - Contains the database.

Those are the important ones to learn first.

## Goal

A goal is something you want to accomplish, similar to a function/method in other languages. 

`GetProductInfo` is a goal. Getting product information involves multiple __steps__, such as retrieving the data from a database and then displaying the data.

## Steps

- Each goal has one or more steps.
- Each step starts with a dash (`-`).
- Each step defines the intent of the developer, e.g.

Step example:
```plang
- read file.txt into %content%
```
I feel I don't have to explain what this code does, do I?

Just in case, the developer wants the app to read the `file.txt` and put the text into the variable `%content%`.

## Variables

Variables are defined with starting and ending `%`. Here are examples of the `%name%`, `%users%`, `%userInfo%` variables.

Variable examples: 
```plang
- set %name% = "jonny"
- select * from users, write to %users%
- get https://jsonplaceholder.typicode.com/users/1, %userInfo%
```

Now you can use those variables:
```plang
- write out 'Hello %name%'
- write out 'There are %users.Count%'
- write out 'The user email is %userInfo.email%'
```

> Advanced: The underlying runtime is C#, so you can use Properties and Methods from the C# API. In the above example, I used `%users.Count%`. `Count` is a property on the `List` class.

## %Now%

Current time is always important. You can access it like this: `%Now%` or `%NowUtc%`. All the properties and methods are available.

You can also say `%Now+1day%`, `%Now+1hour%`, `%Now+1ms%`. [Read more about Time](../Time.md).

## Goal File Structure

Give the goal file a good name that defines the goal you want to achieve.

The file always starts with the name of the goal:
```plang
ReadFile
```

It should be the same as the name of the file minus the '.goal'.

Then come the steps, what do you need to do to accomplish this goal?

Each step starts with a dash (`-`). It can be multiple lines, but new lines cannot start with a dash (`-`).

```plang
ReadFile
- read file.txt, into %content%
- write out %content%
```

Those are the steps in the goal `ReadFile`. They are easy to understand.

Now you know how Plang is structured: goal, steps, and what a variable is.

Next is [Lesson 3](./Lesson%203.md).