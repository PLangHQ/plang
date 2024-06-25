# Lesson 2 - Basic Concepts in Plang

We will start by learning how to structure your code. This is natural language programming language, but you have to follow some rules.

First are the files & folders. There are few important

- Start.goal - This is the default entry point into a plang app
- Setup.goal - This is where you setup the system, create tbls, insert config data
- Events folder - You can bind events on goals and steps
- .build folder - where your code is compiled to
- .db folder - contains the database

Those are the important once. 

## Goal
It's a goal. You want to accomplish some goal. RegisterUser is a goal, as you know, registering user are multiple steps

## Steps
- Each goal has one or more steps. 
- Each step starts with a dash(-)
- Each step defines the intent of the developer, e.g.

```plang
- read file.txt into %content%
```
I feel I dont have to explain what this code does, do I?

Just in case, the developer wants the app to read the `file.txt`, and put the text into the variable `%content%`

## Variables

Variables are defined with starting and ending %. Here is example of the %name%, %users%, %userInfo% variables
```plang
- set %name% = "jonny"
- select * from users, write to %users%
- get https://jsonplaceholder.typicode.com/users/1, %userInfo%
```

Now you can use those variables
```plang
- write out 'Hello %name%'
- write out 'There are %users.Count%'
- write out 'The user email is %userInfo.email%'
```

> Advanced: Underlying runtime is C#, so you can use Properties and Methods(limited now) from the c# library, in the above exmple I used `%users.Count%`, Count is a property on the List class

# %Now%

Current time is always important. So you can access it like this `%Now%` or `%NowUtc%`
All the properties and methods are available

You can also say `%Now+1day%`, `%Now+1hour%`, `%Now+1ms%`. [Read more about Time](../Time.md)

## Goal file structure

Give the goal file a good name that defines the goal you want to be achived.

The file always start with the name of the goal
```plang
ReadFile
```

It should be the same as the name of the file minus the '.goal'.

then come steps, what do you need to do to accomplish this goal

each step starts with a dash(-), it can be multiple lines but new line cannot start with dash(-)

```plang
ReadFile
- read file.txt, into %content%
- write out %content%
```

Those are the steps in the goal `ReadFile`. They are easy to understand

Now you know how plang is structured, goal, steps and what variable is

Next is the [Lesson 3](./Lesson%203.md)