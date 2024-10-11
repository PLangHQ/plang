# Todo Web Service Tutorial

Welcome to the Todo Web Service tutorial! In this guide, you will learn how to create a simple web service using the Plang programming language. This tutorial will walk you through setting up a web server, creating an API, managing a database table, and testing your application.

## Before starting

Make sure you have [installed Plang](./Install.md) & [IDE](/IDE.md)


## What You Will Learn
- How to start a web server
- How to set up an API
- How to create a database table
- How to insert data into a table
- How to create and run tests

## Video Tutorial
For a visual walkthrough, you can watch [this video](https://www.youtube.com/watch?v=m4QC19btS_I&list=PLbm1UMZKMaqfT4tqPtr-vhxMs4JGGFVEB&index=1). There might be some insights that are not in the written guide.

[![Watch the video](https://img.youtube.com/vi/m4QC19btS_I/hqdefault.jpg)](https://www.youtube.com/watch?v=m4QC19btS_I&list=PLbm1UMZKMaqfT4tqPtr-vhxMs4JGGFVEB&index=1)

## Step-by-Step Guide

### Step 1: Create Project Folder
Create a folder named `Todo` at a location of your choice. For example:
- **Windows**: `C:\apps\Todo`
- **MacOS/Linux**: `~/apps/Todo`

### Step 2: Setup Database Table
Create a file named `Setup.goal` in the `Todo` folder with the following content:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
```

**Explanation**: This code sets up a database table named `Todos` with columns for `task`, `due_date`, `completed`, and `created`. Plang automatically manages an `Id` column for you, which is useful for event sourcing and syncing between devices.

### Step 3: Build and Run Setup
To create the table in the database, build and run `Setup.goal`:

```bash
plang exec Setup
```

### Step 4: Start the Web Server
Create a file named `Start.goal` in the `Todo` folder with the following content:

```plang
Start
- start webserver
```

### Step 5: Create API Folder
Create a folder named `api` inside the `Todo` folder.

### Step 6: Create New Task API
Create a file named `NewTask.goal` in the `api` folder with the following content:

```plang
NewTask
- make sure that %task% and %due_date% is not empty, throw error
- insert into Todos %task%, %due_date%, write to %id%
- write out %id%
```

**Explanation**: This code defines an API endpoint for creating a new task. It checks that `task` and `due_date` are not empty, inserts the data into the `Todos` table, and returns the new task's ID.

### Step 7: Create List API
Create a file named `List.goal` in the `api` folder with the following content:

```plang
List
- select everything from Todos, write to %list%
- write out %list%
```

**Explanation**: This code defines an API endpoint for listing all tasks. It selects all entries from the `Todos` table and returns them.

### Step 8: Build and Run the Application
Ensure you are in the root of the `Todo` folder, then build and run the application:

```bash
plang exec
```

**Explanation**: This command starts the web server with the defined logic.

### Step 9: Test the API
#### Using a REST Client
- **New Task**: Send a POST request to `http://localhost:8080/api/newtask` with the following JSON body:
  ```json
  {
      "task": "Do homework",
      "due_date": "2023-12-27"
  }
  ```
  You should receive the ID of the new task.

- **List Tasks**: Send a GET request to `http://localhost:8080/api/list` to retrieve the list of tasks.

#### Using Plang
Create a folder named `test` in the root of the `Todo` folder.

- **New Task Test**: Create a file named `NewTask.goal` in the `test` folder with the following content:

  ```plang
  NewTask 
  - post http://localhost:8080/api/newtask
      {
          "task": "Do homework",
          "due_date": "2023-12-27"
      }
  ```

  Execute the test:

  ```bash
  plang exec test/NewTask
  ```

- **List Tasks Test**: Create a file named `GetList.goal` in the `test` folder with the following content:

  ```plang
  GetList 
  - get http://localhost:8080/api/list, write to %todos%
  - write out %todos%
  ```

  Execute the test:

  ```bash
  plang exec test/GetList
  ```

## Next Steps
To learn how to add LLM (Language Model) to your app, refer to the [next tutorial](./Todo_Llm.md).