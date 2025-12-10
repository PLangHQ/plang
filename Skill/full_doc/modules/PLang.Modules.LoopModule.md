
# Loop

## Introduction
Loops are fundamental constructs in programming that allow you to repeat a set of instructions until a certain condition is met. In PLang, loops enable you to automate repetitive tasks efficiently, such as processing items in a list or dictionary.

## For Beginners
A loop is like a set of instructions that you want to follow over and over again. Imagine you have a to-do list for your daily routine. Instead of writing the same tasks for each day of the week, you decide to write them once and then repeat them every day. In programming, a loop works similarly. It allows you to repeat tasks without writing the same code multiple times.

## Best Practices for Loop
When using loops in PLang, it's important to ensure that your loop will eventually stop. Otherwise, it could run forever and cause your program to freeze. To avoid this, make sure that the condition for the loop to continue is one that will eventually become false.

### Example:
Suppose you have a list of numbers and you want to print each number until you reach a number that is greater than 10.

```plang
- add 5 to list, write to %numbers%
- add 8 to list, write to %numbers%
- add 12 to list, write to %numbers%
- go through %numbers% call !PrintNumber, item=%number%, list=%numbers%, key=1

PrintNumber
- if %number% <= 10 then call !Print, else !EndLoop
- write out %number%

!Print
- write out "Number: %number%"

!EndLoop
- write out "Number is greater than 10, ending loop."
```

In this example, the loop will stop when it encounters the number 12 because it's greater than 10.


# Loop Module Documentation

The Loop module in PLang allows for the execution of loops, such as while, for, and foreach, to iterate through lists and dictionaries. Below are examples of how to use the Loop module in PLang, sorted by their popularity and typical use cases.

## Examples

### Foreach Loop Through a List of Products
This example demonstrates how to loop through a list of products and call a goal for each product.

```plang
- add {"Name":"Product1", "Price":111} to list, write to %products%
- add {"Name":"Product2", "Price":222} to list, write to %products%
- go through %products% call !ShowProduct, item=%product%, list=%products%, key=1
```

### Foreach Loop Through a Dictionary
This example shows how to loop through a dictionary and call a goal for each key-value pair.

```plang
- add 'key1', 'Hello', write to %dict%
- add 'key2', 'PLang', write to %dict%
- add 'key3', 'World', write to %dict%
- loop through %dict%, call !PrintDict
```

### ShowProduct Goal
The `ShowProduct` goal is called for each product in the list, outputting the product's name, price, and the total count of products.

```plang
ShowProduct
- write out %product.Name% - %product.Price% - %products.Count% idx:1, listCount:1, key:%key%
```

### PrintDict Goal
The `PrintDict` goal is called for each entry in the dictionary, outputting the key and value.

```plang
PrintDict
/ listCount gives -1 on dictionary objects
- write out %item.Key% - %item.Value%, listCount:1, ["c:\\Users\\Ingi Gauti\\source\\repos\\plang\\Tests\\Loop\\Loop.goal"]
```

## Method Description

The `RunLoop` method is used to iterate through a list or dictionary. It requires the name of the variable to loop through and the name of the goal to call for each iteration. An optional dictionary of parameters can also be passed.

### RunLoop Method Example with Parameters
This example demonstrates calling the `RunLoop` method with additional parameters.

```plang
- add {"Name":"Product3", "Price":333} to list, write to %products%
- add {"Name":"Product4", "Price":444} to list, write to %products%
- go through %products% call !ShowProduct, item=%product%, list=%products%, key=1, max size 3mb, expires in 2 weeks
```

In this example, the `RunLoop` method is used to iterate through the `%products%` list, calling the `!ShowProduct` goal for each product. Additional parameters such as `max size` and `expires in` are provided to demonstrate how to pass parameters to the called goal.


For a full list of loop examples, visit [PLang Loop Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Loop).

## Step Options
When writing your PLang code, you can enhance your loops with additional step options. Click on the links below for more detail on how to use each option:

- [CacheHandler](/modules/CacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancellationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For those who are interested in diving deeper into the mechanics of loops in PLang and how they interact with C#, check out the [advanced documentation](./PLang.Modules.LoopModule_advanced.md).

## Created
This documentation was created on 2024-01-02T22:02:59.
