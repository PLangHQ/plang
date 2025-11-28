


## Common Mistakes and How to Fix Them
#### ❌ INCORRECT: Inconsistent variable naming
```plang
- read file.txt into data
- hash password write to hashedPassword
- get user from db, id=%userId%, write %User%
```

#### ✅ CORRECT: Consistent %variable% syntax
```plang
- read file.txt into %data%
- hash %password%, write to %hashedPassword%
- get user from db where id=%userId%, write to %user%
```

**Rule**: ALWAYS use `%variableName%` syntax for variables.



### Variables handling
#### ❌ INCORRECT: creating new variable from object to reuse
```plang
- set %name% = %contract.name%
- write out "%name%"
```

#### ✅ CORRECT: use the existing variable
```plang
- write out "%contract.name%
```