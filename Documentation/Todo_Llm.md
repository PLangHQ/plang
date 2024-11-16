# Todo App with LLM

In this tutorial, we will enhance our previous Todo app by integrating a Language Learning Model (LLM). This will allow us to automate the categorization of tasks, making the user experience more seamless and efficient.

## What You Will Learn

- How to use LLM in Plang
- Modifying the database to add new columns
- Implementing "run and forget" functionality
- Focusing the builder on specific modules, such as [llm]

## Video of Tutorial

[![Tutorial Video](https://img.youtube.com/vi/pzgT_uNNNrE/hqdefault.jpg)](https://www.youtube.com/watch?v=pzgT_uNNNrE&list=PLbm1UMZKMaqfT4tqPtr-vhxMs4JGGFVEB&index=2)

## Step 1: Change Setup.goal

*You will learn how to add a column to an existing table.*

Add the following step to your `Setup.goal` file:

```plang
- add column 'category' to tbl 'Todos'
```

After adding it, your `Setup.goal` file should look like this:

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
- add column 'category' to tbl 'Todos'                
```

This code adds a new column named 'category' to the existing 'Todos' table, allowing us to store the category of each task.

## Step 2: Change NewTask.goal

We want to categorize tasks automatically using LLM to simplify the user experience. Modify `NewTask.goal` as follows:

```plang
NewTask
- if %request.task% and %request.due_date% is empty
    - show error "Task & due_date cannot be empty"
- insert into Todos, %request.task%, %request.due_date%, write to %id%
- call !Categorize, dont wait
- write out %id%

Categorize
- [llm]: system: categories the user input by 3 categories, 'Work', "home", 'hobby'
    user: %request.task%
    scheme: {category:string}
- update table Todos, set %category% where %id%
```

### Explanation

- `call !Categorize, dont wait`: This instructs Plang not to wait for the response from the `!Categorize` goal, allowing the app to continue running while the LLM processes the task categorization.
- `[llm]`: This indicates to Plang that the LLM module should be used. It helps the language detect which module to use.
- `scheme`: This forces the LLM to return a specific structure, which is automatically loaded as a variable. In this case, `%category%` is used in the next step to update the task's category.

For more on LLM, check out the [LlmModule documentation](./modules/PLang.Modules.LlmModule.md).

## Step 3: Test the API Endpoints

You can test the API endpoints using your favorite REST client.

To create a new task, send a POST request to `http://localhost:8080/api/newtask` with the following JSON body:

```json
{
    "task":"Buy some Lego",
    "due_date": "2023-12-27"
}    
```

Now, if you open `http://localhost:8080/api/List`, you should see that your new task has a category.

### Using Plang to Test

Modify your `TestNewTask.goal` file in the `test` directory with the following code:

```plang
TestNewTask
- post http://localhost:8080/api/newtask
    {
        "task":"Buy some Lego",
        "due_date": "2023-12-27"
    }
    write to %result%
- write out %result%
```

Then, execute the `TestNewTask.goal` file:

- Press F5 in VS Code, type `TestNewTask` in the prompt window, and press enter.
- Or, if you prefer the terminal:

    ```bash
    plang exec test/TestNewTask
    ```

After execution, open `http://localhost:8080/api/List` to see that your new task has a category.

## Next Steps

Learn how to add `%Identity%` to your app by following the [next tutorial](./Todo_Identity.md).