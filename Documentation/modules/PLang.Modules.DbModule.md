
# Db Module in PLang
## Introduction
The Db module in PLang is a powerful feature that allows you to interact with databases directly within your PLang code. It simplifies the process of performing database operations such as creating tables, inserting, updating, selecting, and deleting records. With the built-in SQLite database, you can quickly set up and manage your data without the need for external database management tools.

## For Beginners
If you're new to programming or databases, think of a database (Db) as a collection of information organized in a way that a computer program can quickly select, insert, update, or delete data. Databases are like interactive filing cabinets where your data is stored in tables, which are similar to spreadsheets with rows and columns. The Db module in PLang allows you to manage this data with simple commands.

## Best Practices for Db
When working with databases in PLang, it's important to follow best practices to ensure your code is efficient, readable, and maintainable:

1. **Use Descriptive Names**: Choose table and column names that clearly describe their contents.
2. **Keep It Simple**: Start with the basic operations and gradually add complexity as needed.
3. **Avoid Hardcoding Values**: Use variables for dynamic data to make your code more flexible.
4. **Use Transactions**: Group related operations within transactions to maintain data integrity.
5. **Handle Errors Gracefully**: Implement error handling to manage unexpected issues during database operations.

### Example: Inserting Data with Best Practices
```plang
- set var 'task_description' as 'Learn PLang'
- set var 'task_due_date' as \%Now\%
- begin transaction
- insert into tasks (description, due_date) values (%task_description%, %task_due_date%)
    on error call !HandleInsertError
- end transaction
```
In this example, we use descriptive variable names, a transaction to group the insert operation, and error handling to manage any issues that may arise.


# PLang Database Module Examples

The following examples demonstrate the usage of the PLang Database Module, which provides functionality for database operations such as creating tables, inserting records, updating, deleting, and executing raw SQL commands. The examples are sorted by their expected frequency of use in real-world applications.

## Selecting Data

### Example 1: Select All Records from a Table
```plang
- select * from items, write to %items%
- go through %items%, call !PrintOut
```

### Example 2: Select with Caching and Retry Logic
```plang
- select * from tasks
    cache for 10 minutes
    retry 2 times over 1 minute
    write to %tasks%
- go through %tasks%, call !PrintOut
```

## Inserting Data

### Example 3: Insert a Record with Variables
```plang
- set var 'description' as 'This is a task description'
- set var 'due_date' as 7.1.2024 21:47:43
- insert into tasks (description, due_date) values (%description%, %due_date%)
```

### Example 4: Insert and Retrieve ID of the Inserted Record
```plang
- insert into tasks (description, due_date) values ('This is a desc', '12.1.2024 21:47:43'), write to %id%
```

## Updating Data

### Example 5: Update a Record by ID
```plang
- update tasks set description='Updated first task' where id=%tasks[1].id%
```

### Example 6: Update a Record Using a Variable
```plang
- update tasks set description='Hello PLang world' where id=%id%
```

## Deleting Data

### Example 7: Delete All Records from a Table
```plang
- delete from tasks
```

## Transaction Management

### Example 8: Use Transactions for Multiple Operations
```plang
- begin transaction
- insert into tasks (description, due_date) values (%description%, %due_date%)
- delete from tasks where description=%description%
- end transaction
```

## Table Management

### Example 9: Create a Table with Columns
```plang
- create table tasks (description varchar(255) not null, due_date datetime not null, created datetime default now)
```

### Example 10: Alter a Table by Adding a Column
```plang
- alter table tasks add column completed bit
```

### Example 11: Drop a Table
```plang
- drop table items
```

### Example 12: Add a Unique Index to a Table
```plang
- add unique index to table tasks on (description, due_date)
```

## Auxiliary Goals

### PrintOut
```plang
- write %item.description% - %item.due_date%
```

The examples provided above cover the most common database operations in PLang. They are designed to be easily adapted to specific use cases by replacing the table names, column names, and values with those relevant to the user's database schema and requirements.


For a full list of examples, visit [PLang Db Module Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Db).

## Step Options
When writing steps in PLang, you have several options to enhance the functionality of each step. Click the links below for more details on how to use each option:

- [CacheHandler](/moduels/cacheHandler.md): Caches the results of database queries.
- [ErrorHandler](/moduels/ErrorHandler.md): Manages errors that occur during database operations.
- [RetryHandler](/moduels/RetryHandler.md): Retries a step if it fails due to transient issues.
- [CancelationHandler](/moduels/CancelationHandler.md): Cancels a step if certain conditions are met.
- [Run and forget](/moduels/RunAndForget.md): Executes a step without waiting for its completion.

## Advanced
For more advanced information on the Db module, including how it maps to underlying C# functionality, refer to the [PLang.Modules.DbModule_advanced documentation](./PLang.Modules.DbModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:48:48.
