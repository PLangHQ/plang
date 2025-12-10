# Security - Isolated User Storage Pattern

In Plang, when you are using sqlite database for your web service, you can seperate each user data into seperate database, this will provide extra security in a way that there is no chance of one user getting other users data, as the data it self live in seperate databases. I call this *Isolated User Storage Pattern*

In a typical way when we are retrieveing users data, it looks something like this

```plang
Bookmarks
- SELECT * FROM bookmarks WHERE userId=%userId%, write to %bookmarks%
- write out %bookmarks%
```
We need to filter the table by the `%userId%`

If you follow the pattern of each user having it's own database, the code should look like this
```plang
Bookmarks
- set datasource as '%Identity%/.db/data.sqlite'
- SELECT * FROM bookmarks, write to %bookmarks%
- write out %bookmarks%
```

There is now no `WHERE` statement in the sql query, as this user own all the data in the database.

This prevents a too common bug that leaks data, where hacker can change the parameter of the request to get others users data. I bug that should not exists, but still happens today.

You can then easily allow the user to download all the data

```plang
ExportDatabase
- read bytes of '%Identity%/.db/data.sqlite', %bytes%
- write out %bytes%
```

If user request his data to be deleted, it is simple
```plang
DeleteUser
- delete file '%Identity%/.db/data.sqlite'
```

This only applies when you are using sqlite database. 

If you have other databases, you can [inject the supported type](../Services.md). Plang does not support this behaviour with other databases.

## Collecting user behaviour

This might change how you do analytics for you product. Since you cannot simply sum a table with all the user, you will need to save that data seperatly. This forces you to define what you want to analyze and why.

Lets say you want to know how many bookmarks are in the system, one option is to travers through all databases to collect this data. The 'correct' way, would be to store it in a central database on each change

```plang
SaveBookmark
- set datasource as '%Identity%/.db/data.sqlite'
- insert into bookmarks %pageId%
- set datasource as 'data'
- update table stats set bookmark_count=bookmark_count+1
```
