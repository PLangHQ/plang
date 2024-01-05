# Introduction to `if` Statements in PLang

`if` statements are a fundamental part of programming in PLang, allowing your program to make decisions based on certain conditions.

## ELI5 Explanation of `if` Statements

Imagine you're deciding what to wear. If it's raining, you wear a raincoat. If it's sunny, you wear a hat. Here, you're making a choice based on the weather. In programming, `if` statements work similarly. They let your program choose what to do based on certain conditions.

## Syntax of `if` Statements in PLang

In PLang, an `if` statement checks a condition and runs different goals or steps based on whether that condition is true.

### Basic Format
```plang
- if %condition% then call !GoalName, else !ElseGoalName
```

### With Sub-steps
```plang
- if %condition% then
   - step1
   - step2
```

## Using `if` Statements

`if` statements are useful when you want different parts of your program to run under different conditions.

**Example 1: Simple Condition**
```plang
- if %isAdmin% then call !ShowAdmin, else !ShowUser
```

**Example 2: With Sub-steps**

Sub-steps are indented with 4 spaces. This is a rule

```plang
- if %isAdmin% then
   - call !ShowAdmin
   - write out 'This is admin'
- if %isUser% then
   - call !ShowUser
   - write out 'This is user'
```

In these examples, the program decides which goal to call based on whether `%isAdmin%` or `%isUser%` is true.

## Else, Elseif

You can have `if` and `else` in one line condition (see Example 1)

PLang doesn't have `else` or `elseif` with sub steps, so you need to write separate `if` statements for each condition.

## Best Practices

- Keep conditions simple and clear.
- Use meaningful names for your variables to make the conditions understandable.

## Summary and Key Takeaways

`if` statements in PLang are powerful tools that help your program make decisions and execute different code based on conditions.
