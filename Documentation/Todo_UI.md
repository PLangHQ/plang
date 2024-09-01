# Todo Window App Tutorial

Welcome to the Todo Window App tutorial! In this guide, we will transform a previous Todo web service into a window application using Plang. This tutorial will walk you through the process step-by-step, providing you with the necessary knowledge to create a window app, structure its layout, and call goals from your UI.

## What You Will Learn
- How to create a window app
- How to structure the layout
- How to call goals from your UI

## Video Walkthrough
For a more detailed walkthrough, you can watch the accompanying video tutorial [here](https://www.youtube.com/watch?v=RJYv5PUz9bY&list=PLbm1UMZKMaqfT4tqPtr-vhxMs4JGGFVEB&index=4).

[![Video Thumbnail](https://img.youtube.com/vi/RJYv5PUz9bY/hqdefault.jpg)](https://www.youtube.com/watch?v=RJYv5PUz9bY&list=PLbm1UMZKMaqfT4tqPtr-vhxMs4JGGFVEB&index=4)

## Important Note
- **Warning**: At version 0.1, the app only runs on Windows. It is in a very early Alpha stage and is more of a proof of concept than a fully functional application. Future versions aim to support all platforms (desktop, mobile, tablets, TV, etc.).

## Steps to Create the Todo Window App

### Step 1: Modify Start.goal
Create or modify the `Start.goal` file to initiate the window app and call the `Todos` goal.

```plang
Start
- start window app, call !Todos
```

### Step 2: Create UI Folder
Create a folder named `ui` to organize your UI-related files.

### Step 3: Create Todos.goal
Create a `Todos.goal` file to define the main interface of the Todo app.

```plang
Todos
- select * from todos, where not completed, newest first, write to %todos%
- button, name="New task", call !NewTask
- table for %todos%
    header: Task, Due Date
    body:  task, due_date
```

**Explanation**: This code fetches all incomplete todos, displays them in a table, and provides a button to add a new task.

### Step 4: Create NewTask.goal
Create a `NewTask.goal` file to handle the creation of new tasks.

```plang
NewTask
- form, inputs for "task" and "due_date"(DateTime)
- button "Save", call !SaveTask
```

**Explanation**: This code creates a form with inputs for a task and its due date, along with a save button that triggers the `SaveTask` goal.

### Step 5: Create SaveTask.goal
Create a `SaveTask.goal` file to save new tasks to the database.

```plang
SaveTask
- if %task% or %due_date% is empty, throw error
- insert into Todos, %task%, %due_date%
- call !Todos
```

**Explanation**: This code checks if the task or due date is empty, throws an error if so, otherwise inserts the new task into the Todos database and refreshes the Todos view.

### Step 6: Run the App
To run the app, use `plangw.exe` instead of `plang.exe`.

```bash
plangw exec
```

**Explanation**: This command executes the window app using the Plang window executable.

## Next Steps
To further enhance your app, consider learning how to rethink the UX by following the guide in [todo_new_approch.md](./todo_new_approch.md).

By following this tutorial, you should now have a basic Todo window app running on your system. Happy coding!