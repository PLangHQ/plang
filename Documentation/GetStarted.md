# Get Started with Plang

Welcome to the world of Plang, a dynamic and versatile programming language! This guide is specifically designed to cater to new Plang users. Whether you're on Windows, Linux, or macOS, this guide will help you navigate through the initial stages of Plang programming with ease. We'll begin by creating a simple "Hello World" application, then progress to a more complex Todo app on a webserver, and finally transform it into a desktop application.

## Introduction

Before diving into the coding, please ensure that you have Plang installed on your system. For installation instructions, refer to [Install.md](./Install.md). This guide will not only help you set up Plang but also familiarize you with using Plang's integration with Language Learning Models (LLM), currently powered by GPT-4. You can either use the Plang service or, if you have your GPT-4 API key, refer to [gpt4.md](./gpt4.md) for setup instructions.

## Hello World

Watch this helpful video tutorial: [Hello World in Plang](https://www.youtube.com/watch?v=iGW4btk34yQ)

1. **Create a Folder**: Make a new folder named `HelloWorld`. For example:
   - Windows: `C:\plang\HelloWorld`
   - Linux/MacOS: `/home/user/plang/HelloWorld`

2. **Create Start.goal File**: In the `HelloWorld` folder, create a file named `Start.goal`.

3. **Write Your Code**:
    ```plang
    Start
    - write out 'Hello plang world'
    ```

4. **Save the File**: After writing the code, save the `Start.goal` file.

5. **Execute the Code**:
   - Press `F5` in your Plang editor or run it from the console/terminal with the following command:
     ```bash
     plang exec
     ```

6. **Output**: Your code will compile and execute, displaying "Hello plang world" on the screen.

## Todo App

Follow along with this detailed video guide: [Building a Todo App in Plang](https://www.youtube.com/watch?v=abew4btk34)

1. **Create a Todo Folder**: 
   - Windows: `C:\plang\Todo`
   - Linux/MacOS: `/home/user/plang/Todo`

2. **Create Setup.goal**:
    ```plang
    Setup
    - create table Todos 
        columns: task(string, not null), due_date(datetime, not null), 
                 completed(bool, false), created(datetime, now)
    ```
    Note: Plang automatically manages the 'Id' column for tables, enabling powerful features like Event Source.

3. **Build the Database Structure**:
    ```bash
    plang exec Setup
    ```

4. **Create Start.goal**:
    ```
    Start
    - start webserver
    ```

5. **Create 'api' Folder** in your Todo directory.

6. **Create NewTask.goal**:
    ```plang
    NewTask
    - ensure %task% and %due_date% are not empty, else throw error
    - insert into Todos %task%, %due_date%, write to %id%
    - write out %id%
    ```

7. **Create List.goal**:
    ```plang
    List
    - select everything from Todos, write to %list%
    - write out %list%
    ```

8. **Build and Run**: In the root of the Todo folder, press F5 or use the console/terminal:
    ```bash
    plang exec
    ```

9. **Testing the App**: 
   - Use a REST client or Plang to interact with your webserver.
   - Add a task: POST to `http://localhost:8080/api/newtask` with JSON body:
     ```
     {
         "task": "Do homework",
         "due_date": "2023-27-12"
     }
     ```
   - Retrieve tasks: GET `http://localhost:8080/api/list`

Congratulations! You've now set up a functional webserver with Plang. As you explore more, remember that Plang is designed to be intuitive and powerful, making your programming experience enjoyable and efficient. Happy coding with Plang!