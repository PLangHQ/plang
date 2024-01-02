```markdown
# Get Started with plang

## Introduction

Welcome to the world of plang, a versatile programming language that runs on Windows, Linux, and macOS. This guide will walk you through the basics of plang, from setting up your environment to creating your first applications. By the end of this guide, you'll have a solid foundation to build upon.

Before we dive in, make sure you have plang installed on your system. If you haven't done so yet, please refer to the [Installation Guide](Install.md).

## Prerequisites

- plang must be installed on your system.
- Visual Studio Code is recommended as your editor, with the plang extension installed.

## Hello World

[Watch the Hello World tutorial video](https://www.youtube.com/watch?v=iGW4btk34yQ)

For first-time users, you'll need to configure your plang service account for a simple setup or use a GPT-4 API key for advanced usage.

### Steps to create a Hello World app:

1. Create a folder named `HelloWorld` in your preferred location, for example, `C:\plang\HelloWorld` on Windows.
2. Inside the folder, create a file named `Start.goal`.
3. Enter the following code into `Start.goal`:

    ```plang
    Start
    - write out 'Hello plang world'
    ```

4. Save the file.
5. To run the program, press `F5` or execute it from the console/terminal with the following command:

    ```bash
    plang exec
    ```

6. The code should build and then execute, displaying "Hello plang world" on the screen.

## Todo App

[Watch the Todo app tutorial video](https://www.youtube.com/watch?v=abew4btk34)

### Steps to create a Todo app:

1. Create a folder named `Todo` in your preferred location, for example, `C:\plang\Todo` on Windows.
2. Inside the folder, create a file named `Setup.goal` with the following content:

    ```plang
    Setup
    - create table Todos 
        columns:    task(string, not null), due_date(datetime, not null), 
                    completed(bool, false), created(datetime, now)
    ```

    Note: plang automatically creates and manages an `Id` column for each table, enabling powerful features like Event Sourcing for device syncing.

3. Build and run `Setup.goal` to create the database table:

    ```bash
    plang exec Setup
    ```

4. Create a file named `Start.goal` with the following content:

    ```plang
    Start
    - start webserver
    ```

5. Create a folder named `api` within the `Todo` directory.
6. Inside the `api` folder, create a file named `NewTask.goal`:

    ```plang
    NewTask
    - make sure that %task% and %due_date% are not empty, throw error if they are
    - insert into Todos %task%, %due_date%, write to %id%
    - write out %id%
    ```

7. Create a file named `List.goal`:

    ```plang
    List
    - select everything from Todos, write to %list%
    - write out %list%
    ```

8. Build and run the code from the root of the `Todo` folder by pressing `F5` or using the console/terminal:

    ```bash
    plang exec
    ```

    You now have a running webserver with the Todo logic implemented.

9. To interact with your Todo app, use a REST client or plang itself. Here's how to add a new task using a REST client:

    POST to `http://localhost:8080/api/newtask` with the JSON body:

    ```json
    {
        "task": "Do homework",
        "due_date": "2023-12-27"
    }
    ```

    You should receive the ID of the new task in response.

    To list all tasks, send a GET request to `http://localhost:8080/api/list`. You should see the task you just created in the response.
```

Remember to adjust the file paths and commands according to your operating system. For example, on macOS or Linux, you might create a folder with `mkdir ~/plang/HelloWorld` and navigate to it with `cd ~/plang/HelloWorld`.