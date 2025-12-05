


## Common Mistakes and How to Fix Them

Variables are global for the context that is running

#### ❌ INCORRECT: Overwriting variable

LoadUser
- select * from users where id=%id%, return 1, write to %user%
- call goal LoadSomeProperty
- write out %user% / this will only contain type of the user, since LoadSomeProperty overwrote %user%

LoadSomeProperty
- select type from users where id=%user.id%, return 1, write to %user%

#### ✅ CORRECT: variable defined for it's purpose
LoadUser
- select * from users where id=%id%, return 1, write to %user%
- call goal LoadSomeProperty
- write out %user% / this will now contain the full user info

LoadSomeProperty
- select type from users where id=%user.id%, return 1, write to %userType%

#### ❌ INCORRECT: Set variable default value when receiving variable from request

LoadProduct
- set default value %productId% = %request.body.productId%

#### ✅ CORRECT: use route pattern

In Start.goal or where route is added to webserver, include expected variable in path

AddRoute
- add route /product/%productId%, call goal Product

%productId% is then accessible in the goal being called
validation can be added to the route, e.g.

- add route /product/%productId%(number > 0), call goal Product
- add route /activate/%activeStatus%(bool), call goal Activate

This is not always the case, e.g. when submitting multiple variables like on POST, then just use the request object in the code without creating new variable.


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