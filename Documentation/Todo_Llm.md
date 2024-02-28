# Todo with LLM Integration

This guide will walk you through the process of enhancing a Todo web service by integrating LLM (Language Learning Model) capabilities. We'll also cover how to modify the database schema and introduce the concept of "run and forget" for asynchronous operations in plang.

## What we want to do

What we we want is to categorize the tasks in the todo list. Since we have the power of LLM in the language, lets use it to categories the task for us. Saving us the very precious time of selecting a category.

## 1. Update `Setup.goal`

Let's add a new column to your `Todos` table, you need to modify the `Setup.goal` file. 

Add the following line into `Setup.goal`

> Cost estimate $0.182

```plang
- add column 'category' to tbl 'Todos'
```

The `Setup.goal` file will look like this

```plang
Setup
- create table Todos 
    columns:    task(string, not null), due_date(datetime, not null), 
                completed(bool, false), created(datetime, now)
- add column 'category' to tbl 'Todos'
```

By adding the `- add column 'category' to tbl 'Todos'` step, you instruct plang to update the database schema to include a new column named 'category'.

## 2. Modify `NewTask.goal`

> Cost estimated: $0.55

To categorize tasks automatically using LLM, you'll need to adjust the `NewTask.goal` file. 

Here's the updated code:
```plang
NewTask
- make sure that %request.task% and %request.due_date% is not empty, throw error
- insert into Todos %request.task%, %request.due_date%, write to %id%
- call !Categorize, dont wait
- write out %id%

Categorize
- [llm] system: categories the user input by 3 categories, 'Work', "Home", 'Hobby'
    user: %request.task%
    scheme: {category:string}
- update table Todos, set %category% where %id%
```

#### Explanation

- The `call !Categorize, dont wait` step is an example of the "run and forget" pattern. It tells plang to call the `!Categorize` goal but not to wait for its completion. This is useful when the operation (like LLM processing) might take a few seconds, and you don't want to delay the user's workflow.
- The `[llm]` tag in the `system:` step indicates that this step should utilize the LLM module. It's helpfull for the plang builder, not necesary.
- The `scheme` keyword within the llm step defines the expected structure of the LLM's response. In this case, it expects a string value for `category`. The returned structure is automatically converted into variables, allowing you to use `%category%` in the subsequent step.
- If you need a structured response from LLM, the `scheme` is essential. It's a powerful feature that ensures the LLM's output matches the expected format.
- For more information on how to use the LLM module in plang, please refer to the LLM Module documentation: [PLang.Modules.LlmModule.md](./modules/PLang.Modules.LlmModule.md).

### Test the API Endpoints

> Cost estimated: $0.153

Lets modify our `TestNewTask.goal` file (just change the task text)

- Modify `TestNewTask.goal` file in the `test` directory with the following code:

    ```plang
    TestNewTask
    - post http://localhost:8080/api/newtask
        {
            "task":"Buy some Lego",
            "due_date": "%Now+2days%"
        }
        write to %result%
    - write out %result%
    ```

Then, execute the `TestNewTask.goal` file:

- Press F5 in VS Code, in the prompt window type in `TestNewTask` and press enter
- or if you prefer terminal

    ```bash
    plang exec test/TestNewTask
    ```

Now if you open the http://localhost:8080/api/List, you should see that your new task has a category.

Alternatively, you can test the API endpoints using your favorite REST client.

To create a new task, send a POST request to `http://localhost:8080/api/newtask` with the following JSON body:

```json
{
    "task":"Buy some Lego",
    "due_date": "2023-12-27"
}    
```

# Next tutorials


- Learn how to use [%Identity% in plang](./Todo_Identity.md), instead of username & password.

