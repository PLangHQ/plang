## Todo New Approach

Create `llm` folder  
Create `system.txt` in `llm`  
Add to `system.txt`:  
```txt
User will provide you with a text of tasks. 
Your job is to analyze the text and create list of tasks to return.

Current date & time is %Now%

task: the description of the task
due_date: when the task is due, if not defined, set default to 2 days from now
category: categorize the user input by 3 categories, 'Work', "Home", 'Hobby'
```

Create `NewLlmTask.goal` in `api` folder:  
```plang
NewLlmTask
- read file llm/system.txt, write to %system%, load vars
- [llm] system:%system%
        user: %request.tasks%
        scheme:[{task:string, due_date:date, category:string}]
        write to %taskList%
- for each %taskList%, call !SaveTask

SaveTask
- insert into Todos table, %item.task%, %item.due_date%, %Identity%
```

Create `TestNewLlmTask` in `test` folder:  
```plang
TestNewTask
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
    write to %result%
- write out %result%
```