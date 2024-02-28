# Todo Identity Integration Guide

Welcome to the Todo Identity Integration Guide! This document will walk you through the process of enhancing your Todo web service with user-specific functionality using the `%Identity%` variable in `plang`. By the end of this guide, you'll be able to show tasks that belong to a specific user, ensuring a personalized experience.

Before you begin, make sure you're familiar with the concept of `%Identity%` by reading [what is %Identity%](./Identity.md). You should also have completed the [previous tutorials](./Todo_webservice.md) to have the necessary `.goal` files ready.

## Prerequisites

- Understanding of `%Identity%` ([Identity](./Identity.md))
- Completion of [previous tutorials](./Todo_webservice.md)

## Step-by-Step Integration

### Step 1: Modify `Setup.goal`

First, you'll need to add an 'Identity' column to your 'Todos' table. Update your `Setup.goal` file with the following step:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
- add column 'category' to tbl 'Todos'    
- add column 'Identity'(string) to tbl 'Todos'             
```

### Step 2: Ensure `%Identity%` is not empty

Create an `events` folder at the root of your Todos project and add a file named `Events.goal` with the following content:

```plang
Events
- before each goal in 'api/*', call !CheckIdentity
```

Create a new file, `CheckIdentity.goal` in Events folder
```plang
CheckIdentity
- if %Identity% is empty, call !ShowError

ShowError
- write out error 'You need to sign the request'
```

### Step 3: Update `NewTask.goal`

Incorporate `%Identity%` into the insert statement within `NewTask.goal`:

```plang
NewTask
- make sure that %request.task% and %request.due_date% is not empty, throw error
- insert into Todos %request.task%, %request.due_date%, %Identity%, write to %id%
- call !Categorize, dont wait
- write out %id%

Categorize
- [llm]: system: categories the user input by 3 categories, 'Work', "home", 'hobby'
    user: %request.task%
    scheme: {category:string}
- update table tasks, set %category% where %id%
```

### Step 4: Adjust `List.goal`

Modify `List.goal` to filter tasks based on `%Identity%`:

```plang
List
- select everything from Todos where %Identity%, write to %todos%
- write out %todos%
```

### Step 5: Test Creating a New Task

Testing with a REST client is no longer an option due to the need for `%Identity%` generation. Instead, write the test in `plang`:

Update the `TestNewTask.goal` file in the `test` directory:

```plang
TestNewTask
- post http://localhost:8080/api/newtask
    {
        "task":"Do some grocery shopping",
        "due_date": "%Now+2days%"
    }
    write to %result%
- write out %result%
```

Execute the `TestNewTask.goal` file:

- In VS Code, press F5, type `TestNewTask`, and press enter.
- Or, in the terminal, run:

    ```bash
    plang exec test/TestNewTask
    ```

### Step 6: Retrieve Your New Task

Create a `plang` client to fetch the task list, which should now return only tasks associated with the `%Identity%`:

Create `TestTasks.goal` in the `tests` folder:

```plang
TestTasks
- get http://localhost:8080/api/list
    write to %todos%
- write out %todos%
```

Execute the `TestTasks.goal` file:

- In VS Code, press F5, type `TestTasks`, and press enter.
- Or, in the terminal, run:

    ```bash
    plang exec test/TestTasks
    ```

## Next tutorial
- If you are running on a Windows machine (sorry, only Windows for now), let's change the web service [into a desktop app](./Todo_UI.md)
- Else, check out some more [Examples](https://github.com/PLangHQ/plang/tree/main/Tests) or other [Apps written by others](https://github.com/PLangHQ/apps) to start learning. It is all open source and you can view all the code.

Happy coding with `plang`!