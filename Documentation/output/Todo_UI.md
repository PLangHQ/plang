# Todo Window App Guide

This guide will walk you through the process of converting a todo web service into a window app using the plang programming language. 

For a more detailed walkthrough, you can watch this step-by-step video tutorial: [Todo Window App Tutorial](https://www.youtube.com/watch?v=abew4btk34)

> **Warning:** As of version 0.1, the window app only runs on Windows. Future versions are planned to support all platforms (desktop, mobile, tablets, TV, etc.). Please note that the window app is currently in early Alpha stage and serves more as a proof of concept than a fully functional application.

## Steps

### 1. Modify Start.goal

Change the `Start.goal` file as follows:

```plang
Start
- start window app, call !Todos
```

### 2. Create UI Folder

Create a new folder named `ui`.

### 3. Create Todos.goal

In the `ui` folder, create a new file named `Todos.goal` with the following content:

```plang
Todos
- select * from todos, where not completed, newest first, write to %todos%
- button, name="New task", call !NewTask
- table for %todos%
    header: Task, Due Date
    body:  task, due_date
```

### 4. Create NewTask.goal

In the `ui` folder, create another file named `NewTask.goal` with the following content:

```plang
NewTask
- form, inputs for "task" and "due_date"(DateTime)
- button "Save", call !SaveTask
```

### 5. Create SaveTask.goal

Next, create a file named `SaveTask.goal` in the `ui` folder with the following content:

```plang
SaveTask
- if %task% or %due_date% is empty, throw error
- insert into Todos, %task%, %due_date%
- call !Todos
```

### 6. Run the App

The app is now ready to run. Use `plangw.exe` (not `plang.exe`) to run it. 

For Windows:

```bash
plangw exec
```

For Linux and MacOS, you might need to make `plangw` executable first:

```bash
chmod +x plangw
./plangw exec
```

Enjoy your new Todo Window App!