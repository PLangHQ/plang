# Understanding Loops in Plang

## What are Loops?

Loops are a fundamental concept in programming that allow you to repeat a set of instructions multiple times. Imagine you have a list of tasks to complete, and you want to perform the same action on each task. Loops help you automate this process, so you don't have to write the same code over and over again.

### ELI5 Explanation

Think of a loop like a conveyor belt in a factory. Each item on the belt is processed in the same way. Once an item is processed, the belt moves, and the next item is processed. This continues until all items have been handled.

## Examples of Loops in Plang

### Simple Example

In Plang, when you loop through a list, a new variable `%item%` is automatically created for you. Here's a simple example:

```plang
Start
- [code] generate list from 1 to 10, write to %numbers%
- go through %numbers%, call PrintNumber

PrintNumber
- write out %item%
```

**Explanation:**  
This code generates a list of numbers from 1 to 10 and then loops through each number, calling the `PrintNumber` goal. Inside `PrintNumber`, it writes out the current number (`%item%`).

### Define Your Own `%item%` Variable

You can customize the name of the `%item%` variable to make your code more readable:

```plang
Start
- [code] generate list from 1 to 10, write to %numbers%
- loop through %numbers%, call PrintNumber %number%=item

PrintNumber
- write out %number%
```

**Explanation:**  
Here, `%item%` is renamed to `%number%`, making it clear that the loop is iterating over numbers.

### Listing Files in a Folder

Let's write out all the files in a folder:

```plang
Files
- get files in './', write to %files%
- foreach %files%, call !LoadText

LoadText
- write out 'Reading: %item.path%'
- read file %item.path% into %content%
- write out 'This is the content: %content%'
```

**Explanation:**  
This code retrieves all files in the current directory and loops through them, reading and displaying their content.

### Working with HTTP Requests

You can also use loops with HTTP requests. For example, fetching cat facts:

```plang
CatFacts
- GET https://cat-fact.herokuapp.com/facts, write to %facts%
- go through %facts% !ShowFact fact=%item%

ShowFact
- write out '%fact.text% was created at %fact.createdAt%'
```

**Explanation:**  
This code fetches cat facts from an API and loops through each fact, displaying its text and creation date.

## Did You Notice?

In each example, the instruction to loop through the list was different: `loop through`, `foreach`, `go through`. Plang is designed to be written in natural language, so you can express your intentions clearly without worrying about specific keywords.

## Default Values in Loop Calls

When you use a loop in Plang, some default values are automatically available:

- `%list%`: The list you're looping through.
- `%item%`: The current item in the loop.
- `%position%`: The position of the current item, starting at 0.
- `%listCount%`: The total number of items in the list.

### Example with Default Values

```plang
ShowFact
- Write out "This is fact nr. %position% of %listCount%, this is the %item%, from %list%"
```

**Explanation:**  
This code uses default loop variables to display the position and total count of items in the list.

### Customizing Default Names

You can overwrite the default names of these values to make your code clearer or to meet specific requirements of the goal you're calling:

```plang
Products
- select everything from products table, newest first, write to %products%
- go through %products% call !ProcessProduct %productList%=list, %product%=item, %productPosition%=position, %numberOfProducts%=listCount

ShowProduct
- Write out "This is product nr. %productPosition% of %numberOfProducts%, this is the %product%, from %productList%"
```

**Explanation:**  
In this example, the default loop variables are renamed to better reflect the context of processing products.

## Best Practices for Beginners

1. **Be Clear and Consistent:** Use descriptive names for your loop variables to make your code easy to understand.
2. **Start Simple:** Begin with basic loops and gradually add complexity as you become more comfortable.
3. **Test Your Code:** Run your loops with different data to ensure they work as expected.
4. **Use Comments:** Add comments to explain the purpose of your loops and any complex logic.

## Next Step

Learn about [Date and Time](./Time.md) in Plang.