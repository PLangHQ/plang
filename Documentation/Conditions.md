# Introduction to Conditions in Plang

In programming, conditions are like decision-making tools that help your program decide what to do next based on certain criteria. Think of them as traffic lights for your code: they tell your program when to stop, go, or take a different route. Conditions allow your program to execute different actions depending on whether certain conditions are true or false.

## ELI5 Explanation of Conditions

Imagine you're playing a game where you have to decide what to do based on the weather. If it's sunny, you might decide to go outside and play. If it's raining, you might choose to stay indoors and read a book. Conditions in programming work similarly. They help your program make decisions based on the information it has, just like you decide what to do based on the weather.

## Using Conditions in Plang

In Plang, conditions are used to control the flow of your program. They allow you to execute certain steps only if specific conditions are met. Let's look at some examples to understand how conditions work in Plang.

### Example 1: If Statements with Sub Steps

```plang
Start
- if %isAdmin% then
   - call !ShowAdmin
   - write out 'This is admin'
- if %isUser% then
   - call !ShowUser
   - write out 'This is user'
```

In this example, the program checks if the variable `%isAdmin%` is true. If it is, the program will execute the sub-steps: it will call the `!ShowAdmin` goal and write out "This is admin". Similarly, if `%isUser%` is true, it will call the `!ShowUser` goal and write out "This is user".

### Example 2: If Statements Calling Other Goals

```plang
Start
- if %isAdmin% then call !ShowAdmin, else !ShowUser
```

Here, the program checks if `%isAdmin%` is true. If it is, it calls the `!ShowAdmin` goal. If `%isAdmin%` is false, it calls the `!ShowUser` goal instead. This is a more compact way to handle conditions where you have a clear alternative action.

### Important Note

In Plang, you cannot start steps with `- else` or `- else if`. These will not create valid condition statements. Always ensure that your conditions are structured correctly to avoid errors in your program.

By understanding and using conditions, you can make your Plang programs more dynamic and responsive to different situations. Conditions are a fundamental part of programming, and mastering them will greatly enhance your ability to create complex and useful programs.