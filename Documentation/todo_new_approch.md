# Todo New Approach Documentation

## Introduction

In the pursuit of creating a more intuitive and natural todo application, we are adopting a methodology that mirrors the simplicity of jotting down tasks on a piece of paper. This document will guide you through the process of setting up a system that can interpret and organize text-based task inputs into a structured todo list.

## What will be covered
In this tutorial we will cover the basics for working with plang. This includes.

- Rethink how we make apps
    - Computers adapt to user, not vice versa
    - Keep the structured data
- Read file from disk
- For statements


## Setting Up the System Configuration

### Step 1: Create the Configuration Folder and File

- Navigate to the `api` directory and create a new subdirectory named `llm`.
- Within the `llm` directory, create a file named `system.txt`.

### Step 2: Define System Commands

- Open the `system.txt` file and insert the following configuration:

```txt
User will provide you with a text of tasks. 
Your job is to analyze the text and create a list of tasks to return.

Current date & time is %Now%

task: the description of the task
due_date: when the task is due, if not defined, set default to 2 days from now
category: categorize the user input by 3 categories, 'Work', 'Home', 'Hobby'
```

## Implementing the `NewLlmTask.goal`

### Step 1: Create the Goal File

- In the `api` folder, create a new file named `NewLlmTask.goal`.

### Step 2: Write the Goal Logic

- Add the following plang code to the `NewLlmTask.goal` file:

```plang
NewLlmTask
- read file llm/system.txt, write to %system%, load vars
- [llm] system:%system%
        user: %request.tasks%
        scheme:[{task:string, due_date:date, category:string}]
        write to %taskList%
- for each %taskList%, call !SaveTask

SaveTask
- insert into Todos table, %item.task%, %item.due_date%, %item.category%, %Identity%
```

### Explanation of the Code

- The `NewLlmTask` goal starts by reading the `system.txt` configuration and loading the variables.
- It processes the user's task input using the LLM model specified in the HTTP request, adhering to a defined schema, and stores the output in the `%taskList%` variable.
- The `SaveTask` goal is then invoked for each task in `%taskList%`, inserting the task details into the database.

### Tip for Iteration

- Utilize the syntax `for each %taskList%, call !SaveTask %task%=item` to loop through the tasks and reference the `%task%` variable within the `SaveTask` goal.

## Testing the Implementation

### Step 1: Create the Test File

- In the `test` directory, create a file named `TestNewLlmTask.goal`.

### Step 2: Define the Test Logic

- Populate the `TestNewLlmTask.goal` file with the following plang code:

```plang
TestNewLlmTask
- post http://localhost:8080/api/NewLlmTask
    {
        "tasks": "toothbrush
                  toothpaste
                  new oil for car, tomorrow
                  milk
                  talk with boss about salary, 2 days before end of month
                  solve credit card payment in system, in 7 days"
    }
    timeout 2 min
    write to %result%
- write out %result%
```

> Note: The HTTP request timeout is set to 2 minutes to accommodate the potential processing time required by the LLM.

## Building and Running the Web Server

To build and start the web server, execute the following command:

```plang
plang exec
```

Ensure that you restart the server if it was already running to apply the new changes.

## Executing the Test

Run the test with the following command:

```plang
plang run test/TestNewLlmTask
```

After the test execution, you can retrieve all the tasks using:

```plang
plang run test/TestTasks
```

This completes the setup and testing of the new approach for the todo application using plang.