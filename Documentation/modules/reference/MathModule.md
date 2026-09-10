# MathModule

Hint: `[math]`  
Type: `PLang.Modules.MathModule.Program`

Solves math expressions

## Methods

### Fibonacci

```
Fibonacci(Int32 variable) : Nullable<Int32>
```

Find the fibonacci number of a given variable or number.


### PrimeNumbers

```
PrimeNumbers(Int32 number) : List<Int32>
```

Find the first n prime numbers where n is a given number.


### SolveExpression

```
SolveExpression(String expression, Int32 decimalRound = 2, Nullable<MidpointRounding> midpointRounding = null) : Object
```

Solve a complex math expression given as a string. Please capitalize any functions called like sqrt() into Sqrt()


