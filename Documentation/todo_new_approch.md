
# Todo New Approach Documentation

## Overview
This documentation outlines the approach for creating a todo list that mimics the natural process of writing tasks on pen and paper. It involves setting up a system that interprets text input and organizes it into a structured list of tasks.

## Setting Up the `system.txt` File
1. Create a new folder named `llm` in the `api` folder.
2. Inside the `llm` folder, create a file named `system.txt`.
3. Populate `system.txt` with the following content:

```txt
User will provide you with a text of tasks. 
Your job is to analyze the text and create a list of tasks to return.

Current date & time is %Now%

task: the description of the task
due_date: when the task is due, if not defined, set default to 2 days from now
category: categorize the user input by 3 categories, 'Work', "Home", 'Hobby'
```

## Creating the `NewLlmTask.goal`
1. In the `api` folder, create a file named `NewLlmTask.goal`.
2. Add the following plang code to `NewLlmTask.goal`:

```plang
NewLlmTask
- read file llm/system.txt, write to %system%, load vars
- [llm] system:%system%
        user: %request.tasks%
        scheme:[{task:string, due_date:date, category:string}]
        model:'gpt-4-1106-preview'
        write to %taskList%
- for each %taskList%, call !SaveTask

SaveTask
- insert into Todos table, %item.task%, %item.due_date%, %Identity%
```

## Explanation of Code
- Notice the path when reading the file is `llm/system.txt`, since your code is in `api` folder, the `llm` folder MUST be in the `api` folder
- It then processes the user's tasks using LLM from the HTTP request, defines a schema, and writes the results to the `%taskList%` variable.
- The `SaveTask` goal is called for each task in `%taskList%` to save the task to the database.


## Tip
- Use the syntax `for each %taskList%, call !SaveTask %task%=item` to reference the `%task%` variable within the `SaveTask` goal.

## Testing the New Approach
1. Create a new file in the `test` folder named `TestNewLlmTask.goal`.
2. Add the following plang code to `TestNewLlmTask.goal`:

```plang
TestNewLlmTask
- post http://localhost:8080/api/NewLlmTask
    {
        "tasks":"toothbrush
            toothpaste
            new oil for car, tomorrow
            milk
            talk with boss about salary, 2 days before end of month
            solve credit card payment in system, in 7 days
            "
    }
    timeout 2 min
    write to %result%
- write out %result%
```

> Note: We set the timeout for the http request to 2 minutes, since llm can take longer then the default 30 sec.

## Build and Run web server
Lets start by building and starting the web server
```plang
plang exec
```

This should build the new code and start your server. If it was already running, make sure to restart it.

## Run test
Next we execute the the test

```plang
plang run test/TestNewLlmTask
```

After it has runned, lets retrieve all the tasks.

```plang
plang run test/TestTasks
```
