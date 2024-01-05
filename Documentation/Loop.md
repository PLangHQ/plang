# Introduction to Loops in PLang

Loops are a fundamental part of programming in PLang, allowing you to repeat actions with different data or until a certain condition is met.

## ELI5 Explanation of Loops

Imagine you have a deck of cards, and you want to show each card one by one. You start with the first card, show it, move to the next, and continue until you've shown all the cards. In programming, a loop does something similar: it goes through items in a list, one at a time, and does something with each item.

## Understanding Loops in PLang

In PLang, loops are used to go through each item in a list, file, or data received from HTTP, and perform actions on each item.

### Syntax for Looping
```plang
- loop %list% call !GoalForItem
```

## Practical Examples

### Loop Through Lists
```plang
- select * from users, write to %users%
- loop %users% call !ProcessUser
```

### Looping Through Files and HTTP Data
```plang
Files
- get files in 'c:\dir', write to %files%
- foreach %files%, call !LoadText

LoadText
- read file %item.path% into %content%
```
```plang
CatFacts
- GET https://cat-fact.herokuapp.com/facts, write to %facts%
- go through %facts% !ShowFact fact=%item%

ShowFact
- write out '%fact.text% was created at %fact.createdAt%'
```

Notice how the syntax is natural and not strict to `foreach` or `for(int i=...)`. In the examples the intent is described of going through a list, such as `go through %facts%`

## Default Values in Loop Calls

When you use a loop in PLang, some default values are automatically available:

- `%list%`: The list you're looping through.
- `%item%`: The current item in the loop.
- `%position%`: The position of the current item. %position% starts at 1
- `%listCount%`: The total number of items in the list.

### Example with Default Values
```plang
ShowFact
- Write out "This is fact nr. %position% of %listCount%, this is the %item%, from %list%"
```

You can overwrite the default names of those values. This can be helpful to make your code clear or of the goal you are calling requires specific name.
```plang
Products
- select everything from products table, newest first, write to %products%
- go through %products% call !ProcessProduct list=%productList%, item=%product%, position=%productPosition%, listCount=%numberOfProducts%

ShowProduct
- Write out "This is product nr. %productPosition% of %numberOfProducts%, this is the %product%, from %productList%"
```

## Best Practices

- Use descriptive names for your list and item variables.
- Keep the actions within the loop as simple and clear as possible.

## Summary and Key Takeaways

Loops in PLang are powerful tools for handling repetitive tasks on collections of data, like lists, files, or data from external sources.
