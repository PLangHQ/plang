# Todo New Approach

Welcome to the Todo New Approach tutorial! In this guide, we will explore a new way of designing user experiences for applications, specifically focusing on a Todo app. This approach emphasizes making the computer adjust to the user, rather than the user adjusting to the computer.

## Video
[![Watch the video](https://img.youtube.com/vi/0hSfGJYCBf8/hqdefault.jpg)](https://www.youtube.com/watch?v=0hSfGJYCBf8&list=PLbm1UMZKMaqfT4tqPtr-vhxMs4JGGFVEB&index=5)

## What You Will Learn:
- How to rethink the design of user experiences.
- How to make the computer adjust to the user, not the other way around.

## Introduction

In the real world, creating a todo list is as simple as taking a pen and paper and jotting down tasks. This natural process is what we aim to replicate in our software design. Let's dive into how we can achieve this.

## Setting Up the Environment

1. **Create a New Folder:**
   - Inside the `api` folder, create a folder named `llm`.

2. **Create `system.txt`:**
   - Inside the `llm` folder, create a file named `system.txt`.
   - This file will simplify changing system commands without rebuilding the code.
   - Path: `/api/llm/system.txt`

3. **Add the Following to `system.txt`:**
   ```txt
   User will be provid you with a text of tasks. 
   You job is to analyze the text and create list of tasks to return.

   Current date & time is %Now%

   task: the description of the task
   due_date: when the task is due, if not defined, set default to 2 days from now
   category: categorize the user input by 3 categories, 'Work', "Home", 'Hobby'
   ```

## Creating the Task Processing Logic

1. **Create `NewLlmTask.goal`:**
   - In the `api` folder, create a file named `NewLlmTask.goal`.

2. **Add the Following Code:**
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

   **Explanation:**
   - The code reads the system command from `system.txt` and processes the user's tasks from an HTTP request.
   - It defines a schema and writes the result to the `%taskList%` variable.
   - It then loops through the list and saves each item to the database.

   **Tip:** You can write `for each %taskList%, call !SaveTask %task%=item` to reference the `%task%` variable in the `SaveTask` goal.

## Testing the New Approach

1. **Create a Test File:**
   - In the `test` folder, create a file named `TestNewLlmTask`.

2. **Add the Following Code:**
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

## Build, restart and run

Lets build our code

```bash
plang build
```

After you have build it, restart your webserver, kill the process and run following in your `Todo` folder

```bash
plang
```

Now lets test this, run the following from your `Todo` folder

```bash
plang test/TestNewLllmTask
```

## What is This New Approach?

Think about how you create a todo list in the physical world. You simply write it down without worrying about structure. In today's software, we often use structured forms for data input, which can be cumbersome for users.

This tutorial demonstrates a breakthrough in user experience design by eliminating the need for structured forms. The computer now adjusts to the user, allowing for a more natural interaction. This approach revolutionizes how we interact with software, making it more intuitive and user-friendly.

## Next Steps

Congratulations! You have completed the tutorial and are now ready to start writing your own apps. To further your learning:

- Explore how to build your steps in [How do I know how to build my steps](./modules/README.md).
- Check out more [Examples](https://github.com/PLangHQ/plang/tree/main/Tests).
- Discover [Apps written by others](https://github.com/PLangHQ/apps) to gain inspiration.

All resources are open source, allowing you to view and learn from the code. Happy coding!