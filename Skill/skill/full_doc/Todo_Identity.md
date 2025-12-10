# TodoIdentity Tutorial

Welcome to the TodoIdentity tutorial! In this guide, we will enhance our Todo web service by integrating the `%Identity%` feature. This will allow us to display tasks specific to individual users, demonstrating the use of the built-in `%Identity%` variable in Plang.

## What You Will Learn

- How to integrate `%Identity%` into your Todo web service.
- Understanding the `%Identity%` variable and its significance.
- Using Plang's Events to simplify and secure your application.
- Modifying existing goals to incorporate user-specific data.
- Testing your application using Plang.

## What is `%Identity%`

In short, `%Identity%` acts like a `user_id` in your system. For a more detailed understanding, read [What is %Identity%](./Identity.md).

## Video Tutorial

[![TodoIdentity Video Tutorial](https://img.youtube.com/vi/0FYqllGVOQU/hqdefault.jpg)](https://www.youtube.com/watch?v=0FYqllGVOQU&list=PLbm1UMZKMaqfT4tqPtr-vhxMs4JGGFVEB&index=4)

## Prerequisites

Before proceeding, ensure you have completed the previous tutorials to have the necessary `.goal` files. Start with the [previous tutorials](./Todo_webservice.md).

## Step-by-Step Guide

### 1. Change `Setup.goal`

Add the following step to your `Setup.goal` file:

```plang
- add column 'Identity' to tbl 'Todos'
```

After adding it, your `Setup.goal` file should look like this:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
- add column 'category' to tbl 'Todos'    
- add column 'Identity' to tbl 'Todos'             
```

#### Run Setup

Lets create that column in our db.

```bash
plang exec Setup
```

You should now see `Identity` in your Todos table

### 2. Ensure `%Identity%` is Not Empty

*You will learn how to use [Events](./Events.md) and the power they give you to make your app simple and flexible.*

- Create a folder `events` in the root of Todos.
- Create a file `Events.goal` in the `events` folder.

```plang
Events
- before any api/*, call CheckIdentity
```
The `Events.goal` file cannot contain goal, so lets create a new file `CheckIdentity.goal`

```plang
CheckIdentity
- if %Identity% is empty then
    - write out error 'You need to sign the request'
```

This setup ensures authentication for your program in just three lines of code.

### 3. Change `NewTask.goal`

Let's add `%Identity%` into the insert statement. Modify `NewTask.goal` as follows:

```plang
NewTask
- make sure that %task% and %due_date% is not empty, throw error
- insert into Todos %task%, %due_date%, %Identity%, write to %id%
- call !Categorize, dont wait
- write out %id%

Categorize
- [llm]: system: categories the user input by 3 categories, 'Work', "home", 'hobby'
    user: %task%
    scheme: {category:string}
- update table tasks, set %category% where %id%
```

We added `%Identity%` into the `- insert into ...` statement.

### 4. Change `List.goal`

Filter on the `%Identity%` when selecting the list:

```plang
List
- select everything from Todos where %Identity%, write to %todos%
- write out %todos%
```

We added a `where` statement to the `- select everything...` step, filtering by your `%Identity%`.

### 5. Test Creating New Task

If you used your favorite REST client before, this is no longer an option. You need to write the test in Plang because the program needs to generate `%Identity%` for us.

Modify `TestNewTask.goal` file in the `test` directory with the following code:

```plang
TestNewTask
- post http://localhost:8080/api/newtask
    {
        "task":"Do some grocery shopping",
        "due_date": "2023-27-12"
    }
    write to %result%
- write out %result%
```

Then, execute the `TestNewTask.goal` file:

- Press F5 in VS Code, in the prompt window type in `TestNewTask` and press enter.
- Or if you prefer terminal:

    ```bash
    plang exec test/TestNewTask
    ```

### 6. Get My New Task

Now we need to write a Plang client that retrieves the task list. We should only get one (not three) result back since we are now filtering on `%Identity%`.

Create `TestTasks.goal` in the `tests` folder:

```plang
TestTasks
- get http://localhost:8080/api/list
    write to %todos%
- write out %todos%
```

- Press F5 in VS Code, in the prompt window type in `TestTasks` and press enter.
- Or if you prefer terminal:

    ```bash
    plang exec test/TestTasks
    ```

## Next Steps

- If you are running on a Windows machine, let's change the web service [into a desktop app](./Todo_UI.md).
- Else, [learn how you can rethink](./todo_new_approch.md) how you make apps, making the computer adapt to the user.

Check out some more [Examples](https://github.com/PLangHQ/plang/tree/main/Tests) or other [Apps written by others](https://github.com/PLangHQ/apps) to start learning. It is all open source, and you can view all the code.