# Todo Webservice Guide

This guide will walk you through the process of creating a Todo web service using the Plang programming language. 

![Todo Webservice](https://img.youtube.com/vi/oE3NC4zmRA4/0.jpg)

You can also watch it in [this video](https://www.youtube.com/watch?v=abew4btk34). There might be some insights that are not in the written guide.

## Prerequisites

- Plang [installed on your system](Install.md)
- Basic knowledge of Plang programming language
- Optional: A REST client for testing the API endpoints

## Steps

### 1. Create a Project Directory

Create a new directory named `Todo` at your preferred location. For instance, you can create it at `c:\plang\Todo` on Windows.

For Linux and MacOS, you can create it at `~/plang/Todo`.

### 2. Create `Setup.goal`

In the `Todo` directory, create a new file named `Setup.goal` and add the following code:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
```

Note: Plang will automatically create and manage an `id` column for the table. By allowing Plang to handle `id` you enable syncing between devices.

### 3. Build and Run `Setup.goal`

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

### 5. Create `api` Directory

In the `Todo` directory, create a new directory named `api`. `api` stands for application programming interface, it is how computer communicate between them.

### 6. Create `NewTask.goal`

In the `api` directory, create a new file named `NewTask.goal` and add the following code:

```plang
NewTask
- make sure that %task% and %due_date% is not empty, throw error
- insert into Todos %task%, %due_date%, write to %id%
- write out %id%
```

### 7. Create `List.goal`

In the `api` directory, create a new file named `List.goal` and add the following code:

```plang
List
- select everything from Todos, write to %todos%
- write out %todos%
```

### 8. Build and Run the Code

Navigate to the root of the `Todo` directory and execute the following command:

```bash
plang exec
```

This will start a web server with the logic you've defined.

### 9. Test the API Endpoints

You can now test the API endpoints using your favorite REST client.

To create a new task, send a POST request to `http://localhost:8080/api/newtask` with the following JSON body:

```json
{
    "task":"Do homework",
    "due_date": "2023-27-12"
}
```

You should receive the `id` of the new task in the response.

Alternatively, you can create a new `AddTask.goal` file in the `test` directory with the following code:

```plang
NewTask
- post http://localhost:8080/api/newtask
    {
        "task":"Do homework",
        "due_date": "2023-27-12"
    }
```

Then, execute the `AddTask.goal` file:

```bash
plang exec test/NewTask
```

To retrieve the list of tasks, send a GET request to `http://localhost:8080/api/list`. This should return a list of tasks you've created.

Alternatively, you can create a new `GetList.goal` file in the `test` directory with the following code:

```plang
GetList 
- get http://localhost:8080/api/list, write to %todos%
- write out %todos%
```

Then, execute the `GetList.goal` file:

```bash
plang exec test/GetList
```