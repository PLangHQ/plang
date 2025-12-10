# **Plang Trick: Database Selection Made Easy**

When you get data from a database in Plang, it always comes as a list—even if you’re only selecting one row using a **primary key**. That means if the database returns just one result, it’s still treated as a **list with one item**.

## **Basic Example: Getting a User**
If you want to grab a user’s **ID** and **name**, you would normally do this:

```plang
Database
- select id, name from users where %userId%, write to %users%
- write out "Name of user: %users[0].name% and id: %users[0].id%"
```
Since **%users%** is a list, you have to access the first item using **%users[0]%.**


## **Shortcut: Directly Using Variables**
Instead of dealing with a list, you can tell Plang to return **just one row** and automatically store the values into variables.

```plang
Database
- select id, name from users where %userId%, return 1
- write out "Name of user: %name% and id: %id%"
```
Plang will detect which columns you're selecting and **create variables for them**, making your code cleaner and easier to read.

## **Custom Variable Names**
You can also rename the columns while selecting:

```plang
Database
- select id as userId, name as fullName from users where %userId%, return 1
- write out "Name of user: %fullName% and id: %userId%"
```

Now, instead of using **%id%** and **%name%**, you can refer to them as **%userId%** and **%fullName%**—whatever makes sense for your code.

## **Why This Trick is Useful**
- No need to handle lists when dealing with single rows.
- Makes your code **shorter and cleaner**.
- Lets you use **custom variable names** for better readability.

A nice little trick to make database queries in Plang **way simpler!** 🚀