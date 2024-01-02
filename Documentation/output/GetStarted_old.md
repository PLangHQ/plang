# Todo Application Guide

This guide will walk you through the process of creating a Todo application using the Plang programming language. For a more detailed walkthrough, you can watch this [video tutorial](https://www.youtube.com/watch?v=abew4btk34).

## Prerequisites

- Plang installed on your system (Windows, Linux, or MacOS)
- Basic knowledge of Plang programming language

## Steps

### 1. Create a Project Directory

Create a new directory named `Todo` at your preferred location. For instance, you can create it at `c:\plang\Todo` on Windows.

### 2. Create `Setup.goal`

Create a new file named `Setup.goal` in the `Todo` directory and add the following code:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
```

Note: Plang automatically creates and manages an `id` column for each table, enabling Event source for syncing between devices.

### 3. Build and Run `Setup.goal`

To create the `Todos` table in the database, build and run `Setup.goal` using the following command:

```bash
plang exec Setup
```

### 4. Create `Start.goal`

Create a new file named `Start.goal` in the `Todo` directory and add the following code:

```plang
Start
- start webserver
```

### 5. Create `api` Directory

Create a new directory named `api` in the `Todo` directory.

### 6. Create `NewTask.goal`

Create a new file named `NewTask.goal` in the `api` directory and add the following code:

```plang
NewTask
- make sure that %task% and %due_date% is not empty, throw error
- insert into Todos %task%, %due_date%, write to %id%
- write out %id%
```

### 7. Create `List.goal`

Create a new file named `List.goal` in the `api` directory and add the following code:

```plang
List
- select everything from Todos, write to System.Collections.Generic.List`1[System.Object]
- write out System.Collections.Generic.List`1[System.Object]
```

### 8. Build and Run the Application

To build and run the application, navigate to the root of the `Todo` directory and execute the following command:

```bash
plang exec
```

You now have a running webserver with logic.

### 9. Test the Application

To test the application, you can use your favorite REST client or write it in Plang. 

To create a new task, send a POST request to `http://localhost:8080/api/newtask` with the following JSON body:

```json
{
    "task":"Do homework",
    "due_date": "2023-27-12"
}
```

You should receive the `id` of the new task in response.

To list all tasks, send a GET request to `http://localhost:8080/api/list`. You should see the task you created in the response.

Congratulations! You have successfully created a Todo application using Plang.