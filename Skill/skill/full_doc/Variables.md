# Plang Variables Documentation

## Introduction to Variables

In programming, a variable is like a container that holds information. This information can be a number, text, or any other data type. Think of a variable as a labeled box where you can store and retrieve data whenever you need it. In Plang, variables are enclosed within percentage signs `%` to distinguish them from regular text.

### ELI5 Explanation

Imagine you have a toy box labeled "Toys." You can put toys in it, take toys out, or even replace the toys with something else. In programming, a variable works similarly. You give it a name, like "Toys," and you can store different things in it, like numbers or words. When you want to use what's inside, you just refer to the label.

## Setting Variables in Plang

To set a variable in Plang, you use the `set` command followed by the variable name enclosed in `%`, an equal sign `=`, and the value you want to assign to it. Here's an example:

```plang
Start
- set %name% = "John"
- write out %name%
```

### Explanation

In the above code:
- We start by setting a variable `%name%` to the value `"John"`.
- The `write out` command then outputs the value of `%name%`, which is "John".

## Date and Time Variables

Plang provides built-in variables for date and time, such as `%Now%`, which represents the current date and time. Here's how you can use it:

```plang
Start
- write out %Now%
```

### Explanation

In this code:
- The `write out` command outputs the current date and time using the `%Now%` variable.

## Conditions

Conditions allow you to execute certain parts of your code based on whether a condition is true or false. You can read more about conditions in the [Conditions documentation](./Conditions.md).

```plang
- set %isValid% = true
- if %isValid% is true, call WriteOutIsValid

WriteOutIsValid
- write out %isValid%
```

### Explanation

In this example:
- We set a variable `%isValid%` to `true`.
- The `if` statement checks if `%isValid%` is true. If it is, it calls the `WriteOutIsValid` goal.
- The `WriteOutIsValid` goal writes out the value of `%isValid%`, which is `true`.

## Loops

Loops allow you to repeat a set of instructions for each item in a collection. When using loops in Plang, a new variable `%item%` is created to represent the current item in the loop. You can read more about loops in the [Loops documentation](./Loops.md).

```plang
Start
- [code] generate list from 1 to 10, write to %numbers%
- go through %numbers%, call PrintNumber

PrintNumber
- write out %item%
```

### Explanation

In this loop example:
- We generate a list of numbers from 1 to 10 and store it in the variable `%numbers%`.
- The `go through` command iterates over each number in `%numbers%`, calling the `PrintNumber` goal for each one.
- The `PrintNumber` goal writes out the current `%item%`, which represents each number in the list as the loop progresses.

By understanding and using variables, conditions, and loops, you can create dynamic and flexible programs in Plang.