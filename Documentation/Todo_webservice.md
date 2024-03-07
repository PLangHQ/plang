# Todo Webservice Guide

This guide will walk you through the process of creating a Todo web service using the Plang programming language. 


## Prerequisites

- Plang installed & IDE setup [installed on your system](Install.md)
- Optional: A REST client for testing the API endpoints

## What will be covered
In this tutorial we will cover the basics for working with plang. This includes.

- Database tables
- Start web server
- Goals (functions) / Variables / Date & time
- Handle web request
- Validation
- Error handling
- Insert into database
- Respond to web request
- Testing


## Steps

### 1. Create a Project Directory

Create a new directory named `Todo` at your preferred location. For instance, you can create it at `c:\apps\Todo` on Windows.

For Linux and MacOS, you can create it at `~/apps/Todo`.

### 2. Create `Setup.goal` 

In the `Todo` directory, create a new file named `Setup.goal` and add the following code:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
```

Note: Plang will automatically create and manage an `id` column for the table. By allowing Plang to handle `id` you enable syncing between devices. It's a technique called Event sourcing. You don't need to know anything about it at this stage.

### 3. Build and Run `Setup.goal`

> Cost estimate: $0.23

Execute the `Setup.goal` file to create the `Todos` table in the database.

```bash
plang exec Setup
```

### 4. Create `Start.goal`

In the `Todo` directory, create a new file named `Start.goal` and add the following code:

```plang
Start
- start webserver
```
This will create a web server running on your computer, at http://localhost:8080 (link will not work just yet).

### 5. Create `api` Directory

In the `Todo` directory, create a new directory named `api`. `api` stands for application programming interface, it is how computers communicate between them.

### 6. Create `NewTask.goal`

In the `api` directory, create a new file named `NewTask.goal` and add the following code:

```plang
NewTask
- make sure that %request.task% and %request.due_date% is not empty, throw error
- insert into Todos %request.task%, %request.due_date%, write to %id%
- write out %id%
```

### 7. Create `List.goal`

In the `api` directory, create a new file named `List.goal` and add the following code:

```plang
List
- select everything from Todos, write to %todos%
- write out %todos%
```
Note: if you are familiar with SQL, you could also write `select * from Todos`, previous statment is just in a more natural language

### 8. Build and Run the Code 

> Cost estimate: $1.10

If you are using VS Code, then press F5 on your keyboard to build and run the code.

If you prefer terminal, navigate to the root of the `Todo` directory and execute the following command:

```bash
plang exec
```

This will start a web server with the logic you've defined.

### 9. Test the API Endpoints

> Cost estimate: $0.47

Lets create some data in your Todo list.

- Create `test` folder in the root of `Todo`
- Create a new `TestNewTask.goal` file in the `test` directory with the following code:
    ```plang
    TestNewTask
    - post http://localhost:8080/api/NewTask
        {
            "task":"Do homework",
            "due_date": "%Now+2days%"
        }
        write to %result%
    - write out %result%
    ```

Then, execute the `TestNewTask.goal` file:

- Press F5 in VS Code, in the prompt window type in `TestNewTask` and press enter. When you type in `TestNewTask` into the prompt, you are telling plang that you want to run a specific goal
- or if you prefer terminal

    ```bash
    plang exec test/TestNewTask
    ```

You should receive the `id` of the new task in the response.

Alternativly, you can test the API endpoints using your favorite REST client.

To create a new task, send a POST request to `http://localhost:8080/api/NewTask` with the following JSON body:

```json
{
    "task":"Do homework",
    "due_date": "2023-12-27"
}
```
### 10. Retrieve all tasks

To retrieve the list of tasks, send a GET request to [http://localhost:8080/api/List](http://localhost:8080/api/List) (you can click the link). This should return a list of tasks you've created.

Alternatively, you can create a new `TestList.goal` file in the `test` directory with the following code:

> Cost estimate: $0.156

```plang
TestList 
- get http://localhost:8080/api/List, write to %todos%
- write out %todos%
```

- Press F5 in VS Code, in the prompt window type in `TestList` and press enter
- or if you prefer terminal
    ```bash
    plang exec test/TestList 
    ```

## [Next, add LLM to this todo app >](./Todo_Llm.md)
