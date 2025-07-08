# Math

## Introduction
The Math module in plang provides a variety of mathematical functions and operations that allow users to perform calculations, solve equations, and work with numbers easily. It is designed to simplify complex mathematical tasks and make them accessible to everyone, regardless of their programming experience.

## For beginners
Math is a way to work with numbers and perform calculations. In plang, the Math module helps you do things like add, subtract, multiply, and divide numbers, as well as find square roots and powers. You can think of it as a tool that allows you to solve math problems using simple commands. Even if you are new to programming, you can use the Math module to perform calculations without needing to understand complicated programming concepts.

## Best Practices for Math
When writing plang code for the Math module, it's important to follow some best practices to ensure your code is clear and effective. Here are a few tips:

1. **Use Clear Variable Names**: Always use descriptive names for your variables. This makes it easier to understand what each variable represents. For example, instead of using `%x%`, use `%sum%` to indicate that it holds a sum.

2. **Break Down Complex Expressions**: If you have a complicated calculation, break it down into smaller steps. This makes it easier to read and debug. For example:
   ```plang
   - what is 5 plus 7, write to %sum%
   - what is 10 times %sum%, write to %result%
   - write out %result%
   ```

3. **Follow the plang Rules**: Remember the rules of plang, such as how to define goals and steps, and how to use variables. This will help you avoid errors in your code.

# Math Module Documentation

## Examples

### Solve Math Expressions
```PLang
Solve_Expression
- what is sqrt(4), write to %result%
- solve for "(sqrt(100) + %result% + pow(2, 3)) / %result%", write to %result%
- write out "result of (sqrt(100) + 2 + pow(2, 3)) / 2 is: %result%"
- solve 5 + (12 - square root of 9) to the power of 2 all divided by 2 after then subtract 1, write to %test%
/ could also do (5 + (12 - sqrt(9))^2) / 2 - 1
- write out %test%
```

### Basic Arithmetic Operations
```PLang
Arithmetic
- what is 5 plus 7, write to %sum%
- write out %sum%
- what is 7 + 7.6533333, write to %sum%
- write out %sum%
- what is 9 times 6, write to %product%
- write out %product%
- what is %sum% minus 633, write to %difference%
- write out %difference%
- what is -8 - 0.93, write to %difference%
- write out %difference%
- find the quotient of 9 divided by 0, write to %quotient%
- write out %quotient%
```

### Powers and Roots
```PLang
Power_Root
- what is 8 to the power of 2, write to %power%
- write out %power%
- what is -2 ^ 3, write to %power%
- write out %power%
- solve for sqrt(90), write to %firstResult%
- write out "square root of 90 is: %firstResult%"
```

### Prime Numbers
```PLang
Prime
- find the first 20 prime numbers, write to %primes%
- write out each item in %primes%
- find the 10th prime number, write to %tenthPrime%
- write out %tenthPrime%
```

### Fibonacci Numbers
```PLang
Fibonacci
- find the 20th fibonacci number, write to %fib%
- write out %fib%
- find the 1st fibonacci number, write to %fib%
- write out %fib%
- find the 3rd fibonacci number, write to %fib%
- write out %fib%
```

### Trigonometric Functions
```PLang
Trig
- what is tangent of 45 degrees, write to %tan%
- write out %tan%
- what is sin of pi, write to %sin%
- write out %sin%
```

## All Possible Functions
- Basic arithmetic (+, -, *, /)
- trigonometry (sin(x), cos(x), tan(x), arcsin(x), arccos(x), arctan(x))
- square roots (sqrt(x))
- exponents (pow(base, exponent))
- expressions (x + y / z)
- logarithms (log(value, base))
- absolute values (abs(x))
- eulers (exp(x) is the same as e^3)
- rounding (floor(x), ceiling(y), round(value, #decimalPlaces), truncate(x))
- min and max (minimum(x), maximum(x))
- return first n prime numbers
- find the nth fibonacci term

## Examples
- You can find the source code of the Math module at [Math Module Source Code](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/MathModule/Program.cs).
- For a list of test cases, visit [Math Test Cases](https://github.com/PLangHQ/plang/tree/main/Tests/Math).

## Step options
These options are available for each step. You can click the links for more details on how to use them:
- [CacheHandler](../CachingHandler.md)
- [ErrorHandler](../ErrorHandler.md)
- [RetryHandler](../RetryHandler.md)
- [CancelationHandler](../CancelationHandler.md)
- [Run and forget](../RunAndForget.md)

## Advanced
For more advanced information, check out the [Advanced Math Module Documentation](./PLang.Modules.MathModule_advanced.md) if you want to understand how the underlying mapping works with C#.

## Created
This documentation is created 2025-07-05T16:31:55