# Lets create and app

Lets create an app that downloads user information from a url and writes into database.

We will be using 
```curl
https://jsonplaceholder.typicode.com/users/1
```

it gives us this json
```json
{
  "id": 1,
  "name": "Leanne Graham",
  "username": "Bret",
  "email": "Sincere@april.biz",
  "address": {
    "street": "Kulas Light",
    "suite": "Apt. 556",
    "city": "Gwenborough",
    "zipcode": "92998-3874",
    "geo": {
      "lat": "-37.3159",
      "lng": "81.1496"
    }
  },
  "phone": "1-770-736-8031 x56442",
  "website": "hildegard.org",
  "company": {
    "name": "Romaguera-Crona",
    "catchPhrase": "Multi-layered client-server neural-net",
    "bs": "harness real-time e-markets"
  }
}
```

We want to store the name, email, address. So to store something, we need a database. Luckely plang has built in database. So lets define that table

We create `Setup.goal` file, and give the Goal name `Setup`

```plang
Setup
- create table users, columns: name, email, address(json), created(datetime, now)
```

I also added the created date, always nice to have.

Next we want to create the `Start.goal` file, and give it the goal name `Start`

```plang
Start
- get https://jsonplaceholder.typicode.com/users/1, into %userInfo%
- insert into users, %userInfo.name%, %userInfo.email%, %userInfo.address%
- select * from users, write to %users%
- write out %users%
```

lets now build this code. if you are on VS Code, press F5, you can even set break points

You should see the output in your Debug panel 

For those using console/terminal, open it in the working dir and run
```bash
plang build
```
it will fail, it fails because the tables haven't been created in the database, and we need them to validate the `insert` and `select` queries.

So now you have to run 
```plang
plang run setup
```
and after that you can run 

```plang
plang build
```
 again.
after you have built it, run
```bash
plang
```

You should now see one user, refresh and they will become 2


Next is [Lesson 4 - How does it really work?](./Lesson%204.md)