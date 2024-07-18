# Database in Plang

Plang has a database built into the language. It uses an SQLite database. It's simple to use.

## Setup Database

Define the table structure in the `Setup.goal` file.

Create `Setup.goal` in the root directory of your project. The project can be located anywhere on your computer.

```plang
Setup
- create table tasks, columns: 
    text(string, not null)
    due_date(datetime, default now)
```

Here we are defining the table `tasks`, with two columns: `text` and `due_date`.

If you are not familiar with Plang, there is not really any syntax; you just need to follow [simple rules that structure the goal file](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md).

So you can write the `create table` in multiple ways. You can write it the way you would like, as long as your intent is clear.

Now build the code and run the setup to create the table in the database.

```bash
plang exec Setup
```

> `plang exec` both builds and runs the code.

## Database Location

The database is located in `.db/data.sqlite`. This folder is hidden, so you might need to show hidden items in your operating system. You can open the database using any database tool that can read SQLite; I use [DBeaver](https://dbeaver.io/).

## Insert, Update, and Select from Database

Let's insert and select some data from the database.

Create a new file `Start.goal` in your root folder:

```plang
Start
- insert into tasks, text='Buy milk', write into %id%
- insert into tasks, text='Fix car', due_date=%Now+2days%
- update tasks, set text='Buy milk now' where %id%
- select * from tasks, write to %tasks%
- delete from tasks
- write out %tasks%
```

You can now build this.

```bash
plang build
```

After the code has been built, you can run it:

```bash
plang
```

The default entry point of Plang apps is `Start.goal`, so you do not need to define the `Start.goal` when you execute `plang` in your terminal/console.

## SQL Statements

You don't really need to write valid SQL statements. The Plang builder will convert your intent into a valid statement.

You can do something like this:

```plang
Start
- select the 10 newest rows from tasks, write to %tasks%
- insert data into the tasks table, put the %text%, and have the due_date=%Now+1day%
```

## Other Databases

Plang supports other databases as well. You can read more about it in the [Services documentation](https://github.com/PLangHQ/plang/blob/main/Documentation/Services.md#location-of-injection-code).

## More Information

If Plang is interesting to you, you should dig a bit deeper:

* [Basic concepts and lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
* [Simple Todo example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) is a good start
* Check out the [GitHub repo](https://github.com/PLangHQ/)
* [Meet up on Discord](https://discord.gg/A8kYUymsDD) to discuss or get help
