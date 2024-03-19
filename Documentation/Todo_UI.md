# Todo Window App Guide

This guide will walk you through the process of converting a todo web service into a window app using the plang programming language. 

For a more detailed walkthrough, you can watch this step-by-step video tutorial: [Todo Window App Tutorial](https://www.youtube.com/watch?v=abew4btk34)

> **Warning:** As of version 0.1, the window app only runs on Windows. Future versions are planned to support all platforms (desktop, mobile, tablets, TV, etc.). Please note that the window app is currently in early Alpha stage and serves more as a proof of concept than a fully functional application.

## What will be covered
In this tutorial we will cover the basics for working with plang. This includes.

- Start window app
- How to make layout

## Steps

### 1. Create StartWindow.goal

> Cost estimated: $0.14

Create the `StartWindow.goal` file as follows:

```plang
StartWindow
- start window app, call !Todos
```
Here we are telling plang to call the goal Todos when it start the window app

### 2. Create UI Folder

Create a new folder named `ui`.

### 3. Create Todos.goal

> Cost estimated: $0.45

In the `ui` folder, create a new file named `Todos.goal` with the following content:

```plang
Todos
- select * from todos, where not completed, newest first, write to %todos%
- button, name="Add task", call !AddTask
- table for %todos%
    header: Task, Category, Due Date
    body:  task, category, due_date
```

### 4. Create AddTask.goal

> Cost estimated: $0.228

In the `ui` folder, create another file named `AddTask.goal` with the following content:

```plang
AddTask
- form, inputs for "task"(required) and "due_date"(required, type is date)
- [ui] button "Save", call !NewTask  %task%, %due_date%
- call !Todos
```
Note: `NewTask.goal` was created in previous tutorial
### 5. Run the App

The app is now ready to run. 

- Press F5 in your VS Code and it will start a window app and show you your todo list.
- or if you prefer terminal, use `plangw.exe` (not `plang.exe`) to run it. 

    For Windows:

    ```bash
    plangw exec StartWindow
    ```

    For Linux and MacOS, sorry, we don't support you at the moment

# Next tutorial
- If you haven't looked at it, checkout how you can [rethink how you design](./todo_new_approch.md) your apps
- Check out some more [Examples](https://github.com/PLangHQ/plang/tree/main/Tests) or other [Apps written by others](https://github.com/PLangHQ/apps) to start learning. It is all open source and you can view all the code.
